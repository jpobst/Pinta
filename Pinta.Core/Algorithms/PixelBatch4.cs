using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Pinta.Core;

/// <summary>
/// A batch of 4 BGRA pixels backed by a <see cref="Vector128{T}"/>.
/// Since <see cref="ColorBgra"/> is 4 bytes, a <c>Vector128&lt;byte&gt;</c> holds exactly 4 pixels (16 bytes).
/// Provides SIMD-friendly operations for pixel processing.
/// </summary>
internal readonly struct PixelBatch4
{
	/// <summary>
	/// The underlying SIMD vector containing 4 packed BGRA pixels.
	/// Layout: [B0,G0,R0,A0, B1,G1,R1,A1, B2,G2,R2,A2, B3,G3,R3,A3]
	/// </summary>
	public readonly Vector128<byte> Data;

	/// <summary>
	/// The number of pixels in a batch.
	/// </summary>
	public const int Count = 4;

	// --- Shuffle masks for per-channel broadcast ---
	// Each mask broadcasts a specific channel's byte to all 4 byte positions within each pixel.

	private static readonly Vector128<byte> alpha_shuffle_mask = Vector128.Create (
		(byte) 3, 3, 3, 3, 7, 7, 7, 7, 11, 11, 11, 11, 15, 15, 15, 15);

	private static readonly Vector128<byte> blue_shuffle_mask = Vector128.Create (
		(byte) 0, 0, 0, 0, 4, 4, 4, 4, 8, 8, 8, 8, 12, 12, 12, 12);

	private static readonly Vector128<byte> green_shuffle_mask = Vector128.Create (
		(byte) 1, 1, 1, 1, 5, 5, 5, 5, 9, 9, 9, 9, 13, 13, 13, 13);

	private static readonly Vector128<byte> red_shuffle_mask = Vector128.Create (
		(byte) 2, 2, 2, 2, 6, 6, 6, 6, 10, 10, 10, 10, 14, 14, 14, 14);

	// Mask to isolate the alpha channel byte in each pixel: [0x00, 0x00, 0x00, 0xFF] repeated
	private static readonly Vector128<byte> alpha_only_mask = Vector128.Create (
		(byte) 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00, 0xFF,
		0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00, 0xFF);

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public PixelBatch4 (Vector128<byte> data)
	{
		Data = data;
	}

	// --- Load / Store ---

	/// <summary>
	/// Loads 4 pixels from a <see cref="ReadOnlySpan{ColorBgra}"/> into a <see cref="PixelBatch4"/>.
	/// The span must contain at least 4 elements.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch4 Load (ReadOnlySpan<ColorBgra> pixels)
	{
		ref byte src = ref Unsafe.As<ColorBgra, byte> (
			ref MemoryMarshal.GetReference (pixels));
		return new PixelBatch4 (Vector128.LoadUnsafe (ref src));
	}

	/// <summary>
	/// Loads 4 pixels starting at the given offset from a reference to a <see cref="ColorBgra"/>.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch4 LoadUnsafe (ref readonly ColorBgra source, nuint offset = 0)
	{
		return new PixelBatch4 (Vector128.LoadUnsafe (
			in Unsafe.As<ColorBgra, byte> (ref Unsafe.Add (ref Unsafe.AsRef (in source), offset))));
	}

	/// <summary>
	/// Stores 4 pixels to a <see cref="Span{ColorBgra}"/>.
	/// The span must contain at least 4 elements.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly void Store (Span<ColorBgra> pixels)
	{
		ref byte dst = ref Unsafe.As<ColorBgra, byte> (
			ref MemoryMarshal.GetReference (pixels));
		Data.StoreUnsafe (ref dst);
	}

	/// <summary>
	/// Stores 4 pixels starting at the given offset from a reference to a <see cref="ColorBgra"/>.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly void StoreUnsafe (ref ColorBgra destination, nuint offset = 0)
	{
		Data.StoreUnsafe (ref Unsafe.As<ColorBgra, byte> (ref Unsafe.Add (ref destination, offset)));
	}

	// --- Per-Channel Broadcast ---

	/// <summary>
	/// Broadcasts each pixel's alpha value to all 4 byte positions within that pixel.
	/// Result: [A0,A0,A0,A0, A1,A1,A1,A1, A2,A2,A2,A2, A3,A3,A3,A3]
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly Vector128<byte> BroadcastAlpha ()
		=> Vector128.Shuffle (Data, alpha_shuffle_mask);

	/// <summary>
	/// Broadcasts each pixel's blue value to all 4 byte positions within that pixel.
	/// Result: [B0,B0,B0,B0, B1,B1,B1,B1, B2,B2,B2,B2, B3,B3,B3,B3]
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly Vector128<byte> BroadcastBlue ()
		=> Vector128.Shuffle (Data, blue_shuffle_mask);

	/// <summary>
	/// Broadcasts each pixel's green value to all 4 byte positions within that pixel.
	/// Result: [G0,G0,G0,G0, G1,G1,G1,G1, G2,G2,G2,G2, G3,G3,G3,G3]
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly Vector128<byte> BroadcastGreen ()
		=> Vector128.Shuffle (Data, green_shuffle_mask);

	/// <summary>
	/// Broadcasts each pixel's red value to all 4 byte positions within that pixel.
	/// Result: [R0,R0,R0,R0, R1,R1,R1,R1, R2,R2,R2,R2, R3,R3,R3,R3]
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly Vector128<byte> BroadcastRed ()
		=> Vector128.Shuffle (Data, red_shuffle_mask);

	// --- Saturating Arithmetic ---

	/// <summary>
	/// Adds two pixel batches with unsigned byte saturation (clamped to [0, 255]).
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch4 AddSaturate (in PixelBatch4 a, in PixelBatch4 b)
		=> new (Vector128.AddSaturate (a.Data, b.Data));

	/// <summary>
	/// Subtracts two pixel batches with unsigned byte saturation (clamped to [0, 255]).
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch4 SubtractSaturate (in PixelBatch4 a, in PixelBatch4 b)
		=> new (Vector128.SubtractSaturate (a.Data, b.Data));

	// --- Widening to ushort ---

	/// <summary>
	/// Widens the lower 8 bytes (pixels 0 and 1) from byte to <see cref="Vector128{UInt16}"/>.
	/// Useful for multiply operations that need 16-bit intermediate precision.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly Vector128<ushort> WidenLow ()
		=> Vector128.WidenLower (Data);

	/// <summary>
	/// Widens the upper 8 bytes (pixels 2 and 3) from byte to <see cref="Vector128{UInt16}"/>.
	/// Useful for multiply operations that need 16-bit intermediate precision.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly Vector128<ushort> WidenHigh ()
		=> Vector128.WidenUpper (Data);

	/// <summary>
	/// Narrows two <see cref="Vector128{UInt16}"/> (pixels 0-1 and pixels 2-3) back to bytes,
	/// clamping values above 255.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch4 NarrowToBytes (Vector128<ushort> low, Vector128<ushort> high)
	{
		// Clamp to byte range, then narrow via truncation (safe since values are ≤ 255).
		var clampedLow = Vector128.Min (low, Vector128.Create ((ushort) 255));
		var clampedHigh = Vector128.Min (high, Vector128.Create ((ushort) 255));
		return new PixelBatch4 (Vector128.Narrow (clampedLow, clampedHigh));
	}

	// --- DivBy255 ---

	/// <summary>
	/// Computes <c>floor(x / 255)</c> for each <see cref="ushort"/> element.
	/// Uses the exact identity: <c>(x + 1 + ((x + 1) &gt;&gt; 8)) &gt;&gt; 8</c>.
	/// Exact for x in [0, 65278].
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static Vector128<ushort> DivBy255 (Vector128<ushort> x)
	{
		// (x + 1 + ((x + 1) >> 8)) >> 8
		var xPlus1 = x + Vector128.Create ((ushort) 1);
		return Vector128.ShiftRightLogical (xPlus1 + Vector128.ShiftRightLogical (xPlus1, 8), 8);
	}

	/// <summary>
	/// Computes <c>floor((x + 128) / 255)</c> for each <see cref="ushort"/> element.
	/// This matches the rounding behavior of <c>(value + 128) / 255</c> used by
	/// <see cref="BlendOpHelper.ComputePremultiplied{TChannelBlend}"/>.
	/// Exact for x in [0, 65150]. For byte×byte products (max 65025), this is always exact.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static Vector128<ushort> DivBy255Rounded (Vector128<ushort> x)
	{
		// Apply DivBy255 to (x + 128):
		// Let n = x + 128, then floor(n / 255) = (n + 1 + ((n + 1) >> 8)) >> 8
		// = (x + 129 + ((x + 129) >> 8)) >> 8
		var n = x + Vector128.Create ((ushort) 129);
		return Vector128.ShiftRightLogical (n + Vector128.ShiftRightLogical (n, 8), 8);
	}

	// --- Bitwise Operations ---

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch4 operator & (in PixelBatch4 a, in PixelBatch4 b)
		=> new (a.Data & b.Data);

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch4 operator | (in PixelBatch4 a, in PixelBatch4 b)
		=> new (a.Data | b.Data);

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch4 operator ^ (in PixelBatch4 a, in PixelBatch4 b)
		=> new (a.Data ^ b.Data);

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch4 operator ~ (in PixelBatch4 a)
		=> new (~a.Data);

	// --- Min / Max ---

	/// <summary>
	/// Returns the element-wise minimum of two pixel batches.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch4 Min (in PixelBatch4 a, in PixelBatch4 b)
		=> new (Vector128.Min (a.Data, b.Data));

	/// <summary>
	/// Returns the element-wise maximum of two pixel batches.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch4 Max (in PixelBatch4 a, in PixelBatch4 b)
		=> new (Vector128.Max (a.Data, b.Data));

	// --- Conditional Select ---

	/// <summary>
	/// Selects bytes from <paramref name="ifTrue"/> or <paramref name="ifFalse"/> based on the <paramref name="mask"/>.
	/// Where mask byte is 0xFF, selects from ifTrue; where 0x00, selects from ifFalse.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch4 ConditionalSelect (Vector128<byte> mask, in PixelBatch4 ifTrue, in PixelBatch4 ifFalse)
		=> new (Vector128.ConditionalSelect (mask, ifTrue.Data, ifFalse.Data));

	// --- Helpers ---

	/// <summary>
	/// Creates a batch with all bytes set to the same value.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch4 Create (byte value)
		=> new (Vector128.Create (value));

	/// <summary>
	/// A batch of 4 fully transparent black pixels (all zero bytes).
	/// </summary>
	public static PixelBatch4 Zero => new (Vector128<byte>.Zero);

	/// <summary>
	/// Returns true if all 4 pixels in the batch have alpha == 0.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly bool AllTransparent ()
		=> (Data & alpha_only_mask) == Vector128<byte>.Zero;

	/// <summary>
	/// Returns true if all 4 pixels in the batch have alpha == 255.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly bool AllOpaque ()
		=> (Data & alpha_only_mask) == alpha_only_mask;

	/// <summary>
	/// Returns true if any pixel in the batch has a non-zero alpha.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly bool AnyVisible ()
		=> (Data & alpha_only_mask) != Vector128<byte>.Zero;
}
