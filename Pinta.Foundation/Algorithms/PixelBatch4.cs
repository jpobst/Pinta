using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Pinta.Foundation;

/// <summary>
/// A batch of 4 BGRA pixels stored in a Vector128&lt;byte&gt;.
/// Layout: [B0,G0,R0,A0, B1,G1,R1,A1, B2,G2,R2,A2, B3,G3,R3,A3]
/// Provides SIMD operations for batch pixel processing.
/// </summary>
internal readonly struct PixelBatch4
{
	public readonly Vector128<byte> Data;

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public PixelBatch4 (Vector128<byte> data) => Data = data;

	/// <summary>Number of ColorBgra pixels in this batch.</summary>
	public const int Count = 4;

	// --- Load / Store ---

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch4 Load (ReadOnlySpan<ColorBgra> src)
		=> new (Vector128.LoadUnsafe (
			ref Unsafe.As<ColorBgra, byte> (ref MemoryMarshal.GetReference (src))));

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly void Store (Span<ColorBgra> dst)
		=> Data.StoreUnsafe (ref Unsafe.As<ColorBgra, byte> (ref MemoryMarshal.GetReference (dst)));

	// --- Channel broadcast: replicate one channel across all 4 bytes of each pixel ---

	/// <summary>Shuffle mask that broadcasts byte 3 (Alpha) of each pixel to all 4 lanes.</summary>
	private static readonly Vector128<byte> AlphaShuffle = Vector128.Create (
		(byte) 3, 3, 3, 3, 7, 7, 7, 7, 11, 11, 11, 11, 15, 15, 15, 15);

	/// <summary>Returns a batch where every channel of each pixel is the alpha value.</summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly PixelBatch4 BroadcastAlpha ()
		=> new (Vector128.Shuffle (Data, AlphaShuffle));

	// --- Saturating Arithmetic ---

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch4 AddSaturate (PixelBatch4 a, PixelBatch4 b)
		=> new (Vector128.Max (a.Data, Vector128.Max (b.Data, (a.Data + b.Data))) == (a.Data + b.Data)
			? (a.Data + b.Data)
			: AddSaturateSlow (a.Data, b.Data));

	[MethodImpl (MethodImplOptions.NoInlining)]
	private static Vector128<byte> AddSaturateSlow (Vector128<byte> a, Vector128<byte> b)
	{
		var sum = a + b;
		var overflow = Vector128.LessThan (sum, a); // If sum < a, overflow occurred
		return Vector128.ConditionalSelect (overflow, Vector128.Create ((byte) 255), sum);
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch4 SubtractSaturate (PixelBatch4 a, PixelBatch4 b)
	{
		var diff = a.Data - b.Data;
		var underflow = Vector128.GreaterThan (b.Data, a.Data);
		return new (Vector128.AndNot (diff, underflow)); // Zero out underflowed lanes
	}

	// --- Widening to ushort pairs ---

	/// <summary>Widens the low 8 bytes to 8 ushorts (pixels 0-1).</summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly Vector128<ushort> WidenLow ()
		=> Vector128.WidenLower (Data).AsUInt16 ();

	/// <summary>Widens the high 8 bytes to 8 ushorts (pixels 2-3).</summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly Vector128<ushort> WidenHigh ()
		=> Vector128.WidenUpper (Data).AsUInt16 ();

	// --- DivBy255 on ushort vectors (exact: (x + 1 + ((x + 1) >> 8)) >> 8) ---

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static Vector128<ushort> DivBy255 (Vector128<ushort> x)
	{
		var xp1 = x + Vector128.Create ((ushort) 1);
		return (xp1 + Vector128.ShiftRightLogical (xp1, 8)) >>> 8;
	}

	/// <summary>Exact floor((x+128)/255) for rounding division by 255.</summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static Vector128<ushort> DivBy255Rounded (Vector128<ushort> x)
	{
		var xp129 = x + Vector128.Create ((ushort) 129);
		return (xp129 + Vector128.ShiftRightLogical (xp129, 8)) >>> 8;
	}

	// --- Narrow: pack two ushort vectors back to byte vector ---

	/// <summary>
	/// Narrows two Vector128&lt;ushort&gt; back to a Vector128&lt;byte&gt; with saturation to [0,255].
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch4 NarrowSaturate (Vector128<ushort> lo, Vector128<ushort> hi)
		=> new (Vector128.Narrow (
			Vector128.Min (lo, Vector128.Create ((ushort) 255)),
			Vector128.Min (hi, Vector128.Create ((ushort) 255))));

	// --- Bitwise ---

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch4 operator & (PixelBatch4 a, PixelBatch4 b)
		=> new (a.Data & b.Data);

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch4 operator | (PixelBatch4 a, PixelBatch4 b)
		=> new (a.Data | b.Data);

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch4 operator ^ (PixelBatch4 a, PixelBatch4 b)
		=> new (a.Data ^ b.Data);

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch4 operator ~ (PixelBatch4 a)
		=> new (~a.Data);

	// --- Min / Max ---

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch4 Min (PixelBatch4 a, PixelBatch4 b)
		=> new (Vector128.Min (a.Data, b.Data));

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch4 Max (PixelBatch4 a, PixelBatch4 b)
		=> new (Vector128.Max (a.Data, b.Data));

	// --- Conditional Select ---

	/// <summary>
	/// Selects bytes from 'ifTrue' where mask is 0xFF, from 'ifFalse' where mask is 0x00.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch4 ConditionalSelect (Vector128<byte> mask, PixelBatch4 ifTrue, PixelBatch4 ifFalse)
		=> new (Vector128.ConditionalSelect (mask, ifTrue.Data, ifFalse.Data));

	// --- Alpha queries ---

	/// <summary>Returns true if all 4 pixels have alpha == 0.</summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly bool AllTransparent ()
	{
		var alpha = BroadcastAlpha ().Data;
		return Vector128.EqualsAll (alpha, Vector128<byte>.Zero);
	}

	/// <summary>Returns true if all 4 pixels have alpha == 255.</summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly bool AllOpaque ()
	{
		var alpha = BroadcastAlpha ().Data;
		return Vector128.EqualsAll (alpha, Vector128.Create ((byte) 255));
	}

	// --- Constants ---

	public static PixelBatch4 Zero => new (Vector128<byte>.Zero);
	public static PixelBatch4 AllOnes => new (Vector128.Create ((byte) 255));

	/// <summary>Mask: 0xFF in alpha position, 0x00 elsewhere.</summary>
	public static readonly PixelBatch4 AlphaMask = new (Vector128.Create (
		(byte) 0, 0, 0, 0xFF, 0, 0, 0, 0xFF, 0, 0, 0, 0xFF, 0, 0, 0, 0xFF));

	/// <summary>Mask: 0xFF in color positions, 0x00 in alpha.</summary>
	public static readonly PixelBatch4 ColorMask = new (Vector128.Create (
		(byte) 0xFF, 0xFF, 0xFF, 0, 0xFF, 0xFF, 0xFF, 0, 0xFF, 0xFF, 0xFF, 0, 0xFF, 0xFF, 0xFF, 0));
}
