using System;
using NUnit.Framework;
using Pinta.Foundation;

namespace Pinta.Foundation.Tests;

[TestFixture]
public class BlendOpTests
{
	[Test]
	public void NormalBlend_OpaqueTop_ReturnsTop ()
	{
		var bottom = ColorBgra.FromBgra (100, 150, 200, 255);
		var top = ColorBgra.FromBgra (50, 75, 25, 255);
		var result = UserBlendOps.NormalBlendOp.ApplyStatic (bottom, top);
		Assert.Multiple (() => {
			Assert.That (result.B, Is.EqualTo (50));
			Assert.That (result.G, Is.EqualTo (75));
			Assert.That (result.R, Is.EqualTo (25));
			Assert.That (result.A, Is.EqualTo (255));
		});
	}

	[Test]
	public void NormalBlend_TransparentTop_ReturnsBottom ()
	{
		var bottom = ColorBgra.FromBgra (100, 150, 200, 255);
		var top = ColorBgra.FromBgra (50, 75, 25, 0);
		var result = UserBlendOps.NormalBlendOp.ApplyStatic (bottom, top);
		Assert.Multiple (() => {
			Assert.That (result.B, Is.EqualTo (100));
			Assert.That (result.G, Is.EqualTo (150));
			Assert.That (result.R, Is.EqualTo (200));
			Assert.That (result.A, Is.EqualTo (255));
		});
	}

	[Test]
	public void MultiplyBlend_ProducesCorrectResult ()
	{
		var bottom = ColorBgra.FromBgra (128, 128, 128, 255);
		var top = ColorBgra.FromBgra (128, 128, 128, 255);
		var result = UserBlendOps.MultiplyBlendOp.ApplyStatic (bottom, top);
		// With premultiplied multiply: (128*128)/255 ≈ 64
		Assert.Multiple (() => {
			Assert.That (result.B, Is.InRange (63, 65));
			Assert.That (result.G, Is.InRange (63, 65));
			Assert.That (result.R, Is.InRange (63, 65));
			Assert.That (result.A, Is.EqualTo (255));
		});
	}

	[Test]
	public void ScreenBlend_WhiteOnBlack_ReturnsWhite ()
	{
		var black = ColorBgra.FromBgra (0, 0, 0, 255);
		var white = ColorBgra.FromBgra (255, 255, 255, 255);
		var result = UserBlendOps.ScreenBlendOp.ApplyStatic (black, white);
		Assert.Multiple (() => {
			Assert.That (result.R, Is.EqualTo (255));
			Assert.That (result.G, Is.EqualTo (255));
			Assert.That (result.B, Is.EqualTo (255));
		});
	}

	[Test]
	public void DarkenBlend_ProducesDarkerColor ()
	{
		var color1 = ColorBgra.FromBgra (200, 100, 150, 255);
		var color2 = ColorBgra.FromBgra (100, 200, 150, 255);
		var result = UserBlendOps.DarkenBlendOp.ApplyStatic (color1, color2);
		// Darken selects min of each channel
		Assert.Multiple (() => {
			Assert.That (result.B, Is.EqualTo (100));
			Assert.That (result.G, Is.EqualTo (100));
			Assert.That (result.R, Is.EqualTo (150));
		});
	}

	[Test]
	public void LightenBlend_ProducesLighterColor ()
	{
		var color1 = ColorBgra.FromBgra (200, 100, 150, 255);
		var color2 = ColorBgra.FromBgra (100, 200, 150, 255);
		var result = UserBlendOps.LightenBlendOp.ApplyStatic (color1, color2);
		// Lighten selects max of each channel
		Assert.Multiple (() => {
			Assert.That (result.B, Is.EqualTo (200));
			Assert.That (result.G, Is.EqualTo (200));
			Assert.That (result.R, Is.EqualTo (150));
		});
	}

	[Test]
	public void DifferenceBlend_SameColor_ReturnsBlack ()
	{
		var color = ColorBgra.FromBgra (128, 128, 128, 255);
		var result = UserBlendOps.DifferenceBlendOp.ApplyStatic (color, color);
		Assert.Multiple (() => {
			Assert.That (result.B, Is.EqualTo (0));
			Assert.That (result.G, Is.EqualTo (0));
			Assert.That (result.R, Is.EqualTo (0));
		});
	}

	[Test]
	public void CreateBlendOp_ReturnsCorrectTypes ()
	{
		Assert.Multiple (() => {
			Assert.That (UserBlendOps.CreateBlendOp (BlendMode.Normal), Is.InstanceOf<UserBlendOps.NormalBlendOp> ());
			Assert.That (UserBlendOps.CreateBlendOp (BlendMode.Multiply), Is.InstanceOf<UserBlendOps.MultiplyBlendOp> ());
			Assert.That (UserBlendOps.CreateBlendOp (BlendMode.Screen), Is.InstanceOf<UserBlendOps.ScreenBlendOp> ());
			Assert.That (UserBlendOps.CreateBlendOp (BlendMode.Overlay), Is.InstanceOf<UserBlendOps.OverlayBlendOp> ());
		});
	}

	// ============================================================
	// SIMD batch tests - ensure the vectorized paths produce
	// identical results to the scalar path for 8+ pixels.
	// ============================================================

	[Test]
	public void NormalBlend_BatchSimdMatchesScalar ()
	{
		var op = new UserBlendOps.NormalBlendOp ();
		AssertBatchMatchesScalar (op, alpha1: 255, alpha2: 128);
	}

	[Test]
	public void NormalBlend_BatchSimd_AllOpaque ()
	{
		var op = new UserBlendOps.NormalBlendOp ();
		AssertBatchMatchesScalar (op, alpha1: 255, alpha2: 255);
	}

	[Test]
	public void NormalBlend_BatchSimd_AllTransparent ()
	{
		var op = new UserBlendOps.NormalBlendOp ();
		AssertBatchMatchesScalar (op, alpha1: 255, alpha2: 0);
	}

	[Test]
	public void MultiplyBlend_BatchSimdMatchesScalar ()
	{
		var op = new UserBlendOps.MultiplyBlendOp ();
		AssertBatchMatchesScalar (op, alpha1: 255, alpha2: 200);
	}

	[Test]
	public void ScreenBlend_BatchSimdMatchesScalar ()
	{
		var op = new UserBlendOps.ScreenBlendOp ();
		AssertBatchMatchesScalar (op, alpha1: 255, alpha2: 180);
	}

	[Test]
	public void DarkenBlend_BatchSimdMatchesScalar ()
	{
		var op = new UserBlendOps.DarkenBlendOp ();
		AssertBatchMatchesScalar (op, alpha1: 255, alpha2: 200);
	}

	[Test]
	public void LightenBlend_BatchSimdMatchesScalar ()
	{
		var op = new UserBlendOps.LightenBlendOp ();
		AssertBatchMatchesScalar (op, alpha1: 255, alpha2: 200);
	}

	[Test]
	public void DifferenceBlend_BatchSimdMatchesScalar ()
	{
		var op = new UserBlendOps.DifferenceBlendOp ();
		AssertBatchMatchesScalar (op, alpha1: 255, alpha2: 200);
	}

	[Test]
	public void OverlayBlend_BatchSimdMatchesScalar ()
	{
		var op = new UserBlendOps.OverlayBlendOp ();
		AssertBatchMatchesScalar (op, alpha1: 255, alpha2: 200);
	}

	[Test]
	public void HardLightBlend_BatchSimdMatchesScalar ()
	{
		var op = new UserBlendOps.HardLightBlendOp ();
		AssertBatchMatchesScalar (op, alpha1: 255, alpha2: 200);
	}

	/// <summary>
	/// Creates 8 pixel pairs, runs both scalar (one-at-a-time) and batch SIMD paths,
	/// and verifies they produce identical results (within ±1 for rounding).
	/// </summary>
	private static void AssertBatchMatchesScalar (UserBlendOp op, byte alpha1, byte alpha2)
	{
		const int count = 8; // Must be >= 4 to hit SIMD path
		var lhs = new ColorBgra[count];
		var rhs = new ColorBgra[count];

		// Create varied pixel data - ensure valid premultiplied alpha (channels ≤ alpha)
		for (int i = 0; i < count; i++) {
			lhs[i] = MakePremultiplied ((byte) (i * 30), (byte) (i * 25), (byte) (i * 20), alpha1);
			rhs[i] = MakePremultiplied ((byte) (255 - i * 30), (byte) (i * 15), (byte) (128 + i * 10), alpha2);
		}

		// Scalar results
		var scalarResults = new ColorBgra[count];
		for (int i = 0; i < count; i++)
			scalarResults[i] = op.Apply (lhs[i], rhs[i]);

		// SIMD batch results
		var batchResults = new ColorBgra[count];
		op.Apply (batchResults, lhs, rhs);

		// Compare
		for (int i = 0; i < count; i++) {
			Assert.Multiple (() => {
				Assert.That (batchResults[i].B, Is.EqualTo (scalarResults[i].B).Within (1), $"Pixel {i} B mismatch");
				Assert.That (batchResults[i].G, Is.EqualTo (scalarResults[i].G).Within (1), $"Pixel {i} G mismatch");
				Assert.That (batchResults[i].R, Is.EqualTo (scalarResults[i].R).Within (1), $"Pixel {i} R mismatch");
				Assert.That (batchResults[i].A, Is.EqualTo (scalarResults[i].A).Within (1), $"Pixel {i} A mismatch");
			});
		}
	}

	[Test]
	public void NormalBlend_BatchSimd_PartialAlpha_MatchesScalar ()
	{
		// Test with semi-transparent pixels to stress the full blend math
		var op = new UserBlendOps.NormalBlendOp ();
		const int count = 12; // 4*3 to cover multiple vector batches + remainder
		var lhs = new ColorBgra[count];
		var rhs = new ColorBgra[count];

		for (int i = 0; i < count; i++) {
			byte a1 = (byte) (200 - i * 10);
			byte a2 = (byte) (50 + i * 15);
			lhs[i] = MakePremultiplied ((byte) (i * 20), (byte) (i * 15 + 10), (byte) (i * 10 + 20), a1);
			rhs[i] = MakePremultiplied ((byte) (255 - i * 20), (byte) (i * 10), (byte) (128), a2);
		}

		var scalarResults = new ColorBgra[count];
		for (int i = 0; i < count; i++)
			scalarResults[i] = op.Apply (lhs[i], rhs[i]);

		var batchResults = new ColorBgra[count];
		op.Apply (batchResults, lhs, rhs);

		for (int i = 0; i < count; i++) {
			Assert.Multiple (() => {
				Assert.That (batchResults[i].B, Is.EqualTo (scalarResults[i].B).Within (1), $"Pixel {i} B mismatch");
				Assert.That (batchResults[i].G, Is.EqualTo (scalarResults[i].G).Within (1), $"Pixel {i} G mismatch");
				Assert.That (batchResults[i].R, Is.EqualTo (scalarResults[i].R).Within (1), $"Pixel {i} R mismatch");
				Assert.That (batchResults[i].A, Is.EqualTo (scalarResults[i].A).Within (1), $"Pixel {i} A mismatch");
			});
		}
	}

	/// <summary>
	/// Creates a valid premultiplied alpha pixel (channels clamped to alpha).
	/// </summary>
	private static ColorBgra MakePremultiplied (byte b, byte g, byte r, byte a)
	{
		return ColorBgra.FromBgra (
			Math.Min (b, a),
			Math.Min (g, a),
			Math.Min (r, a),
			a);
	}

	// ============================================================
	// Tests for newly vectorized blend ops
	// ============================================================

	[Test]
	public void AdditiveBlend_BatchSimdMatchesScalar ()
	{
		var op = new UserBlendOps.AdditiveBlendOp ();
		AssertBatchMatchesScalar (op, alpha1: 255, alpha2: 200);
	}

	[Test]
	public void NegationBlend_BatchSimdMatchesScalar ()
	{
		var op = new UserBlendOps.NegationBlendOp ();
		AssertBatchMatchesScalar (op, alpha1: 255, alpha2: 200);
	}

	[Test]
	public void XorBlend_BatchSimdMatchesScalar ()
	{
		var op = new UserBlendOps.XorBlendOp ();
		AssertBatchMatchesScalar (op, alpha1: 255, alpha2: 200);
	}

	[Test]
	public void ColorBurnBlend_BatchSimdMatchesScalar ()
	{
		var op = new UserBlendOps.ColorBurnBlendOp ();
		AssertBatchMatchesScalar (op, alpha1: 255, alpha2: 200);
	}

	[Test]
	public void ColorDodgeBlend_BatchSimdMatchesScalar ()
	{
		var op = new UserBlendOps.ColorDodgeBlendOp ();
		AssertBatchMatchesScalar (op, alpha1: 255, alpha2: 200);
	}

	[Test]
	public void GlowBlend_BatchSimdMatchesScalar ()
	{
		var op = new UserBlendOps.GlowBlendOp ();
		AssertBatchMatchesScalar (op, alpha1: 255, alpha2: 200);
	}

	[Test]
	public void ReflectBlend_BatchSimdMatchesScalar ()
	{
		var op = new UserBlendOps.ReflectBlendOp ();
		AssertBatchMatchesScalar (op, alpha1: 255, alpha2: 200);
	}

	[Test]
	public void SoftLightBlend_BatchSimdMatchesScalar ()
	{
		var op = new UserBlendOps.SoftLightBlendOp ();
		AssertBatchMatchesScalar (op, alpha1: 255, alpha2: 200);
	}

	// Test with partial alpha for new blend ops
	[Test]
	public void AdditiveBlend_PartialAlpha_MatchesScalar ()
	{
		var op = new UserBlendOps.AdditiveBlendOp ();
		AssertBatchMatchesScalar (op, alpha1: 180, alpha2: 120);
	}

	[Test]
	public void NegationBlend_PartialAlpha_MatchesScalar ()
	{
		var op = new UserBlendOps.NegationBlendOp ();
		AssertBatchMatchesScalar (op, alpha1: 180, alpha2: 120);
	}

	[Test]
	public void ColorBurnBlend_PartialAlpha_MatchesScalar ()
	{
		var op = new UserBlendOps.ColorBurnBlendOp ();
		AssertBatchMatchesScalar (op, alpha1: 180, alpha2: 120);
	}

	[Test]
	public void ColorDodgeBlend_PartialAlpha_MatchesScalar ()
	{
		var op = new UserBlendOps.ColorDodgeBlendOp ();
		AssertBatchMatchesScalar (op, alpha1: 180, alpha2: 120);
	}

	// ============================================================
	// Tests for BinaryPixelOp.Apply(dst, src) → delegates to 3-arg
	// ============================================================

	[Test]
	public void NormalBlend_TwoArgApply_UsesVectorizedPath ()
	{
		var op = new UserBlendOps.NormalBlendOp ();
		const int count = 8;
		var dst = new ColorBgra[count];
		var src = new ColorBgra[count];

		for (int i = 0; i < count; i++) {
			dst[i] = MakePremultiplied ((byte) (i * 30), (byte) (i * 25), (byte) (i * 20), 255);
			src[i] = MakePremultiplied ((byte) (255 - i * 30), (byte) (i * 15), (byte) (128 + i * 10), 200);
		}

		var expected = new ColorBgra[count];
		for (int i = 0; i < count; i++)
			expected[i] = op.Apply (dst[i], src[i]);

		op.Apply (dst, src);

		for (int i = 0; i < count; i++) {
			Assert.Multiple (() => {
				Assert.That (dst[i].B, Is.EqualTo (expected[i].B).Within (1), $"Pixel {i} B mismatch");
				Assert.That (dst[i].G, Is.EqualTo (expected[i].G).Within (1), $"Pixel {i} G mismatch");
				Assert.That (dst[i].R, Is.EqualTo (expected[i].R).Within (1), $"Pixel {i} R mismatch");
				Assert.That (dst[i].A, Is.EqualTo (expected[i].A).Within (1), $"Pixel {i} A mismatch");
			});
		}
	}
}
