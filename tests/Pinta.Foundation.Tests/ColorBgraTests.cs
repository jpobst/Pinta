using System;
using NUnit.Framework;
using Pinta.Foundation;

namespace Pinta.Foundation.Tests;

[TestFixture]
public class ColorBgraTests
{
	[Test]
	public void FromBgra_CreatesCorrectColor ()
	{
		var color = ColorBgra.FromBgra (10, 20, 30, 255);
		Assert.Multiple (() => {
			Assert.That (color.B, Is.EqualTo (10));
			Assert.That (color.G, Is.EqualTo (20));
			Assert.That (color.R, Is.EqualTo (30));
			Assert.That (color.A, Is.EqualTo (255));
		});
	}

	[Test]
	public void FromBgr_SetsAlphaTo255 ()
	{
		var color = ColorBgra.FromBgr (50, 100, 150);
		Assert.That (color.A, Is.EqualTo (255));
	}

	[Test]
	public void FromUInt32_RoundTrips ()
	{
		uint value = 0xFF112233; // A=FF, R=11, G=22, B=33
		var color = ColorBgra.FromUInt32 (value);
		Assert.That (color.BGRA, Is.EqualTo (value));
	}

	[Test]
	public void NewAlpha_ChangesAlpha ()
	{
		var color = ColorBgra.FromBgra (10, 20, 30, 255);
		var newColor = color.NewAlpha (128);
		Assert.That (newColor.A, Is.EqualTo (128));
	}

	[Test]
	public void Indexer_AccessesChannels ()
	{
		var color = ColorBgra.FromBgra (10, 20, 30, 40);
		Assert.Multiple (() => {
			Assert.That (color[0], Is.EqualTo (10)); // B
			Assert.That (color[1], Is.EqualTo (20)); // G
			Assert.That (color[2], Is.EqualTo (30)); // R
			Assert.That (color[3], Is.EqualTo (40)); // A
		});
	}

	[Test]
	public void Predefined_Colors_AreCorrect ()
	{
		Assert.Multiple (() => {
			Assert.That (ColorBgra.Black.R, Is.EqualTo (0));
			Assert.That (ColorBgra.Black.G, Is.EqualTo (0));
			Assert.That (ColorBgra.Black.B, Is.EqualTo (0));
			Assert.That (ColorBgra.Black.A, Is.EqualTo (255));

			Assert.That (ColorBgra.White.R, Is.EqualTo (255));
			Assert.That (ColorBgra.White.G, Is.EqualTo (255));
			Assert.That (ColorBgra.White.B, Is.EqualTo (255));
			Assert.That (ColorBgra.White.A, Is.EqualTo (255));

			Assert.That (ColorBgra.Transparent.A, Is.EqualTo (0));
		});
	}

	[Test]
	public void GetIntensityByte_ReturnsWeightedAverage ()
	{
		var white = ColorBgra.White;
		Assert.That (white.GetIntensityByte (), Is.EqualTo (255));

		var black = ColorBgra.Black;
		Assert.That (black.GetIntensityByte (), Is.EqualTo (0));
	}

	// ============================================================
	// SIMD batch tests for ColorBgra
	// ============================================================

	[Test]
	public void LerpBatch_MatchesScalar ()
	{
		const int count = 8;
		var from = new ColorBgra[count];
		var to = new ColorBgra[count];
		byte frac = 128;

		for (int i = 0; i < count; i++) {
			from[i] = ColorBgra.FromBgra ((byte) (i * 30), (byte) (i * 25), (byte) (i * 20), 255);
			to[i] = ColorBgra.FromBgra ((byte) (255 - i * 30), (byte) (255 - i * 25), (byte) (255 - i * 20), 200);
		}

		var scalarResults = new ColorBgra[count];
		for (int i = 0; i < count; i++)
			scalarResults[i] = ColorBgra.Lerp (from[i], to[i], frac);

		var batchResults = new ColorBgra[count];
		ColorBgra.LerpBatch (from, to, frac, batchResults);

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
	public void ToPremultipliedAlphaBatch_MatchesScalar ()
	{
		const int count = 8;
		var src = new ColorBgra[count];
		for (int i = 0; i < count; i++)
			src[i] = ColorBgra.FromBgra ((byte) (i * 30), (byte) (i * 25 + 10), (byte) (i * 20 + 20), (byte) (i * 30));

		var scalarResults = new ColorBgra[count];
		for (int i = 0; i < count; i++)
			scalarResults[i] = src[i].ToPremultipliedAlpha ();

		var batchResults = new ColorBgra[count];
		ColorBgra.ToPremultipliedAlphaBatch (src, batchResults);

		for (int i = 0; i < count; i++) {
			Assert.Multiple (() => {
				Assert.That (batchResults[i].B, Is.EqualTo (scalarResults[i].B).Within (1), $"Pixel {i} B mismatch");
				Assert.That (batchResults[i].G, Is.EqualTo (scalarResults[i].G).Within (1), $"Pixel {i} G mismatch");
				Assert.That (batchResults[i].R, Is.EqualTo (scalarResults[i].R).Within (1), $"Pixel {i} R mismatch");
				Assert.That (batchResults[i].A, Is.EqualTo (scalarResults[i].A), $"Pixel {i} A mismatch");
			});
		}
	}

	[Test]
	public void Blend_Batch_ProducesAverage ()
	{
		var colors = new ColorBgra[] {
			ColorBgra.FromBgra (0, 0, 0, 255),
			ColorBgra.FromBgra (255, 255, 255, 255),
			ColorBgra.FromBgra (100, 100, 100, 255),
			ColorBgra.FromBgra (200, 200, 200, 255),
			ColorBgra.FromBgra (50, 50, 50, 255),
			ColorBgra.FromBgra (150, 150, 150, 255),
			ColorBgra.FromBgra (80, 80, 80, 255),
			ColorBgra.FromBgra (220, 220, 220, 255),
		};
		var result = ColorBgra.Blend (colors);
		// Average should be roughly 132
		Assert.That (result.B, Is.InRange (128, 135));
		Assert.That (result.A, Is.EqualTo (255));
	}

	// ============================================================
	// Unary Pixel Op SIMD tests
	// ============================================================

	[Test]
	public void Invert_BatchSimdMatchesScalar ()
	{
		var op = new UnaryPixelOps.Invert ();
		const int count = 8;
		var src = new ColorBgra[count];
		for (int i = 0; i < count; i++)
			src[i] = ColorBgra.FromBgra ((byte) (i * 20), (byte) (i * 15), (byte) (i * 10), (byte) (200 + i * 5));

		var scalarResults = new ColorBgra[count];
		for (int i = 0; i < count; i++)
			scalarResults[i] = op.Apply (src[i]);

		var batchResults = new ColorBgra[count];
		op.Apply ((Span<ColorBgra>) batchResults, (ReadOnlySpan<ColorBgra>) src);

		for (int i = 0; i < count; i++) {
			Assert.Multiple (() => {
				Assert.That (batchResults[i].B, Is.EqualTo (scalarResults[i].B), $"Pixel {i} B mismatch");
				Assert.That (batchResults[i].G, Is.EqualTo (scalarResults[i].G), $"Pixel {i} G mismatch");
				Assert.That (batchResults[i].R, Is.EqualTo (scalarResults[i].R), $"Pixel {i} R mismatch");
				Assert.That (batchResults[i].A, Is.EqualTo (scalarResults[i].A), $"Pixel {i} A mismatch");
			});
		}
	}

	[Test]
	public void InvertWithAlpha_BatchSimdMatchesScalar ()
	{
		var op = new UnaryPixelOps.InvertWithAlpha ();
		const int count = 8;
		var src = new ColorBgra[count];
		for (int i = 0; i < count; i++)
			src[i] = ColorBgra.FromBgra ((byte) (i * 30), (byte) (i * 25), (byte) (i * 20), (byte) (i * 30));

		var scalarResults = new ColorBgra[count];
		for (int i = 0; i < count; i++)
			scalarResults[i] = op.Apply (src[i]);

		var batchResults = new ColorBgra[count];
		op.Apply ((Span<ColorBgra>) batchResults, (ReadOnlySpan<ColorBgra>) src);

		for (int i = 0; i < count; i++) {
			Assert.Multiple (() => {
				Assert.That (batchResults[i].B, Is.EqualTo (scalarResults[i].B), $"Pixel {i} B");
				Assert.That (batchResults[i].G, Is.EqualTo (scalarResults[i].G), $"Pixel {i} G");
				Assert.That (batchResults[i].R, Is.EqualTo (scalarResults[i].R), $"Pixel {i} R");
				Assert.That (batchResults[i].A, Is.EqualTo (scalarResults[i].A), $"Pixel {i} A");
			});
		}
	}

	[Test]
	public void SetAlphaChannelTo255_BatchSimd ()
	{
		var op = new UnaryPixelOps.SetAlphaChannelTo255 ();
		const int count = 8;
		var src = new ColorBgra[count];
		for (int i = 0; i < count; i++)
			src[i] = ColorBgra.FromBgra ((byte) (i * 30), (byte) (i * 25), (byte) (i * 20), (byte) (i * 30));

		var dst = new ColorBgra[count];
		op.Apply ((Span<ColorBgra>) dst, (ReadOnlySpan<ColorBgra>) src);

		for (int i = 0; i < count; i++) {
			Assert.Multiple (() => {
				Assert.That (dst[i].B, Is.EqualTo (src[i].B));
				Assert.That (dst[i].G, Is.EqualTo (src[i].G));
				Assert.That (dst[i].R, Is.EqualTo (src[i].R));
				Assert.That (dst[i].A, Is.EqualTo (255));
			});
		}
	}

	[Test]
	public void Desaturate_BatchSimdMatchesScalar ()
	{
		var op = new UnaryPixelOps.Desaturate ();
		const int count = 8;
		var src = new ColorBgra[count];
		for (int i = 0; i < count; i++)
			src[i] = ColorBgra.FromBgra ((byte) (i * 20 + 10), (byte) (i * 15 + 20), (byte) (i * 25 + 5), 255);

		var scalarResults = new ColorBgra[count];
		for (int i = 0; i < count; i++)
			scalarResults[i] = op.Apply (src[i]);

		var batchResults = new ColorBgra[count];
		op.Apply ((Span<ColorBgra>) batchResults, (ReadOnlySpan<ColorBgra>) src);

		for (int i = 0; i < count; i++) {
			Assert.Multiple (() => {
				Assert.That (batchResults[i].B, Is.EqualTo (scalarResults[i].B), $"Pixel {i} B");
				Assert.That (batchResults[i].G, Is.EqualTo (scalarResults[i].G), $"Pixel {i} G");
				Assert.That (batchResults[i].R, Is.EqualTo (scalarResults[i].R), $"Pixel {i} R");
				Assert.That (batchResults[i].A, Is.EqualTo (scalarResults[i].A), $"Pixel {i} A");
			});
		}
	}

	[Test]
	public void Constant_BatchSimdFillsAll ()
	{
		var fillColor = ColorBgra.FromBgra (42, 84, 126, 200);
		var op = new UnaryPixelOps.Constant (fillColor);
		const int count = 8;
		var src = new ColorBgra[count];
		for (int i = 0; i < count; i++)
			src[i] = ColorBgra.FromBgra ((byte) i, (byte) (i * 2), (byte) (i * 3), 255);

		var dst = new ColorBgra[count];
		op.Apply ((Span<ColorBgra>) dst, (ReadOnlySpan<ColorBgra>) src);

		for (int i = 0; i < count; i++) {
			Assert.Multiple (() => {
				Assert.That (dst[i].B, Is.EqualTo (42), $"Pixel {i} B");
				Assert.That (dst[i].G, Is.EqualTo (84), $"Pixel {i} G");
				Assert.That (dst[i].R, Is.EqualTo (126), $"Pixel {i} R");
				Assert.That (dst[i].A, Is.EqualTo (200), $"Pixel {i} A");
			});
		}
	}
}
