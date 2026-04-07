using System;
using System.Runtime.Intrinsics;
using NUnit.Framework;

namespace Pinta.Core.Tests;

[TestFixture]
internal sealed class PixelBatch8Tests
{
	private static readonly ColorBgra[] eight_pixels = [
		ColorBgra.FromBgra (10, 20, 30, 40),
		ColorBgra.FromBgra (50, 60, 70, 80),
		ColorBgra.FromBgra (90, 100, 110, 120),
		ColorBgra.FromBgra (130, 140, 150, 160),
		ColorBgra.FromBgra (170, 180, 190, 200),
		ColorBgra.FromBgra (210, 220, 230, 240),
		ColorBgra.FromBgra (1, 2, 3, 4),
		ColorBgra.FromBgra (5, 6, 7, 8),
	];

	// --- Load / Store ---

	[Test]
	public void Load_Store_Roundtrip ()
	{
		var batch = PixelBatch8.Load (eight_pixels);
		ColorBgra[] result = new ColorBgra[8];
		batch.Store (result);

		Assert.That (result, Is.EqualTo (eight_pixels));
	}

	[Test]
	public void LoadUnsafe_StoreUnsafe_Roundtrip ()
	{
		ColorBgra[] pixels = new ColorBgra[10];
		Array.Copy (eight_pixels, 0, pixels, 1, 8);

		var batch = PixelBatch8.LoadUnsafe (in pixels[0], 1);
		ColorBgra[] result = new ColorBgra[10];
		batch.StoreUnsafe (ref result[0], 1);

		for (int i = 1; i <= 8; i++)
			Assert.That (result[i], Is.EqualTo (pixels[i]));
	}

	// --- Per-Channel Broadcast ---

	[Test]
	public void BroadcastAlpha ()
	{
		var batch = PixelBatch8.Load (eight_pixels);
		var alphas = batch.BroadcastAlpha ();

		// Alpha values: 40, 80, 120, 160, 200, 240, 4, 8
		var expected = Vector256.Create (
			(byte) 40, 40, 40, 40, 80, 80, 80, 80, 120, 120, 120, 120, 160, 160, 160, 160,
			200, 200, 200, 200, 240, 240, 240, 240, 4, 4, 4, 4, 8, 8, 8, 8);

		Assert.That (alphas, Is.EqualTo (expected));
	}

	[Test]
	public void BroadcastBlue ()
	{
		var batch = PixelBatch8.Load (eight_pixels);
		var blues = batch.BroadcastBlue ();

		// Blue values: 10, 50, 90, 130, 170, 210, 1, 5
		var expected = Vector256.Create (
			(byte) 10, 10, 10, 10, 50, 50, 50, 50, 90, 90, 90, 90, 130, 130, 130, 130,
			170, 170, 170, 170, 210, 210, 210, 210, 1, 1, 1, 1, 5, 5, 5, 5);

		Assert.That (blues, Is.EqualTo (expected));
	}

	[Test]
	public void BroadcastGreen ()
	{
		var batch = PixelBatch8.Load (eight_pixels);
		var greens = batch.BroadcastGreen ();

		// Green values: 20, 60, 100, 140, 180, 220, 2, 6
		var expected = Vector256.Create (
			(byte) 20, 20, 20, 20, 60, 60, 60, 60, 100, 100, 100, 100, 140, 140, 140, 140,
			180, 180, 180, 180, 220, 220, 220, 220, 2, 2, 2, 2, 6, 6, 6, 6);

		Assert.That (greens, Is.EqualTo (expected));
	}

	[Test]
	public void BroadcastRed ()
	{
		var batch = PixelBatch8.Load (eight_pixels);
		var reds = batch.BroadcastRed ();

		// Red values: 30, 70, 110, 150, 190, 230, 3, 7
		var expected = Vector256.Create (
			(byte) 30, 30, 30, 30, 70, 70, 70, 70, 110, 110, 110, 110, 150, 150, 150, 150,
			190, 190, 190, 190, 230, 230, 230, 230, 3, 3, 3, 3, 7, 7, 7, 7);

		Assert.That (reds, Is.EqualTo (expected));
	}

	// --- Saturating Arithmetic ---

	[Test]
	public void AddSaturate_Clamps ()
	{
		var a = PixelBatch8.Create (200);
		var b = PixelBatch8.Create (100);
		var result = PixelBatch8.AddSaturate (a, b);

		Assert.That (result.Data, Is.EqualTo (Vector256.Create ((byte) 255)));
	}

	[Test]
	public void SubtractSaturate_Clamps ()
	{
		var a = PixelBatch8.Create (50);
		var b = PixelBatch8.Create (100);
		var result = PixelBatch8.SubtractSaturate (a, b);

		Assert.That (result.Data, Is.EqualTo (Vector256<byte>.Zero));
	}

	// --- Widening / Narrowing ---

	[Test]
	public void Widen_Narrow_Roundtrip ()
	{
		var batch = PixelBatch8.Load (eight_pixels);
		var low = batch.WidenLow ();
		var high = batch.WidenHigh ();

		// Verify widening preserves values
		Assert.That (low.GetElement (0), Is.EqualTo ((ushort) 10)); // B0
		Assert.That (low.GetElement (3), Is.EqualTo ((ushort) 40)); // A0
		Assert.That (high.GetElement (0), Is.EqualTo ((ushort) 170)); // B4
		Assert.That (high.GetElement (15), Is.EqualTo ((ushort) 8)); // A7

		// Narrow back and verify
		var narrowed = PixelBatch8.NarrowToBytes (low, high);
		ColorBgra[] result = new ColorBgra[8];
		narrowed.Store (result);

		Assert.That (result, Is.EqualTo (eight_pixels));
	}

	[Test]
	public void NarrowToBytes_ClampsOverflow ()
	{
		var low = Vector256.Create ((ushort) 500);
		var high = Vector256.Create ((ushort) 42);
		var result = PixelBatch8.NarrowToBytes (low, high);

		// First 16 bytes (from low half) should be clamped to 255
		for (int i = 0; i < 16; i++)
			Assert.That (result.Data.GetElement (i), Is.EqualTo ((byte) 255));

		// Last 16 bytes (from high half) should be 42
		for (int i = 16; i < 32; i++)
			Assert.That (result.Data.GetElement (i), Is.EqualTo ((byte) 42));
	}

	// --- DivBy255 ---

	[Test]
	public void DivBy255_MatchesScalar ()
	{
		ushort[] testValues = [0, 1, 127, 128, 254, 255, 256, 1000, 32768, 65025, 65278];

		foreach (var val in testValues) {
			var vec = Vector256.Create (val);
			var result = PixelBatch8.DivBy255 (vec);
			ushort expected = (ushort) (val / 255);
			Assert.That (result.GetElement (0), Is.EqualTo (expected),
				$"DivBy255({val}): expected {expected}, got {result.GetElement (0)}");
		}
	}

	// --- DivBy255Rounded ---

	[Test]
	public void DivBy255Rounded_MatchesScalar ()
	{
		ushort[] testValues = [0, 1, 126, 127, 128, 254, 255, 256, 1000, 32768, 65025];

		foreach (var val in testValues) {
			var vec = Vector256.Create (val);
			var result = PixelBatch8.DivBy255Rounded (vec);
			ushort expected = (ushort) ((val + 128) / 255);
			Assert.That (result.GetElement (0), Is.EqualTo (expected),
				$"DivBy255Rounded({val}): expected {expected}, got {result.GetElement (0)}");
		}
	}

	// --- Bitwise Operations ---

	[Test]
	public void BitwiseAnd ()
	{
		var a = new PixelBatch8 (Vector256.Create ((byte) 0xFF));
		var b = new PixelBatch8 (Vector256.Create ((byte) 0x0F));
		var result = a & b;
		Assert.That (result.Data, Is.EqualTo (Vector256.Create ((byte) 0x0F)));
	}

	[Test]
	public void BitwiseXor ()
	{
		var a = new PixelBatch8 (Vector256.Create ((byte) 0xFF));
		var b = new PixelBatch8 (Vector256.Create ((byte) 0x0F));
		var result = a ^ b;
		Assert.That (result.Data, Is.EqualTo (Vector256.Create ((byte) 0xF0)));
	}

	[Test]
	public void BitwiseNot ()
	{
		var a = new PixelBatch8 (Vector256.Create ((byte) 0x0F));
		var result = ~a;
		Assert.That (result.Data, Is.EqualTo (Vector256.Create ((byte) 0xF0)));
	}

	// --- Min / Max ---

	[Test]
	public void Min_ReturnsSmaller ()
	{
		var a = PixelBatch8.Create (100);
		var b = PixelBatch8.Create (200);
		var result = PixelBatch8.Min (a, b);
		Assert.That (result.Data, Is.EqualTo (Vector256.Create ((byte) 100)));
	}

	[Test]
	public void Max_ReturnsLarger ()
	{
		var a = PixelBatch8.Create (100);
		var b = PixelBatch8.Create (200);
		var result = PixelBatch8.Max (a, b);
		Assert.That (result.Data, Is.EqualTo (Vector256.Create ((byte) 200)));
	}

	// --- ConditionalSelect ---

	[Test]
	public void ConditionalSelect_AllTrue ()
	{
		var ifTrue = PixelBatch8.Create (42);
		var ifFalse = PixelBatch8.Create (99);
		var mask = Vector256.Create ((byte) 0xFF);
		var result = PixelBatch8.ConditionalSelect (mask, ifTrue, ifFalse);
		Assert.That (result.Data, Is.EqualTo (ifTrue.Data));
	}

	[Test]
	public void ConditionalSelect_AllFalse ()
	{
		var ifTrue = PixelBatch8.Create (42);
		var ifFalse = PixelBatch8.Create (99);
		var mask = Vector256<byte>.Zero;
		var result = PixelBatch8.ConditionalSelect (mask, ifTrue, ifFalse);
		Assert.That (result.Data, Is.EqualTo (ifFalse.Data));
	}

	// --- AllTransparent / AllOpaque / AnyVisible ---

	[Test]
	public void AllTransparent_True ()
	{
		ColorBgra[] pixels = [
			ColorBgra.FromBgra (10, 20, 30, 0),
			ColorBgra.FromBgra (50, 60, 70, 0),
			ColorBgra.FromBgra (90, 100, 110, 0),
			ColorBgra.FromBgra (130, 140, 150, 0),
			ColorBgra.FromBgra (1, 2, 3, 0),
			ColorBgra.FromBgra (4, 5, 6, 0),
			ColorBgra.FromBgra (7, 8, 9, 0),
			ColorBgra.FromBgra (10, 11, 12, 0),
		];

		var batch = PixelBatch8.Load (pixels);
		Assert.That (batch.AllTransparent (), Is.True);
		Assert.That (batch.AllOpaque (), Is.False);
		Assert.That (batch.AnyVisible (), Is.False);
	}

	[Test]
	public void AllOpaque_True ()
	{
		ColorBgra[] pixels = [
			ColorBgra.FromBgra (10, 20, 30, 255),
			ColorBgra.FromBgra (50, 60, 70, 255),
			ColorBgra.FromBgra (90, 100, 110, 255),
			ColorBgra.FromBgra (130, 140, 150, 255),
			ColorBgra.FromBgra (1, 2, 3, 255),
			ColorBgra.FromBgra (4, 5, 6, 255),
			ColorBgra.FromBgra (7, 8, 9, 255),
			ColorBgra.FromBgra (10, 11, 12, 255),
		];

		var batch = PixelBatch8.Load (pixels);
		Assert.That (batch.AllTransparent (), Is.False);
		Assert.That (batch.AllOpaque (), Is.True);
		Assert.That (batch.AnyVisible (), Is.True);
	}

	[Test]
	public void MixedAlpha ()
	{
		ColorBgra[] pixels = [
			ColorBgra.FromBgra (10, 20, 30, 0),
			ColorBgra.FromBgra (50, 60, 70, 128),
			ColorBgra.FromBgra (90, 100, 110, 255),
			ColorBgra.FromBgra (130, 140, 150, 0),
			ColorBgra.FromBgra (1, 2, 3, 0),
			ColorBgra.FromBgra (4, 5, 6, 0),
			ColorBgra.FromBgra (7, 8, 9, 0),
			ColorBgra.FromBgra (10, 11, 12, 64),
		];

		var batch = PixelBatch8.Load (pixels);
		Assert.That (batch.AllTransparent (), Is.False);
		Assert.That (batch.AllOpaque (), Is.False);
		Assert.That (batch.AnyVisible (), Is.True);
	}

	// --- GetLower / GetUpper / Create from PixelBatch4 ---

	[Test]
	public void GetLower_GetUpper_Create_Roundtrip ()
	{
		var batch = PixelBatch8.Load (eight_pixels);
		var lower = batch.GetLower ();
		var upper = batch.GetUpper ();

		// Verify lower half
		ColorBgra[] lowerResult = new ColorBgra[4];
		lower.Store (lowerResult);
		for (int i = 0; i < 4; i++)
			Assert.That (lowerResult[i], Is.EqualTo (eight_pixels[i]));

		// Verify upper half
		ColorBgra[] upperResult = new ColorBgra[4];
		upper.Store (upperResult);
		for (int i = 0; i < 4; i++)
			Assert.That (upperResult[i], Is.EqualTo (eight_pixels[i + 4]));

		// Recombine and verify
		var recombined = PixelBatch8.Create (lower, upper);
		ColorBgra[] fullResult = new ColorBgra[8];
		recombined.Store (fullResult);
		Assert.That (fullResult, Is.EqualTo (eight_pixels));
	}

	// --- Zero ---

	[Test]
	public void Zero_IsAllZeros ()
	{
		var batch = PixelBatch8.Zero;
		Assert.That (batch.Data, Is.EqualTo (Vector256<byte>.Zero));
		Assert.That (batch.AllTransparent (), Is.True);
	}

	// --- Count ---

	[Test]
	public void Count_Is8 ()
	{
		Assert.That (PixelBatch8.Count, Is.EqualTo (8));
	}
}
