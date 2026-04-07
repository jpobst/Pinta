using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Pinta.Foundation;

/// <summary>
/// A batch of 8 BGRA pixels stored in a Vector256&lt;byte&gt;.
/// Layout: [B0,G0,R0,A0, ..., B7,G7,R7,A7]
/// Provides SIMD operations for batch pixel processing.
/// </summary>
internal readonly struct PixelBatch8
{
	public readonly Vector256<byte> Data;

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public PixelBatch8 (Vector256<byte> data) => Data = data;

	/// <summary>Number of ColorBgra pixels in this batch.</summary>
	public const int Count = 8;

	// --- Load / Store ---

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch8 Load (ReadOnlySpan<ColorBgra> src)
		=> new (Vector256.LoadUnsafe (
			ref Unsafe.As<ColorBgra, byte> (ref MemoryMarshal.GetReference (src))));

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly void Store (Span<ColorBgra> dst)
		=> Data.StoreUnsafe (ref Unsafe.As<ColorBgra, byte> (ref MemoryMarshal.GetReference (dst)));

	// --- Channel broadcast ---

	private static readonly Vector256<byte> AlphaShuffle = Vector256.Create (
		(byte) 3, 3, 3, 3, 7, 7, 7, 7, 11, 11, 11, 11, 15, 15, 15, 15,
		3, 3, 3, 3, 7, 7, 7, 7, 11, 11, 11, 11, 15, 15, 15, 15);

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly PixelBatch8 BroadcastAlpha ()
		=> new (Vector256.Shuffle (Data, AlphaShuffle));

	// --- Widening to ushort ---

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly Vector256<ushort> WidenLow ()
		=> Vector256.WidenLower (Data).AsUInt16 ();

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly Vector256<ushort> WidenHigh ()
		=> Vector256.WidenUpper (Data).AsUInt16 ();

	// --- DivBy255 on ushort vectors ---

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static Vector256<ushort> DivBy255 (Vector256<ushort> x)
	{
		var xp1 = x + Vector256.Create ((ushort) 1);
		return (xp1 + Vector256.ShiftRightLogical (xp1, 8)) >>> 8;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static Vector256<ushort> DivBy255Rounded (Vector256<ushort> x)
	{
		var xp129 = x + Vector256.Create ((ushort) 129);
		return (xp129 + Vector256.ShiftRightLogical (xp129, 8)) >>> 8;
	}

	// --- Narrow ---

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch8 NarrowSaturate (Vector256<ushort> lo, Vector256<ushort> hi)
		=> new (Vector256.Narrow (
			Vector256.Min (lo, Vector256.Create ((ushort) 255)),
			Vector256.Min (hi, Vector256.Create ((ushort) 255))));

	// --- Bitwise ---

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch8 operator & (PixelBatch8 a, PixelBatch8 b)
		=> new (a.Data & b.Data);

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch8 operator | (PixelBatch8 a, PixelBatch8 b)
		=> new (a.Data | b.Data);

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch8 operator ^ (PixelBatch8 a, PixelBatch8 b)
		=> new (a.Data ^ b.Data);

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch8 operator ~ (PixelBatch8 a)
		=> new (~a.Data);

	// --- Min / Max ---

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch8 Min (PixelBatch8 a, PixelBatch8 b)
		=> new (Vector256.Min (a.Data, b.Data));

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch8 Max (PixelBatch8 a, PixelBatch8 b)
		=> new (Vector256.Max (a.Data, b.Data));

	// --- Conditional Select ---

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static PixelBatch8 ConditionalSelect (Vector256<byte> mask, PixelBatch8 ifTrue, PixelBatch8 ifFalse)
		=> new (Vector256.ConditionalSelect (mask, ifTrue.Data, ifFalse.Data));

	// --- Alpha queries ---

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly bool AllTransparent ()
	{
		var alpha = BroadcastAlpha ().Data;
		return Vector256.EqualsAll (alpha, Vector256<byte>.Zero);
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public readonly bool AllOpaque ()
	{
		var alpha = BroadcastAlpha ().Data;
		return Vector256.EqualsAll (alpha, Vector256.Create ((byte) 255));
	}

	// --- Constants ---

	public static PixelBatch8 Zero => new (Vector256<byte>.Zero);
	public static PixelBatch8 AllOnes => new (Vector256.Create ((byte) 255));

	public static readonly PixelBatch8 AlphaMask = new (Vector256.Create (
		(byte) 0, 0, 0, 0xFF, 0, 0, 0, 0xFF, 0, 0, 0, 0xFF, 0, 0, 0, 0xFF,
		0, 0, 0, 0xFF, 0, 0, 0, 0xFF, 0, 0, 0, 0xFF, 0, 0, 0, 0xFF));

	public static readonly PixelBatch8 ColorMask = new (Vector256.Create (
		(byte) 0xFF, 0xFF, 0xFF, 0, 0xFF, 0xFF, 0xFF, 0, 0xFF, 0xFF, 0xFF, 0, 0xFF, 0xFF, 0xFF, 0,
		0xFF, 0xFF, 0xFF, 0, 0xFF, 0xFF, 0xFF, 0, 0xFF, 0xFF, 0xFF, 0, 0xFF, 0xFF, 0xFF, 0));
}
