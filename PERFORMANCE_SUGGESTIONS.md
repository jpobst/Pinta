# Pinta Performance Improvement Suggestions

## Overview

This document catalogs opportunities for performance improvements in the Pinta codebase, with particular focus on vectorization (SIMD/AVX/SSE) opportunities. Each suggestion is numbered for easy reference and includes an estimated performance benefit.

The Pinta codebase already employs several good performance practices:
- `Span<T>` and `ReadOnlySpan<T>` for zero-allocation pixel access
- `[MethodImpl(MethodImplOptions.AggressiveInlining)]` on hot-path blend operations
- `in` parameters for struct pass-by-reference
- Lookup tables (MAS table) for fast division
- Bit-shifting arithmetic for intensity and scaling calculations
- `stackalloc` for small temporary buffers
- Row-by-row processing for cache locality
- Task-based parallelism for tileable effects

However, there is **zero use** of `System.Numerics.Vector<T>`, `Vector128<T>`, `Vector256<T>`, or `System.Runtime.Intrinsics` anywhere in the codebase. This represents a significant untapped performance opportunity, especially since Pinta targets .NET 10 which has excellent SIMD support.

### Estimated Benefit Scale

| Rating | Meaning |
|--------|---------|
| 🔴 **Very High** | 4–8× speedup potential for the affected operation |
| 🟠 **High** | 2–4× speedup potential |
| 🟡 **Medium** | 1.3–2× speedup potential |
| 🟢 **Low** | 1.1–1.3× speedup potential or limited scope |

---

## Category A: Blend Operations (Highest Impact)

### A-1: Vectorize `BinaryPixelOp.Apply(Span, ReadOnlySpan, ReadOnlySpan)` with SIMD

**Files:** `Pinta.Core/Algorithms/PixelOps/BinaryPixelOp.cs` (line 16)

**Current code:**
```csharp
public virtual void Apply(Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> lhs, ReadOnlySpan<ColorBgra> rhs)
{
    for (int i = 0; i < dst.Length; ++i)
        dst[i] = Apply(lhs[i], rhs[i]);
}
```

**Suggestion:** Process 4–8 pixels at a time using `Vector128<int>` or `Vector256<int>`. Each `ColorBgra` is 32 bits, so a 256-bit AVX register can hold 8 pixels, and a 128-bit SSE register holds 4. The normal blend operation involves only integer multiplications, additions, and shifts — all perfectly suited for SIMD.

For the `NormalBlendOp` specifically, the `ComputePremultiplied<ChannelBlend>` call processes each channel (B, G, R, A) independently with identical math, which maps directly to SIMD lane operations.

**Estimated benefit:** 🔴 **Very High (4–8×)** for blend-heavy workloads (layer compositing with semi-transparent layers). This is the single most-called pixel operation in Pinta — every layer composite triggers it for every pixel.

---

### A-2: Vectorize `BlendOpHelper.ComputePremultiplied<T>` for Batch Processing

**File:** `Pinta.Core/Algorithms/BlendOpHelper.cs`

**Current code:**
The method processes a single pixel at a time:
```csharp
public static ColorBgra ComputePremultiplied<TChannelBlend>(in ColorBgra bottom, in ColorBgra top)
```

**Suggestion:** Add a batch overload that processes `Span<ColorBgra>` using SIMD. The formula:
```
C_out = (1 - A_b) * C_a + (1 - A_a) * C_b + Blend(C_a, C_b)
```
can be expressed as vectorized operations. The key insight is that for simple blend modes (Normal, Multiply, Screen, Additive, Difference), the `BlendChannel` function is trivial arithmetic that vectorizes perfectly.

For each blend mode, create a SIMD-aware `ApplyBatch` method. The "simple" modes (Normal: `Ab * Ca`, Multiply: `Ca * Cb`, Screen: `Aa * Cb + Ab * Ca - Ca * Cb`, Additive: `Ab * Ca + Aa * Cb`, Difference: `Math.Abs(...)`) can be implemented with `Vector256<ushort>` operations using `Avx2.MultiplyHigh`, `Avx2.Subtract`, etc.

**Estimated benefit:** 🔴 **Very High (4–8×)** for the simple blend modes (Normal, Multiply, Screen, Additive, Difference, Lighten, Darken, Xor). More complex modes (ColorDodge, Overlay) that use the MAS table are harder to vectorize but still feasible with gather instructions.

---

### A-3: SIMD-Optimized Normal Blend with Early-Exit Batching

**File:** `Pinta.Core/Algorithms/PixelOps/UserBlendOps.Normal.cs`

**Current code:**
```csharp
public static ColorBgra ApplyStatic(in ColorBgra bottom, in ColorBgra top)
{
    if (top.A == 255) return top;
    if (top.A == 0) return bottom;
    return BlendOpHelper.ComputePremultiplied<ChannelBlend>(bottom, top);
}
```

**Suggestion:** For the batch path, use SIMD comparison operations to create masks for the three cases (fully opaque, fully transparent, partial). Process fully opaque/transparent pixels with simple vector copy operations and only fall through to the full blend computation for partial-alpha pixels. On real-world images, large contiguous regions are often fully opaque or fully transparent, so this can skip significant computation.

Use `Avx2.CompareEqual` to detect alpha == 255 and alpha == 0 cases in bulk, then use `Avx2.BlendVariable` to select between the fast-path (copy) and slow-path (full blend) results.

**Estimated benefit:** 🟠 **High (2–4×)** on typical images with large opaque/transparent regions, on top of the base SIMD improvement from A-1/A-2.

---

### A-4: Vectorize `UnaryPixelOp.Apply(Span, ReadOnlySpan)` Inner Loop

**File:** `Pinta.Core/Algorithms/PixelOps/UnaryPixelOp.cs` (line 27)

**Current code:**
```csharp
public override void Apply(Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> src)
{
    for (int i = 0; i < src.Length; ++i)
        dst[i] = Apply(src[i]);
}
```

**Suggestion:** For specific unary operations that are pure arithmetic, add SIMD batch overloads. Key candidates:
- **Invert** (XOR with `0x00FFFFFF`): A single `Vector256.Xor()` call
- **SetAlphaChannel** (OR with `0xFF000000`): A single `Vector256.BitwiseOr()` call
- **Desaturate** (intensity calculation): Vectorized weighted sum
- **Constant** (fill): `Vector256.Create(value)` store

**Estimated benefit:** 🔴 **Very High (4–8×)** for Invert and SetAlpha (trivial SIMD). 🟠 **High (2–4×)** for Desaturate and intensity-based operations.

---

## Category B: Core Color Operations

### B-1: Vectorize `ColorBgra.Lerp` for Batch Operations

**File:** `Pinta.Core/Classes/Color/ColorBgra.Blending.cs`

**Current code:**
```csharp
public static ColorBgra Lerp(in ColorBgra from, in ColorBgra to, byte frac)
    => FromBgra(
        b: Mathematics.LerpByte(from.B, to.B, frac),
        g: Mathematics.LerpByte(from.G, to.G, frac),
        r: Mathematics.LerpByte(from.R, to.R, frac),
        a: Mathematics.LerpByte(from.A, to.A, frac));
```

**Suggestion:** Add a batch `Lerp(ReadOnlySpan<ColorBgra> from, ReadOnlySpan<ColorBgra> to, byte frac, Span<ColorBgra> result)` that processes 8+ pixels at once. Lerp is `from + frac * (to - from)` which maps directly to SIMD multiply-add operations. This is called by bilinear sampling and gradient rendering — two very common operations.

**Estimated benefit:** 🟠 **High (2–4×)** for effects that heavily use color interpolation (gradients, blur, distortion effects with bilinear sampling).

---

### B-2: Vectorize `ColorBgra.Blend(ReadOnlySpan<ColorBgra>)` Aggregation

**File:** `Pinta.Core/Classes/Color/ColorBgra.Blending.cs` (line 88)

**Current code:**
```csharp
public static ColorBgra Blend(ReadOnlySpan<ColorBgra> colors)
{
    Blender aggregate = new();
    for (int i = 0; i < count; ++i)
        aggregate += colors[i];
    return aggregate.Blend();
}
```

**Suggestion:** Use SIMD horizontal addition to accumulate B, G, R, A channels across multiple pixels simultaneously. Widen bytes to `Vector256<int>` and use `Avx2.Add` for the accumulation loop. This is used by anti-aliased sampling in warp effects and bilinear interpolation.

**Estimated benefit:** 🟡 **Medium (1.5–2×)** — called less frequently than blend ops, but still impactful for warp/distortion effects that sample many points per output pixel.

---

### B-3: Vectorize `ToPremultipliedAlpha()` / `ToStraightAlpha()` Conversions

**File:** `Pinta.Core/Classes/Color/ColorBgra.Blending.cs`

**Current code:**
```csharp
public readonly ColorBgra ToPremultipliedAlpha()
    => FromBgra((byte)(B * A / 255), (byte)(G * A / 255), (byte)(R * A / 255), A);

public readonly ColorBgra ToStraightAlpha()
{
    if (A > 0)
        return FromBgra((byte)(B * 255 / A), (byte)(G * 255 / A), (byte)(R * 255 / A), A);
    else
        return Zero;
}
```

**Suggestion:** These conversions are called in inner loops of `GaussianBlurEffect`, `LocalHistogram`, `ReduceNoiseEffect`, and `VignetteEffect`. Add batch methods `ToPremultipliedAlpha(ReadOnlySpan<ColorBgra> src, Span<ColorBgra> dst)` that use SIMD to process 8–16 pixels at a time. The premultiplied conversion is straightforward (`channel * alpha / 255`), and the division by 255 can be approximated as `(x + 128) >> 8` or use the `Avx2.MultiplyHigh` instruction for exact results.

The `ToStraightAlpha()` direction requires per-pixel division by alpha, which is harder to vectorize but can use SIMD reciprocal approximations (`Avx.Reciprocal`) combined with a Newton-Raphson refinement step, or a vectorized lookup table.

**Estimated benefit:** 🟠 **High (2–4×)** for effects that frequently convert between alpha formats (Gaussian Blur, Local Histogram effects, Reduce Noise).

---

### B-4: Vectorize `GetIntensityByte()` for Bulk Conversion

**File:** `Pinta.Core/Classes/Color/ColorBgra.Conversion.cs` (line 76)

**Current code:**
```csharp
public readonly byte GetIntensityByte()
    => (byte)((7471 * B + 38470 * G + 19595 * R) >> 16);
```

**Suggestion:** Add a batch `GetIntensityBytes(ReadOnlySpan<ColorBgra> src, Span<byte> dst)` that computes luminance for multiple pixels using SIMD. The weighted sum with fixed-point shift maps perfectly to `Avx2.MultiplyAddAdjacent` or manual multiply-add sequences. This is used by desaturation, black-and-white, and sepia effects.

**Estimated benefit:** 🟡 **Medium (1.5–2×)** — the scalar version is already fast due to integer-only math, but SIMD can still process 16–32 bytes per cycle.

---

## Category C: Effects — Blur and Convolution

### C-1: Implement Separable Gaussian Blur with SIMD Accumulation

**File:** `Pinta.Effects/Effects/GaussianBlurEffect.cs`

**Current code:** Triple-nested loop (lines 88–241) with per-pixel kernel accumulation using `long` accumulators:
```csharp
for (int wx = 0; wx < wlen; ++wx) {
    for (int wy = 0; wy < wlen; ++wy) {
        ColorBgra c = src.GetColorBgra(...).ToStraightAlpha();
        aSums[wx] += wp * c.A;
        bSums[wx] += wp * c.B;
        // ... etc
    }
}
```

**Suggestion:** The current implementation applies the kernel as a 2D convolution. It should be refactored to a **separable two-pass blur** (horizontal pass, then vertical pass), which reduces the operation from O(n²) per pixel to O(n) per pixel (where n is kernel width). Each pass is then a 1D convolution that vectorizes naturally:

1. **Horizontal pass:** For each row, slide a SIMD accumulator across the row. Load 8 adjacent pixels into a `Vector256<int>` (after widening from bytes), multiply by the kernel weight, and accumulate.
2. **Vertical pass:** For each column, load pixels from consecutive rows (stride access) and perform the same SIMD accumulation.

Additionally, use `Avx2.MultiplyAddAdjacent` for the weight × channel accumulation, and accumulate into `Vector256<int>` instead of scalar `long[]`.

**Estimated benefit:** 🔴 **Very High (4–10×)** for large blur radii. The algorithmic improvement (separable) alone gives ~radius/2× speedup. Combined with SIMD, large blur operations (radius 10+) could see 10× or more improvement.

---

### C-2: Vectorize Convolution Kernel Operations (Sharpen, Edge Detect, Emboss, Relief, Outline Edge)

**Files:**
- `Pinta.Effects/Effects/SharpenEffect.cs`
- `Pinta.Effects/Effects/EdgeDetectEffect.cs`
- `Pinta.Effects/Effects/EmbossEffect.cs`
- `Pinta.Effects/Effects/ReliefEffect.cs`
- `Pinta.Effects/Effects/OutlineEdgeEffect.cs`

**Current code:** These effects use `LocalHistogram` or direct kernel application with scalar per-pixel processing.

**Suggestion:** Implement a generic SIMD convolution helper that takes a kernel matrix and processes rows using vectorized multiply-accumulate. For 3×3 kernels, the entire kernel can often fit in registers, and the horizontal accumulation across a row uses SIMD shift-and-add patterns.

**Estimated benefit:** 🟠 **High (2–4×)** for 3×3 and 5×5 kernels. Larger kernels benefit more.

---

### C-3: Vectorize Motion Blur Sample Accumulation

**File:** `Pinta.Effects/Effects/MotionBlurEffect.cs`

**Current code:** Per-pixel loop that accumulates N samples along a motion vector using `ColorBgra.Lerp()`:
```csharp
// Iterates through offset points, accumulating colors
```

**Suggestion:** Pre-compute the sample positions for the row, then use SIMD to accumulate the sampled pixel values. Since all pixels in a row share the same motion vector, the sample offsets are uniform. Load 4–8 sample pixels at once, widen to 16-bit, accumulate with SIMD add, then divide at the end.

**Estimated benefit:** 🟡 **Medium (1.5–2×)** — limited by the scattered memory access pattern of sampling along the motion vector.

---

### C-4: Vectorize Radial Blur and Zoom Blur Accumulation

**Files:**
- `Pinta.Effects/Effects/RadialBlurEffect.cs`
- `Pinta.Effects/Effects/ZoomBlurEffect.cs`

**Current code:** Both effects accumulate multiple samples per pixel in a tight loop. Zoom blur uses 64 sample points with fixed-point arithmetic.

**Suggestion:** For Zoom Blur specifically, the 64-iteration accumulation loop with bit shifts is highly vectorizable. Load the source pixels at the 64 sample positions into SIMD registers and use vectorized addition. The fixed-point coordinate tracking (`fxFixed`, `fyFixed`) can be vectorized by computing 4–8 sample positions in parallel.

For Radial Blur, the sin/cos-based rotation per sample already uses lookup tables/approximations — vectorize the coordinate generation and the pixel accumulation separately.

**Estimated benefit:** 🟡 **Medium (1.5–2×)** for both. Memory access patterns (scattered reads) limit the benefit.

---

## Category D: Effects — Pixel-Independent Operations

### D-1: Vectorize Brightness/Contrast Lookup Table Application

**File:** `Pinta.Effects/Adjustments/BrightnessContrastEffect.cs`

**Current code:** Per-pixel application of a lookup table through `BrightnessContrastPixelOp.Apply()`.

**Suggestion:** The lookup table itself cannot be vectorized, but the effect can use `Avx2.GatherVector256` to perform 8 simultaneous lookups from the LUT. Alternatively, since the brightness/contrast LUT is monotonic and can be expressed as `clamp(a * x + b)`, replace the LUT with SIMD arithmetic: load 8 pixels, widen to 16-bit, multiply by contrast factor, add brightness offset, clamp, and narrow back to 8-bit.

**Estimated benefit:** 🟠 **High (2–4×)** if replaced with direct SIMD arithmetic. 🟡 **Medium (1.3–2×)** if using gather-based LUT lookups.

---

### D-2: Vectorize Levels and Curves Adjustment

**Files:**
- `Pinta.Core/Algorithms/PixelOps/UnaryPixelOps.cs` (Level class, line ~263–503)
- `Pinta.Effects/Adjustments/LevelsEffect.cs`
- `Pinta.Effects/Adjustments/CurvesEffect.cs`

**Current code:** Curves and Levels use per-channel lookup tables (256 entries each for R, G, B) applied pixel-by-pixel:
```csharp
ColorBgra ret = Apply(color); // Applies LUT per channel
```

**Suggestion:** Use `Avx2.Shuffle` or `Ssse3.Shuffle` as a vectorized 4-bit LUT for the lower nibble, combined with two lookups and a blend for the full 8-bit range. Alternatively, use `Avx2.GatherVector256` for 8-way parallel LUT lookup. For curves that are smooth functions, approximate with SIMD linear interpolation between control points.

**Estimated benefit:** 🟡 **Medium (1.5–2×)** — LUT operations are already fast but SIMD gather can still help.

---

### D-3: Vectorize Posterize Effect

**File:** `Pinta.Effects/Adjustments/PosterizeEffect.cs`

**Current code:** Applies a per-pixel quantization operation that maps each channel value through a short lookup table.

**Suggestion:** Posterize can be expressed as `(channel / step) * step` where `step = 256 / levels`. This is pure integer arithmetic that vectorizes trivially with `Vector256<byte>` operations — divide can be replaced by multiply + shift. Process 32 bytes (8 BGRA pixels) per SIMD operation.

**Estimated benefit:** 🟠 **High (2–4×)** — simple arithmetic, perfect SIMD candidate.

---

### D-4: Vectorize Sepia Tone Effect

**File:** `Pinta.Effects/Adjustments/SepiaEffect.cs`

**Current code:** Chains desaturate → levels → lerp per pixel:
```csharp
ColorBgra after = ColorBgra.Lerp(original, adjustedColor, frac);
```

**Suggestion:** Combine the desaturate, level adjustment, and lerp into a single SIMD pass. The entire sepia computation for a pixel is:
1. Compute intensity: weighted sum of R, G, B (vectorizable)
2. Apply levels LUT (can be approximated as linear transform in SIMD)
3. Lerp between original and tinted color (vectorizable)

Fusing these three steps eliminates intermediate `ColorBgra` construction and reduces memory traffic.

**Estimated benefit:** 🟡 **Medium (1.5–2×)** — three operations fused into one SIMD pass.

---

### D-5: Vectorize Add Noise Effect

**File:** `Pinta.Effects/Effects/AddNoiseEffect.cs`

**Current code:** Per-pixel noise generation using lookup tables and byte arithmetic with clamping.

**Suggestion:** The noise lookup, byte addition with clamping (`Utility.ClampToByte`), and intensity calculation can all be vectorized. Use `Avx2.AddSaturate` for byte addition with automatic clamping (saturating add), eliminating the need for explicit `ClampToByte` calls. Process 8 pixels per iteration.

**Estimated benefit:** 🟠 **High (2–4×)** — `AddSaturate` is a single instruction that replaces add + clamp.

---

### D-6: SIMD-Accelerate Vignette Effect sRGB Conversion

**File:** `Pinta.Effects/Effects/VignetteEffect.cs`

**Current code:** Per-pixel sRGB ↔ linear conversion using lookup tables, followed by distance-based darkening:
```csharp
SrgbUtility.ToLinear(pixel.R) // per channel, per pixel
```

**Suggestion:** The `ToLinear()` conversion uses a 256-entry LUT. Use `Avx2.GatherVector256` for parallel lookups (8 pixels × 3 channels = 24 lookups batched into 3 gather operations). The distance calculation (`dx*dx + dy*dy`) for the vignette falloff is identical for all pixels in a row (same y-coordinate), so pre-compute the y-component and vectorize only the x-component.

**Estimated benefit:** 🟡 **Medium (1.3–2×)** — the per-row y-component optimization alone provides some benefit; SIMD gathers for the LUT add more.

---

## Category E: Distortion and Warp Effects

### E-1: Vectorize Bilinear Sampling for Distortion Effects

**File:** `Pinta.Core/Extensions/Cairo/CairoExtensions.Samples.cs`

**Current code:** `GetBilinearSampleClamped()` fetches 4 neighboring pixels, computes 4 weights, and calls `BlendColors4W16IP`:
```csharp
public static ColorBgra GetBilinearSampleClamped(...)
{
    // Floor, fractional parts, 4 pixel fetches, BlendColors4W16IP
}
```

**Suggestion:** Add a batch bilinear sampling method that processes N sample requests at once. Pre-pack the 4 source pixels for each sample into SIMD registers, compute weights in SIMD, and perform the weighted blend in parallel. This is especially effective for warp effects (Bulge, Twist, Dents, Tile, Frosted Glass) which call bilinear sampling for every output pixel.

Use `Avx2.GatherVector256` to fetch the 4 neighbor pixels per sample in a single gather instruction (if positions are pre-computed as integer offsets into the pixel buffer).

**Estimated benefit:** 🟠 **High (2–3×)** for distortion effects. These effects are dominated by bilinear sampling cost.

---

### E-2: Vectorize Warp Effect Anti-Alias Sample Accumulation

**File:** `Pinta.Effects/Algorithms/Warp.cs`

**Current code:** For anti-aliased warp effects, the inner loop samples N points per pixel:
```csharp
Span<ColorBgra> samples = stackalloc ColorBgra[aaSamples];
for (int i = 0; i < aaSamples; i++) {
    // Transform sample point, get bilinear sample
    samples[i] = GetSample(settings, ...);
}
return ColorBgra.Blend(samples);
```

**Suggestion:** Pre-compute all N transform coordinates, batch the bilinear samples, and use SIMD-accelerated `Blend()` for the final color averaging. This fuses the transform+sample+blend pipeline into a vectorized operation.

**Estimated benefit:** 🟡 **Medium (1.5–2×)** — depends on the anti-alias quality setting (number of samples).

---

### E-3: Vectorize Coordinate Transformation in Distortion Effects

**Files:**
- `Pinta.Effects/Effects/BulgeEffect.cs`
- `Pinta.Effects/Effects/TwistEffect.cs`
- `Pinta.Effects/Effects/DentsEffect.cs`
- `Pinta.Effects/Effects/TileEffect.cs`

**Current code:** Each pixel's source coordinate is computed individually with floating-point math (magnitude, atan2, sin, cos, etc.).

**Suggestion:** For effects where the transform is row-coherent (e.g., Twist, Bulge), use `Vector256<float>` to compute 8 pixel transforms in parallel. Load x-coordinates as a vector (consecutive in a row), compute the transform function, and output 8 source coordinates at once. `MathF.Sin`/`MathF.Cos` can be replaced with SIMD-friendly polynomial approximations or use `System.Runtime.Intrinsics.X86.Avx.Sqrt` for vectorized sqrt.

**Estimated benefit:** 🟡 **Medium (1.3–2×)** — floating-point math is already fast, but 8-wide SIMD gives a meaningful multiplier.

---

## Category F: Histogram and Statistical Operations

### F-1: Vectorize Histogram Accumulation

**File:** `Pinta.Core/Algorithms/HistogramRGB.cs` (lines 46–65)

**Current code:**
```csharp
for (int x = 0; x < width; ++x) {
    ColorBgra col = row[x];
    ++histogramB[col.B];
    ++histogramG[col.G];
    ++histogramR[col.R];
}
```

**Suggestion:** Use a "histogram striping" technique: partition the 256 bins into sub-histograms, process 4–8 pixels per iteration, and merge at the end. Alternatively, since .NET 10 supports AVX-512 on capable hardware, use `Vector512<int>` conflict detection instructions (`vpconflictd`) available in AVX-512CD to handle histogram bin collisions. For CPUs without AVX-512, use a simpler approach: maintain 4 independent histograms (one per SIMD lane set), update them without conflicts, and merge at the end.

**Estimated benefit:** 🟡 **Medium (1.3–2×)** — histogram scatter updates are inherently hard to vectorize, but the striping technique helps for large images.

---

### F-2: Vectorize `LocalHistogram` Inner Loop

**File:** `Pinta.Effects/Algorithms/LocalHistogram.cs`

**Current code:** Maintains a sliding window histogram for median/percentile filters:
```csharp
for (int v = top; v <= bottom; ++v) {
    for (int u = left; u <= right; ++u) {
        if ((u * u + v * v) > cutoff) continue;
        ++hb[psamp.B]; ++hg[psamp.G]; ++hr[psamp.R]; ++ha[psamp.A];
    }
}
```

**Suggestion:** The sliding histogram approach already avoids full recomputation, but the "add column / remove column" operations can be vectorized. When adding a column of pixels to the histogram, load the column's pixel values, sort them into bins using SIMD scatter operations, and increment counters. The circular radius check `(u*u + v*v) > cutoff` can be pre-computed as a bitmask.

Also, consider replacing the scalar histogram with a SIMD-friendly representation using `Vector256<int>` for groups of 8 bins, allowing vectorized addition when adding/removing columns.

**Estimated benefit:** 🟡 **Medium (1.3–2×)** — the sliding histogram is already an efficient algorithm; SIMD helps with the column add/remove steps.

---

## Category G: Memory and Allocation Optimizations

### G-1: Use `ArrayPool<T>` for Temporary Buffers in Effects

**Files:** Multiple effects that allocate temporary arrays

**Current patterns:**
- `GradientRenderer.cs`: `lerp_alphas = new byte[256]; lerp_colors = new ColorBgra[256];`
- Various effects allocate temporary surfaces or arrays during `CreateSettings()`
- `DitheringEffect.cs` allocates arrays for changed pixels and colors

**Suggestion:** Replace `new T[]` allocations in frequently-called effect code with `ArrayPool<T>.Shared.Rent()` / `Return()`. This eliminates GC pressure during effect preview (which re-renders on every slider change). For small fixed-size arrays (≤256 elements), prefer `stackalloc` when possible (already used in some places). For larger arrays (pixel buffers, histogram arrays), `ArrayPool` avoids repeated allocation.

**Estimated benefit:** 🟢 **Low (1.1–1.3×)** for throughput, but **significant reduction in GC pauses** during interactive use (effect preview, slider dragging).

---

### G-2: Pre-allocate and Reuse Effect Settings Buffers

**Files:** Effect classes that create new settings objects per tile

**Current pattern:** Many effects create `Settings` record objects inside `Render()` or `CreateSettings()`, which may include arrays:
```csharp
DitheringSettings settings = CreateSettings(source, roi);
```

**Suggestion:** For effects that process multiple tiles in parallel, ensure that per-tile allocations are minimized. Consider making `Settings` objects reusable or using thread-local storage for temporary buffers. The `AsyncEffectRenderer` spawns N tasks that each process tiles sequentially — each task could own a pre-allocated working buffer.

**Estimated benefit:** 🟢 **Low (1.1–1.2×)** for throughput, but reduces GC pressure during batch rendering.

---

### G-3: Use `MemoryMarshal.CreateSpan` to Avoid Span Slicing Overhead in Tight Loops

**Files:** `BinaryPixelOp.cs`, `UnaryPixelOp.cs`, `BaseEffect.cs`

**Current code:**
```csharp
for (int row = 0; row < height; ++row) {
    Apply(dst_data.Slice((dstOffset.Y + row) * dst_width + dstOffset.X, width), ...);
}
```

**Suggestion:** Instead of computing a new `Slice()` each iteration (which involves bounds checking), use `ref` pointer arithmetic via `Unsafe.Add(ref MemoryMarshal.GetReference(span), offset)` to walk forward by the stride each iteration. This eliminates per-row bounds checking.

**Estimated benefit:** 🟢 **Low (1.05–1.15×)** — the bounds check is cheap but adds up over millions of rows. Profile first to confirm.

---

## Category H: Parallelization Improvements

### H-1: Add SIMD + Parallel Combination for Blend Operations

**File:** `Pinta.Core/Algorithms/PixelOps/BinaryPixelOp.cs`

**Current code:** Row-by-row processing in a sequential for loop (lines 72–77).

**Suggestion:** Combine SIMD with multi-threading. Use `Parallel.For` to process rows in parallel (each thread gets a row range), and within each thread, use SIMD to process 8+ pixels per iteration. This provides two levels of parallelism:
1. **Thread-level:** Different rows on different cores
2. **Data-level:** Multiple pixels per SIMD instruction

The current blend operations are not parallelized at the row level (only effects have `IsTileable` parallelism). Adding row-level parallelism to `BinaryPixelOp.Apply(ImageSurface, ImageSurface)` would provide immediate benefit for layer compositing.

**Estimated benefit:** 🔴 **Very High (Ncores × 4–8×)** for layer compositing on multi-core systems. On a 4-core system with AVX2, this could be 16–32× faster than the current scalar single-threaded path.

---

### H-2: Make More Effects Tileable

**Files:** Various effects where `IsTileable` returns `false`

**Current pattern:** Some effects return `IsTileable = false` because they modify shared state or have inter-pixel dependencies. However, some of these could be restructured:

Audit each non-tileable effect to determine if it can be made tileable with minor refactoring. Effects that only read from source and write to destination (no shared mutable state) should be tileable.

**Estimated benefit:** 🟠 **High (Ncores×)** for each newly-parallelized effect on multi-core systems, but depends on which effects are identified as candidates.

---

### H-3: Use `Parallel.For` with Row Ranges Instead of Individual Rows

**File:** `Pinta.Core/Effects/AsyncEffectRenderer.cs`

**Current code:** When tileable, each row is a separate tile queued to the concurrent queue:
```csharp
settings.EffectIsTileable
    ? settings.RenderBounds.ToRows()  // Each row is a separate work item
    : [settings.RenderBounds]
```

**Suggestion:** Instead of one work item per row, batch rows into chunks (e.g., 16–64 rows per chunk). This reduces thread synchronization overhead (fewer dequeue operations on the `ConcurrentQueue`) and improves cache locality (each thread processes a contiguous block of rows). The optimal chunk size can be tuned or computed as `totalRows / (threadCount * 4)`.

**Estimated benefit:** 🟢 **Low (1.1–1.3×)** — reduces scheduling overhead, most beneficial for small effects (previews) where per-tile overhead is proportionally large.

---

## Category I: Algorithm-Level Improvements

### I-1: Approximate `MathF.Sqrt` with SIMD Fast Inverse Square Root

**Files:**
- `Pinta.Effects/Effects/FeatherEffect.cs`
- `Pinta.Effects/Effects/OutlineObjectEffect.cs`

**Current code:**
```csharp
float distance = MathF.Sqrt(dx * dx + dy * dy);
```

**Suggestion:** Use `Avx.ReciprocalSqrt()` (vrsqrtps) followed by a reciprocal to get an approximate sqrt, or use `Avx.Sqrt()` (vsqrtps) for exact vectorized sqrt. Process 8 distance calculations per SIMD operation. Pre-compute the `dx * dx` term for the row (y is constant within a row), then vectorize `dx * dx + dy_squared` and `vsqrtps` across 8 consecutive x values.

**Estimated benefit:** 🟡 **Medium (1.5–2×)** for Feather and Outline Object effects specifically. These effects spend significant time in the distance calculation loop.

---

### I-2: Use Bit-Manipulation Instructions for `ClampToByte`

**File:** `Pinta.Core/Algorithms/Utility.cs`

**Current code:**
```csharp
public static byte ClampToByte(int x)
    => (byte)Math.Clamp(x, byte.MinValue, byte.MaxValue);
```

**Suggestion:** When processing in SIMD, use `Avx2.PackSignedSaturate` to narrow `Vector256<int>` → `Vector256<short>` → `Vector128<byte>` with automatic clamping. This eliminates all explicit clamp operations in the blend pipeline, as the narrowing instruction handles clamping for free. This is a key building block for making A-1 and A-2 efficient.

**Estimated benefit:** 🟡 **Medium** — not a standalone improvement, but enables 🔴 **Very High** speedups when combined with A-1/A-2 by eliminating per-channel clamping overhead.

---

### I-3: Replace `FastDivideShortByByte` MAS Table Lookup with SIMD Multiply-High

**File:** `Pinta.Core/Algorithms/Utility.cs` (lines 138–150)

**Current code:**
```csharp
public static int FastDivideShortByByte(ushort n, byte d)
{
    int i = d * 3;
    uint m = mas_table[i];
    uint a = mas_table[i + 1];
    uint s = mas_table[i + 2];
    uint nTimesMPlusA = unchecked((n * m) + a);
    uint shifted = nTimesMPlusA >> (int)s;
    return (int)shifted;
}
```

**Suggestion:** In the SIMD blend path, replace the MAS table lookup (which is inherently scalar due to the table lookup) with `Avx2.MultiplyHigh` for the common case of dividing by 255. The expression `(x + 128) / 255` can be approximated as `(x + 128 + ((x + 128) >> 8)) >> 8`, which requires only SIMD add and shift operations — no lookup table needed.

For division by other values (used in ColorDodge, Overlay), use SIMD floating-point division via `Avx.Divide` on `Vector256<float>`, which is fast on modern CPUs.

**Estimated benefit:** 🟡 **Medium (1.3–2×)** for blend modes that use the MAS table. Eliminates the random-access LUT pattern that can cause cache misses.

---

### I-4: Optimize Dithering Error Diffusion with SIMD

**File:** `Pinta.Effects/Effects/DitheringEffect.cs`

**Current code:** Error diffusion applies a matrix of error corrections to neighboring pixels in a nested loop.

**Suggestion:** The error diffusion matrix application (e.g., Floyd-Steinberg 2×3, Stucki 3×5) can be partially vectorized. The error values for a row of pixels can be stored in a `Vector256<float>` or `Vector256<int>`, and the matrix coefficients broadcast and multiplied simultaneously. The key challenge is the sequential dependency (each pixel depends on errors from previously processed pixels), but the distribution to multiple neighbors can be vectorized.

For ordered dithering alternatives, the entire operation becomes embarrassingly parallel and fully vectorizable.

**Estimated benefit:** 🟢 **Low (1.1–1.3×)** for error diffusion (sequential dependency limits parallelism). 🟠 **High (2–4×)** if an ordered dithering option were added as an alternative.

---

## Category J: Data Structure Optimizations

### J-1: Consider Structure-of-Arrays (SoA) Layout for Batch Processing

**Current design:** `ColorBgra` is Array-of-Structures (AoS): `[B0,G0,R0,A0, B1,G1,R1,A1, ...]`

**Suggestion:** For SIMD processing, a Structure-of-Arrays layout (`[B0,B1,B2,...], [G0,G1,G2,...], [R0,R1,R2,...], [A0,A1,A2,...]`) is often more efficient because each SIMD load gets 16–32 values of the same channel. This avoids the need for shuffle/deinterleave operations.

However, this would be a very invasive change to Pinta's architecture since `ColorBgra` is the fundamental pixel type and Cairo expects AoS layout. Instead, **use SoA only as a local transformation within SIMD processing loops**: deinterleave AoS → SoA at the start of a SIMD batch, process in SoA form, then interleave SoA → AoS at the end. The deinterleave/interleave cost is amortized over the batch size.

**Estimated benefit:** 🟡 **Medium (1.3–2×)** improvement to SIMD throughput within processing loops, at the cost of deinterleave/interleave overhead. Most beneficial for operations that process all 4 channels identically (blend, lerp, brightness).

---

### J-2: Use `Vector128<byte>` Directly as a 4-Pixel Batch Type

**Suggestion:** Since `ColorBgra` is 4 bytes, a `Vector128<byte>` holds exactly 4 pixels (16 bytes). Create a helper type `PixelBatch4` backed by `Vector128<byte>` that supports:
- Load/store from `Span<ColorBgra>`
- Per-channel operations via shuffle masks
- Byte-level saturating arithmetic (`AddSaturate`, `SubtractSaturate`)
- Widening to `Vector128<ushort>` for multiply operations

This would serve as the building block for all SIMD pixel operations and provide a clean API boundary between scalar and vectorized code.

**Estimated benefit:** 🟡 **Medium** — primarily an architectural suggestion that enables cleaner SIMD implementations for all of Category A and Category D suggestions.

---

## Category K: Miscellaneous Optimizations

### K-1: Use `Vector256.LoadUnsafe` / `Vector256.StoreUnsafe` for Pixel Buffer Access

**Suggestion:** When implementing any SIMD optimization, use the .NET 10 `Vector256.LoadUnsafe(ref source, offset)` and `StoreUnsafe()` APIs rather than manually pinning memory. These APIs generate optimal load/store instructions and work directly with `Span<T>` data via `MemoryMarshal.GetReference()`.

**Estimated benefit:** 🟢 **Low** — enables other optimizations rather than being a standalone improvement.

---

### K-2: Profile and Optimize Bilinear Sampling Cache Access Pattern

**File:** `Pinta.Core/Extensions/Cairo/CairoExtensions.Samples.cs`

**Current code:** Bilinear sampling fetches 4 pixels at positions `(x, y)`, `(x+1, y)`, `(x, y+1)`, `(x+1, y+1)`, which involves two different cache lines for the vertical neighbors.

**Suggestion:** For warp effects that process output pixels left-to-right, the two left samples `(x, y)` and `(x, y+1)` from the current pixel become the right samples of the previous pixel. Cache the "right column" samples and reuse them as the "left column" for the next pixel. This cuts memory fetches in half for sequential bilinear sampling.

**Estimated benefit:** 🟢 **Low (1.1–1.2×)** for distortion effects — depends on how cache-friendly the access pattern already is.

---

### K-3: Use `Vector.IsHardwareAccelerated` for Runtime SIMD Detection

**Suggestion:** When implementing SIMD optimizations, use runtime capability checks:
```csharp
if (Avx2.IsSupported) { /* AVX2 path */ }
else if (Sse2.IsSupported) { /* SSE2 path */ }
else { /* Scalar fallback */ }
```
.NET's JIT will eliminate dead branches at runtime, so there is zero overhead from the checks. This ensures Pinta works on all platforms (including ARM via `AdvSimd`) while using the best available SIMD width.

**Estimated benefit:** 🟢 **Low** — architectural guidance, not a direct performance improvement.

---

### K-4: Vectorize the `Utility.FastScaleByteByByte` Pattern

**File:** `Pinta.Core/Algorithms/Utility.cs`

**Current code:**
```csharp
public static byte FastScaleByteByByte(byte a, byte b)
{
    int r1 = a * b + 0x80;
    int r2 = ((r1 >> 8) + r1) >> 8;
    return (byte)r2;
}
```

**Suggestion:** This pattern (approximate `a * b / 255`) appears throughout the blend pipeline. In SIMD, use `Avx2.MultiplyHigh` on `Vector256<ushort>` which computes the high 16 bits of a 16×16 multiplication — effectively computing `a * b >> 16` in one instruction. For 8-bit precision, widen bytes to shorts, multiply-high, and narrow back. This replaces the multi-instruction scalar sequence with a single SIMD instruction.

**Estimated benefit:** 🟡 **Medium** — part of the broader blend optimization (A-1/A-2) rather than standalone, but it's the key instruction that makes SIMD blending fast.

---

## Summary Table

| ID | Category | Suggestion | Estimated Benefit | Complexity |
|----|----------|-----------|-------------------|------------|
| **A-1** | Blend Ops | Vectorize `BinaryPixelOp.Apply` batch loop | 🔴 Very High (4–8×) | High |
| **A-2** | Blend Ops | Vectorize `BlendOpHelper.ComputePremultiplied` batch | 🔴 Very High (4–8×) | High |
| **A-3** | Blend Ops | SIMD early-exit batching for Normal blend | 🟠 High (2–4×) | Medium |
| **A-4** | Blend Ops | Vectorize `UnaryPixelOp.Apply` for trivial ops | 🔴 Very High (4–8×) | Medium |
| **B-1** | Color Ops | Vectorize `ColorBgra.Lerp` batch | 🟠 High (2–4×) | Medium |
| **B-2** | Color Ops | Vectorize `ColorBgra.Blend` aggregation | 🟡 Medium (1.5–2×) | Low |
| **B-3** | Color Ops | Vectorize premultiplied alpha conversions | 🟠 High (2–4×) | Medium |
| **B-4** | Color Ops | Vectorize `GetIntensityByte` bulk conversion | 🟡 Medium (1.5–2×) | Low |
| **C-1** | Blur | Separable Gaussian blur + SIMD accumulation | 🔴 Very High (4–10×) | High |
| **C-2** | Convolution | SIMD convolution kernel helper | 🟠 High (2–4×) | Medium |
| **C-3** | Blur | Vectorize Motion Blur accumulation | 🟡 Medium (1.5–2×) | Medium |
| **C-4** | Blur | Vectorize Radial/Zoom Blur accumulation | 🟡 Medium (1.5–2×) | Medium |
| **D-1** | Adjustments | Vectorize Brightness/Contrast | 🟠 High (2–4×) | Medium |
| **D-2** | Adjustments | Vectorize Levels and Curves | 🟡 Medium (1.5–2×) | Medium |
| **D-3** | Adjustments | Vectorize Posterize | 🟠 High (2–4×) | Low |
| **D-4** | Adjustments | Vectorize Sepia (fused pipeline) | 🟡 Medium (1.5–2×) | Medium |
| **D-5** | Adjustments | Vectorize Add Noise with saturating add | 🟠 High (2–4×) | Low |
| **D-6** | Adjustments | SIMD sRGB conversion for Vignette | 🟡 Medium (1.3–2×) | Medium |
| **E-1** | Distortion | Vectorize bilinear sampling batch | 🟠 High (2–3×) | High |
| **E-2** | Distortion | Vectorize Warp anti-alias accumulation | 🟡 Medium (1.5–2×) | Medium |
| **E-3** | Distortion | Vectorize coordinate transforms | 🟡 Medium (1.3–2×) | Medium |
| **F-1** | Histogram | Vectorize histogram accumulation | 🟡 Medium (1.3–2×) | Medium |
| **F-2** | Histogram | Vectorize LocalHistogram sliding window | 🟡 Medium (1.3–2×) | High |
| **G-1** | Memory | ArrayPool for temporary buffers | 🟢 Low (GC benefit) | Low |
| **G-2** | Memory | Reuse effect settings buffers | 🟢 Low (GC benefit) | Low |
| **G-3** | Memory | `Unsafe.Add` instead of `Slice` in tight loops | 🟢 Low (1.05–1.15×) | Low |
| **H-1** | Parallelism | SIMD + Parallel for blend operations | 🔴 Very High (Ncores×4–8×) | High |
| **H-2** | Parallelism | Audit and enable more tileable effects | 🟠 High (Ncores×) | Varies |
| **H-3** | Parallelism | Batch rows in AsyncEffectRenderer | 🟢 Low (1.1–1.3×) | Low |
| **I-1** | Algorithms | SIMD fast sqrt for distance calculations | 🟡 Medium (1.5–2×) | Low |
| **I-2** | Algorithms | SIMD saturating pack for ClampToByte | 🟡 Medium (enabler) | Low |
| **I-3** | Algorithms | Replace MAS table with SIMD multiply-high | 🟡 Medium (1.3–2×) | Medium |
| **I-4** | Algorithms | Vectorize dithering error diffusion | 🟢 Low (1.1–1.3×) | High |
| **J-1** | Data Layout | Local SoA transformation for SIMD loops | 🟡 Medium (1.3–2×) | Medium |
| **J-2** | Data Layout | `Vector128<byte>` as 4-pixel batch type | 🟡 Medium (enabler) | Medium |
| **K-1** | Infrastructure | Use `Vector256.LoadUnsafe`/`StoreUnsafe` | 🟢 Low (enabler) | Low |
| **K-2** | Infrastructure | Cache bilinear sampling neighbors | 🟢 Low (1.1–1.2×) | Low |
| **K-3** | Infrastructure | Runtime SIMD detection with fallbacks | 🟢 Low (enabler) | Low |
| **K-4** | Infrastructure | Vectorize `FastScaleByteByByte` pattern | 🟡 Medium (enabler) | Low |

## Recommended Priority Order

If pursuing these improvements, I recommend the following order based on **impact × feasibility**:

1. **A-1 + A-2 + I-2 + K-4** — Vectorize the core blend pipeline. This is the single highest-impact change as it affects all layer compositing. Start with Normal blend, then extend to other modes.
2. **H-1** — Add `Parallel.For` to the blend pipeline for multi-core compositing.
3. **C-1** — Separable Gaussian blur. The algorithmic + SIMD improvement is huge.
4. **A-4** — Vectorize trivial unary ops (Invert, SetAlpha). Quick wins with massive speedup.
5. **D-3 + D-5** — Vectorize Posterize and Add Noise. Simple, high impact.
6. **B-3** — Vectorize alpha conversions (enables many effects to be faster).
7. **D-1** — Vectorize Brightness/Contrast with direct SIMD arithmetic.
8. **E-1** — Vectorize bilinear sampling (enables all distortion effects to be faster).
9. **G-1** — ArrayPool usage (low effort, reduces GC pauses).
10. Everything else based on profiling data from benchmarks.
