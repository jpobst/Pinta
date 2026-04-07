using System.Runtime.Intrinsics;
using NUnit.Framework;

namespace Pinta.Core.Tests;

[TestFixture]
internal sealed class PixelBatch4Tests
{
	// --- Load / Store ---

	[Test]
	public void Load_Store_Roundtrip ()
	{
		ColorBgra[] pixels = [
			ColorBgra.FromBgra (10, 20, 30, 40),
			ColorBgra.FromBgra (50, 60, 70, 80),
			ColorBgra.FromBgra (90, 100, 110, 120),
			ColorBgra.FromBgra (130, 140, 150, 160),
		];

		var batch = PixelBatch4.Load (pixels);
		ColorBgra[] result = new ColorBgra[4];
		batch.Store (result);

		Assert.That (result, Is.EqualTo (pixels));
	}

	[Test]
	public void LoadUnsafe_StoreUnsafe_Roundtrip ()
	{
		ColorBgra[] pixels = [
			ColorBgra.FromBgra (0, 0, 0, 0), // padding
			ColorBgra.FromBgra (10, 20, 30, 40),
			ColorBgra.FromBgra (50, 60, 70, 80),
			ColorBgra.FromBgra (90, 100, 110, 120),
			ColorBgra.FromBgra (130, 140, 150, 160),
		];

		var batch = PixelBatch4.LoadUnsafe (in pixels[0], 1);
		ColorBgra[] result = new ColorBgra[5];
		batch.StoreUnsafe (ref result[0], 1);

		Assert.That (result[1], Is.EqualTo (pixels[1]));
		Assert.That (result[2], Is.EqualTo (pixels[2]));
		Assert.That (result[3], Is.EqualTo (pixels[3]));
		Assert.That (result[4], Is.EqualTo (pixels[4]));
	}

	// --- Per-Channel Broadcast ---

	[Test]
	public void BroadcastAlpha ()
	{
		// Pixel 0: B=10, G=20, R=30, A=40
		// Pixel 1: B=50, G=60, R=70, A=80
		// Pixel 2: B=90, G=100, R=110, A=120
		// Pixel 3: B=130, G=140, R=150, A=160
		ColorBgra[] pixels = [
			ColorBgra.FromBgra (10, 20, 30, 40),
			ColorBgra.FromBgra (50, 60, 70, 80),
			ColorBgra.FromBgra (90, 100, 110, 120),
			ColorBgra.FromBgra (130, 140, 150, 160),
		];

		var batch = PixelBatch4.Load (pixels);
		var alphas = batch.BroadcastAlpha ();

		// Expected: [40,40,40,40, 80,80,80,80, 120,120,120,120, 160,160,160,160]
		var expected = Vector128.Create (
			(byte) 40, 40, 40, 40, 80, 80, 80, 80, 120, 120, 120, 120, 160, 160, 160, 160);

		Assert.That (alphas, Is.EqualTo (expected));
	}

	[Test]
	public void BroadcastBlue ()
	{
		ColorBgra[] pixels = [
			ColorBgra.FromBgra (10, 20, 30, 40),
			ColorBgra.FromBgra (50, 60, 70, 80),
			ColorBgra.FromBgra (90, 100, 110, 120),
			ColorBgra.FromBgra (130, 140, 150, 160),
		];

		var batch = PixelBatch4.Load (pixels);
		var blues = batch.BroadcastBlue ();

		var expected = Vector128.Create (
			(byte) 10, 10, 10, 10, 50, 50, 50, 50, 90, 90, 90, 90, 130, 130, 130, 130);

		Assert.That (blues, Is.EqualTo (expected));
	}

	[Test]
	public void BroadcastGreen ()
	{
		ColorBgra[] pixels = [
			ColorBgra.FromBgra (10, 20, 30, 40),
			ColorBgra.FromBgra (50, 60, 70, 80),
			ColorBgra.FromBgra (90, 100, 110, 120),
			ColorBgra.FromBgra (130, 140, 150, 160),
		];

		var batch = PixelBatch4.Load (pixels);
		var greens = batch.BroadcastGreen ();

		var expected = Vector128.Create (
			(byte) 20, 20, 20, 20, 60, 60, 60, 60, 100, 100, 100, 100, 140, 140, 140, 140);

		Assert.That (greens, Is.EqualTo (expected));
	}

	[Test]
	public void BroadcastRed ()
	{
		ColorBgra[] pixels = [
			ColorBgra.FromBgra (10, 20, 30, 40),
			ColorBgra.FromBgra (50, 60, 70, 80),
			ColorBgra.FromBgra (90, 100, 110, 120),
			ColorBgra.FromBgra (130, 140, 150, 160),
		];

		var batch = PixelBatch4.Load (pixels);
		var reds = batch.BroadcastRed ();

		var expected = Vector128.Create (
			(byte) 30, 30, 30, 30, 70, 70, 70, 70, 110, 110, 110, 110, 150, 150, 150, 150);

		Assert.That (reds, Is.EqualTo (expected));
	}

	// --- Saturating Arithmetic ---

	[Test]
	public void AddSaturate_Clamps ()
	{
		var a = PixelBatch4.Create (200);
		var b = PixelBatch4.Create (100);
		var result = PixelBatch4.AddSaturate (a, b);

		// 200 + 100 = 300, clamped to 255
		Assert.That (result.Data, Is.EqualTo (Vector128.Create ((byte) 255)));
	}

	[Test]
	public void AddSaturate_NoOverflow ()
	{
		var a = PixelBatch4.Create (100);
		var b = PixelBatch4.Create (50);
		var result = PixelBatch4.AddSaturate (a, b);

		Assert.That (result.Data, Is.EqualTo (Vector128.Create ((byte) 150)));
	}

	[Test]
	public void SubtractSaturate_Clamps ()
	{
		var a = PixelBatch4.Create (50);
		var b = PixelBatch4.Create (100);
		var result = PixelBatch4.SubtractSaturate (a, b);

		// 50 - 100, clamped to 0
		Assert.That (result.Data, Is.EqualTo (Vector128<byte>.Zero));
	}

	[Test]
	public void SubtractSaturate_NoUnderflow ()
	{
		var a = PixelBatch4.Create (200);
		var b = PixelBatch4.Create (50);
		var result = PixelBatch4.SubtractSaturate (a, b);

		Assert.That (result.Data, Is.EqualTo (Vector128.Create ((byte) 150)));
	}

	// --- Widening / Narrowing ---

	[Test]
	public void Widen_Narrow_Roundtrip ()
	{
		ColorBgra[] pixels = [
			ColorBgra.FromBgra (10, 20, 30, 40),
			ColorBgra.FromBgra (50, 60, 70, 80),
			ColorBgra.FromBgra (90, 100, 110, 120),
			ColorBgra.FromBgra (130, 140, 150, 160),
		];

		var batch = PixelBatch4.Load (pixels);
		var low = batch.WidenLow ();
		var high = batch.WidenHigh ();

		// Verify widening preserves values
		Assert.That (low.GetElement (0), Is.EqualTo ((ushort) 10)); // B0
		Assert.That (low.GetElement (3), Is.EqualTo ((ushort) 40)); // A0
		Assert.That (low.GetElement (4), Is.EqualTo ((ushort) 50)); // B1
		Assert.That (high.GetElement (0), Is.EqualTo ((ushort) 90)); // B2
		Assert.That (high.GetElement (7), Is.EqualTo ((ushort) 160)); // A3

		// Narrow back and verify
		var narrowed = PixelBatch4.NarrowToBytes (low, high);
		ColorBgra[] result = new ColorBgra[4];
		narrowed.Store (result);

		Assert.That (result, Is.EqualTo (pixels));
	}

	[Test]
	public void NarrowToBytes_ClampsOverflow ()
	{
		var low = Vector128.Create ((ushort) 300); // > 255
		var high = Vector128.Create ((ushort) 50);
		var result = PixelBatch4.NarrowToBytes (low, high);

		// Verify low half is clamped to 255
		for (int i = 0; i < 8; i++)
			Assert.That (result.Data.GetElement (i), Is.EqualTo ((byte) 255));

		// Verify high half is 50
		for (int i = 8; i < 16; i++)
			Assert.That (result.Data.GetElement (i), Is.EqualTo ((byte) 50));
	}

	// --- DivBy255 ---

	[Test]
	public void DivBy255_MatchesScalar ()
	{
		ushort[] testValues = [0, 1, 127, 128, 254, 255, 256, 1000, 32768, 65025, 65278];

		foreach (var val in testValues) {
			var vec = Vector128.Create (val);
			var result = PixelBatch4.DivBy255 (vec);
			ushort expected = (ushort) (val / 255);
			Assert.That (result.GetElement (0), Is.EqualTo (expected),
				$"DivBy255({val}): expected {expected}, got {result.GetElement (0)}");
		}
	}

	// --- DivBy255Rounded ---

	[Test]
	public void DivBy255Rounded_MatchesScalar ()
	{
		// Test key values that exercise rounding behavior
		ushort[] testValues = [0, 1, 126, 127, 128, 254, 255, 256, 1000, 32768, 65025];

		foreach (var val in testValues) {
			var vec = Vector128.Create (val);
			var result = PixelBatch4.DivBy255Rounded (vec);
			ushort expected = (ushort) ((val + 128) / 255);
			Assert.That (result.GetElement (0), Is.EqualTo (expected),
				$"DivBy255Rounded({val}): expected {expected}, got {result.GetElement (0)}");
		}
	}

	[Test]
	public void DivBy255Rounded_FastScaleByteByByte_Equivalence ()
	{
		// Verify DivBy255Rounded matches FastScaleByteByByte for all byte*byte products
		for (int a = 0; a <= 255; a += 17) {
			for (int b = 0; b <= 255; b += 17) {
				int product = a * b;
				byte scalarResult = Utility.FastScaleByteByByte ((byte) a, (byte) b);
				var vec = Vector128.Create ((ushort) product);
				var simdResult = PixelBatch4.DivBy255Rounded (vec).GetElement (0);
				Assert.That ((byte) simdResult, Is.EqualTo (scalarResult),
					$"FastScaleByteByByte({a}, {b}): product={product}, scalar={scalarResult}, simd={simdResult}");
			}
		}
	}

	// --- Bitwise Operations ---

	[Test]
	public void BitwiseAnd ()
	{
		var a = new PixelBatch4 (Vector128.Create ((byte) 0xFF));
		var b = new PixelBatch4 (Vector128.Create ((byte) 0x0F));
		var result = a & b;
		Assert.That (result.Data, Is.EqualTo (Vector128.Create ((byte) 0x0F)));
	}

	[Test]
	public void BitwiseOr ()
	{
		var a = new PixelBatch4 (Vector128.Create ((byte) 0xF0));
		var b = new PixelBatch4 (Vector128.Create ((byte) 0x0F));
		var result = a | b;
		Assert.That (result.Data, Is.EqualTo (Vector128.Create ((byte) 0xFF)));
	}

	[Test]
	public void BitwiseXor ()
	{
		var a = new PixelBatch4 (Vector128.Create ((byte) 0xFF));
		var b = new PixelBatch4 (Vector128.Create ((byte) 0x0F));
		var result = a ^ b;
		Assert.That (result.Data, Is.EqualTo (Vector128.Create ((byte) 0xF0)));
	}

	[Test]
	public void BitwiseNot ()
	{
		var a = new PixelBatch4 (Vector128.Create ((byte) 0x0F));
		var result = ~a;
		Assert.That (result.Data, Is.EqualTo (Vector128.Create ((byte) 0xF0)));
	}

	// --- Min / Max ---

	[Test]
	public void Min_ReturnsSmaller ()
	{
		var a = PixelBatch4.Create (100);
		var b = PixelBatch4.Create (200);
		var result = PixelBatch4.Min (a, b);
		Assert.That (result.Data, Is.EqualTo (Vector128.Create ((byte) 100)));
	}

	[Test]
	public void Max_ReturnsLarger ()
	{
		var a = PixelBatch4.Create (100);
		var b = PixelBatch4.Create (200);
		var result = PixelBatch4.Max (a, b);
		Assert.That (result.Data, Is.EqualTo (Vector128.Create ((byte) 200)));
	}

	// --- ConditionalSelect ---

	[Test]
	public void ConditionalSelect_AllTrue ()
	{
		var ifTrue = PixelBatch4.Create (42);
		var ifFalse = PixelBatch4.Create (99);
		var mask = Vector128.Create ((byte) 0xFF);
		var result = PixelBatch4.ConditionalSelect (mask, ifTrue, ifFalse);
		Assert.That (result.Data, Is.EqualTo (ifTrue.Data));
	}

	[Test]
	public void ConditionalSelect_AllFalse ()
	{
		var ifTrue = PixelBatch4.Create (42);
		var ifFalse = PixelBatch4.Create (99);
		var mask = Vector128<byte>.Zero;
		var result = PixelBatch4.ConditionalSelect (mask, ifTrue, ifFalse);
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
		];

		var batch = PixelBatch4.Load (pixels);
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
		];

		var batch = PixelBatch4.Load (pixels);
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
		];

		var batch = PixelBatch4.Load (pixels);
		Assert.That (batch.AllTransparent (), Is.False);
		Assert.That (batch.AllOpaque (), Is.False);
		Assert.That (batch.AnyVisible (), Is.True);
	}

	// --- Zero ---

	[Test]
	public void Zero_IsAllZeros ()
	{
		var batch = PixelBatch4.Zero;
		Assert.That (batch.Data, Is.EqualTo (Vector128<byte>.Zero));
		Assert.That (batch.AllTransparent (), Is.True);
	}

	// --- Count ---

	[Test]
	public void Count_Is4 ()
	{
		Assert.That (PixelBatch4.Count, Is.EqualTo (4));
	}
}
