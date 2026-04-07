using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Pinta.Core;

/// <summary>
/// A batch of 8 BGRA pixels backed by a <see cref="Vector256{T}"/>.
/// Since <see cref="ColorBgra"/> is 4 bytes, a <c>Vector256&lt;byte&gt;</c> holds exactly 8 pixels (32 bytes).
/// Provides SIMD-friendly operations for pixel processing.
/// </summary>
internal readonly struct PixelBatch8
{
	/// <summary>
	/// The underlying SIMD vector containing 8 packed BGRA pixels.
	/// Layout: [B0,G0,R0,A0, B1,G1,R1,A1, ... B7,G7,R7,A7]
	/// </summary>
	public readonly Vector256<byte> Data;

	/// <summary>
	/// The number of pixels in a batch.
	/// </summary>
	public const int Count = 8;

	// --- Shuffle masks for per-channel broadcast ---
	// Same per-lane mask pattern as PixelBatch4, applied to both 128-bit lanes.
	// On x86/AVX2, vpshufb operates per-lane, so indices > 15 in the upper lane
	// correctly map to lane-local offsets since only the low 4 bits are used.

	private static readonly Vector256<byte> alpha_shuffle_mask = Vector256.Create (
		(byte) 3, 3, 3, 3, 7, 7, 7, 7, 11, 11, 11, 11, 15, 15, 15, 15,
		19, 19, 19, 19, 23, 23, 23, 23, 27, 27, 27, 27, 31, 31, 31, 31);

	private static readonly Vector256<byte> blue_shuffle_mask = Vector256.Create (
		(byte) 0, 0, 0, 0, 4, 4, 4, 4, 8, 8, 8, 8, 12, 12, 12, 12,
		16, 16, 16, 16, 20, 20, 20, 20, 24, 24, 24, 24, 28, 28, 28, 28);

	private static readonly Vector256<byte> green_shuffle_mask = Vector256.Create (
		(byte) 1, 1, 1, 1, 5, 5, 5, 5, 9, 9, 9, 9, 13, 13, 13, 13,
		17, 17, 17, 17, 21, 21, 21, 21, 25, 25, 25, 25, 29, 29, 29, 29);

	private static readonly Vector256<byte> red_shuffle_mask = Vector256.Create (
		(byte) 2, 2, 2, 2, 6, 6, 6, 6, 10, 10, 10, 10, 14, 14, 14, 14,
		18, 18, 18, 18, 22, 22, 22, 22, 26, 26, 26, 26, 30, 30, 30, 30);

	// Mask to isolate the alpha channel byte in each pixel: [0x00, 0x00, 0x00, 0xFF] repeated
	private static readonly Vector256<byte> alpha_only_mask = Vector256.Create (
		(byte) 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00, 0xFF,
		0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00, 0xFF,
		0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00, 0xFF,
		0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00, 0xFF);

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public PixelBatch8 (Vector256<byte> data)
	{
		Data = data;
	}

	// --- Load / Store ---

	/// <summary>
	/// Loads 8 pixels from a <see cref="ReadOnlySpan{ColorBgra}"/> into a <see cref="PixelBatch8"/>.
	/// The span must contain at least 8 elements.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch8 Load (ReadOnlySpan<ColorBgra> pixels)
	{
		ref byte src = ref Unsafe.As<ColorBgra, byte> (
			ref MemoryMarshal.GetReference (pixels));
		return new PixelBatch8 (Vector256.LoadUnsafe (ref src));
	}

	/// <summary>
	/// Loads 8 pixels starting at the given offset from a reference to a <see cref="ColorBgra"/>.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch8 LoadUnsafe (ref readonly ColorBgra source, nuint offset = 0)
	{
		return new PixelBatch8 (Vector256.LoadUnsafe (
			in Unsafe.As<ColorBgra, byte> (ref Unsafe.Add (ref Unsafe.AsRef (in source), offset))));
	}

	/// <summary>
	/// Stores 8 pixels to a <see cref="Span{ColorBgra}"/>.
	/// The span must contain at least 8 elements.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly void Store (Span<ColorBgra> pixels)
	{
		ref byte dst = ref Unsafe.As<ColorBgra, byte> (
			ref MemoryMarshal.GetReference (pixels));
		Data.StoreUnsafe (ref dst);
	}

	/// <summary>
	/// Stores 8 pixels starting at the given offset from a reference to a <see cref="ColorBgra"/>.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly void StoreUnsafe (ref ColorBgra destination, nuint offset = 0)
	{
		Data.StoreUnsafe (ref Unsafe.As<ColorBgra, byte> (ref Unsafe.Add (ref destination, offset)));
	}

	// --- Per-Channel Broadcast ---

	/// <summary>
	/// Broadcasts each pixel's alpha value to all 4 byte positions within that pixel.
	/// Result: [A0,A0,A0,A0, A1,A1,A1,A1, ... A7,A7,A7,A7]
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly Vector256<byte> BroadcastAlpha ()
		=> Vector256.Shuffle (Data, alpha_shuffle_mask);

	/// <summary>
	/// Broadcasts each pixel's blue value to all 4 byte positions within that pixel.
	/// Result: [B0,B0,B0,B0, B1,B1,B1,B1, ... B7,B7,B7,B7]
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly Vector256<byte> BroadcastBlue ()
		=> Vector256.Shuffle (Data, blue_shuffle_mask);

	/// <summary>
	/// Broadcasts each pixel's green value to all 4 byte positions within that pixel.
	/// Result: [G0,G0,G0,G0, G1,G1,G1,G1, ... G7,G7,G7,G7]
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly Vector256<byte> BroadcastGreen ()
		=> Vector256.Shuffle (Data, green_shuffle_mask);

	/// <summary>
	/// Broadcasts each pixel's red value to all 4 byte positions within that pixel.
	/// Result: [R0,R0,R0,R0, R1,R1,R1,R1, ... R7,R7,R7,R7]
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly Vector256<byte> BroadcastRed ()
		=> Vector256.Shuffle (Data, red_shuffle_mask);

	// --- Saturating Arithmetic ---

	/// <summary>
	/// Adds two pixel batches with unsigned byte saturation (clamped to [0, 255]).
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch8 AddSaturate (in PixelBatch8 a, in PixelBatch8 b)
		=> new (Vector256.AddSaturate (a.Data, b.Data));

	/// <summary>
	/// Subtracts two pixel batches with unsigned byte saturation (clamped to [0, 255]).
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch8 SubtractSaturate (in PixelBatch8 a, in PixelBatch8 b)
		=> new (Vector256.SubtractSaturate (a.Data, b.Data));

	// --- Widening to ushort ---

	/// <summary>
	/// Widens the lower 16 bytes (pixels 0–3) from byte to <see cref="Vector256{UInt16}"/>.
	/// Useful for multiply operations that need 16-bit intermediate precision.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly Vector256<ushort> WidenLow ()
		=> Vector256.WidenLower (Data);

	/// <summary>
	/// Widens the upper 16 bytes (pixels 4–7) from byte to <see cref="Vector256{UInt16}"/>.
	/// Useful for multiply operations that need 16-bit intermediate precision.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly Vector256<ushort> WidenHigh ()
		=> Vector256.WidenUpper (Data);

	/// <summary>
	/// Narrows two <see cref="Vector256{UInt16}"/> (pixels 0–3 and pixels 4–7) back to bytes,
	/// clamping values above 255.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch8 NarrowToBytes (Vector256<ushort> low, Vector256<ushort> high)
	{
		// Clamp to byte range, then narrow via truncation (safe since values are ≤ 255).
		var clampedLow = Vector256.Min (low, Vector256.Create ((ushort) 255));
		var clampedHigh = Vector256.Min (high, Vector256.Create ((ushort) 255));
		return new PixelBatch8 (Vector256.Narrow (clampedLow, clampedHigh));
	}

	// --- DivBy255 ---

	/// <summary>
	/// Computes <c>floor(x / 255)</c> for each <see cref="ushort"/> element.
	/// Uses the exact identity: <c>(x + 1 + ((x + 1) &gt;&gt; 8)) &gt;&gt; 8</c>.
	/// Exact for x in [0, 65278].
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static Vector256<ushort> DivBy255 (Vector256<ushort> x)
	{
		// (x + 1 + ((x + 1) >> 8)) >> 8
		var xPlus1 = x + Vector256.Create ((ushort) 1);
		return Vector256.ShiftRightLogical (xPlus1 + Vector256.ShiftRightLogical (xPlus1, 8), 8);
	}

	/// <summary>
	/// Computes <c>floor((x + 128) / 255)</c> for each <see cref="ushort"/> element.
	/// This matches the rounding behavior of <c>(value + 128) / 255</c> used by
	/// <see cref="BlendOpHelper.ComputePremultiplied{TChannelBlend}"/>.
	/// Exact for x in [0, 65150]. For byte×byte products (max 65025), this is always exact.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static Vector256<ushort> DivBy255Rounded (Vector256<ushort> x)
	{
		// Apply DivBy255 to (x + 128):
		// Let n = x + 128, then floor(n / 255) = (n + 1 + ((n + 1) >> 8)) >> 8
		// = (x + 129 + ((x + 129) >> 8)) >> 8
		var n = x + Vector256.Create ((ushort) 129);
		return Vector256.ShiftRightLogical (n + Vector256.ShiftRightLogical (n, 8), 8);
	}

	// --- Bitwise Operations ---

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch8 operator & (in PixelBatch8 a, in PixelBatch8 b)
		=> new (a.Data & b.Data);

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch8 operator | (in PixelBatch8 a, in PixelBatch8 b)
		=> new (a.Data | b.Data);

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch8 operator ^ (in PixelBatch8 a, in PixelBatch8 b)
		=> new (a.Data ^ b.Data);

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch8 operator ~ (in PixelBatch8 a)
		=> new (~a.Data);

	// --- Min / Max ---

	/// <summary>
	/// Returns the element-wise minimum of two pixel batches.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch8 Min (in PixelBatch8 a, in PixelBatch8 b)
		=> new (Vector256.Min (a.Data, b.Data));

	/// <summary>
	/// Returns the element-wise maximum of two pixel batches.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch8 Max (in PixelBatch8 a, in PixelBatch8 b)
		=> new (Vector256.Max (a.Data, b.Data));

	// --- Conditional Select ---

	/// <summary>
	/// Selects bytes from <paramref name="ifTrue"/> or <paramref name="ifFalse"/> based on the <paramref name="mask"/>.
	/// Where mask byte is 0xFF, selects from ifTrue; where 0x00, selects from ifFalse.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch8 ConditionalSelect (Vector256<byte> mask, in PixelBatch8 ifTrue, in PixelBatch8 ifFalse)
		=> new (Vector256.ConditionalSelect (mask, ifTrue.Data, ifFalse.Data));

	// --- Helpers ---

	/// <summary>
	/// Creates a batch with all bytes set to the same value.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch8 Create (byte value)
		=> new (Vector256.Create (value));

	/// <summary>
	/// A batch of 8 fully transparent black pixels (all zero bytes).
	/// </summary>
	public static PixelBatch8 Zero => new (Vector256<byte>.Zero);

	/// <summary>
	/// Returns true if all 8 pixels in the batch have alpha == 0.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly bool AllTransparent ()
		=> (Data & alpha_only_mask) == Vector256<byte>.Zero;

	/// <summary>
	/// Returns true if all 8 pixels in the batch have alpha == 255.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly bool AllOpaque ()
		=> (Data & alpha_only_mask) == alpha_only_mask;

	/// <summary>
	/// Returns true if any pixel in the batch has a non-zero alpha.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly bool AnyVisible ()
		=> (Data & alpha_only_mask) != Vector256<byte>.Zero;

	// --- Conversion to/from PixelBatch4 ---

	/// <summary>
	/// Extracts the lower 4 pixels (pixels 0–3) as a <see cref="PixelBatch4"/>.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly PixelBatch4 GetLower ()
		=> new (Data.GetLower ());

	/// <summary>
	/// Extracts the upper 4 pixels (pixels 4–7) as a <see cref="PixelBatch4"/>.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly PixelBatch4 GetUpper ()
		=> new (Data.GetUpper ());

	/// <summary>
	/// Combines two <see cref="PixelBatch4"/> batches into a single <see cref="PixelBatch8"/>.
	/// The <paramref name="lower"/> batch becomes pixels 0–3 and <paramref name="upper"/> becomes pixels 4–7.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch8 Create (in PixelBatch4 lower, in PixelBatch4 upper)
		=> new (Vector256.Create (lower.Data, upper.Data));
}
