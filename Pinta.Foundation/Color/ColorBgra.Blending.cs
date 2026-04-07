/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) Rick Brewster, Tom Jackson, and past contributors.            //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
/////////////////////////////////////////////////////////////////////////////////

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Pinta.Foundation;

partial struct ColorBgra
{
	/// <summary>
	/// Linearly interpolates between two color values with premultiplied alpha.
	/// </summary>
	/// <param name="from">The color value that represents 0 on the lerp number line.</param>
	/// <param name="to">The color value that represents 255 on the lerp number line.</param>
	/// <param name="frac">A value in the range [0, 255].</param>
	public static ColorBgra Lerp (in ColorBgra from, in ColorBgra to, byte frac)
		=> FromBgra (
			b: Mathematics.LerpByte (from.B, to.B, frac),
			g: Mathematics.LerpByte (from.G, to.G, frac),
			r: Mathematics.LerpByte (from.R, to.R, frac),
			a: Mathematics.LerpByte (from.A, to.A, frac));

	/// <summary>
	/// Linearly interpolates between two color values with premultiplied alpha.
	/// </summary>
	/// <param name="from">The color value that represents 0 on the lerp number line.</param>
	/// <param name="to">The color value that represents 1 on the lerp number line.</param>
	/// <param name="frac">A value in the range [0, 1].</param>
	public static ColorBgra Lerp (in ColorBgra from, in ColorBgra to, float frac)
		=> FromBgra (
			b: ClampToByte (Mathematics.Lerp (from.B, to.B, frac)),
			g: ClampToByte (Mathematics.Lerp (from.G, to.G, frac)),
			r: ClampToByte (Mathematics.Lerp (from.R, to.R, frac)),
			a: ClampToByte (Mathematics.Lerp (from.A, to.A, frac)));

	/// <summary>
	/// Linearly interpolates between two color values with premultiplied alpha.
	/// </summary>
	/// <param name="from">The color value that represents 0 on the lerp number line.</param>
	/// <param name="to">The color value that represents 1 on the lerp number line.</param>
	/// <param name="frac">A value in the range [0, 1].</param>
	public static ColorBgra Lerp (in ColorBgra from, in ColorBgra to, double frac)
		=> FromBgra (
			b: ClampToByte (Mathematics.Lerp (from.B, to.B, frac)),
			g: ClampToByte (Mathematics.Lerp (from.G, to.G, frac)),
			r: ClampToByte (Mathematics.Lerp (from.R, to.R, frac)),
			a: ClampToByte (Mathematics.Lerp (from.A, to.A, frac)));

	/// <summary>
	/// SIMD-accelerated batch lerp: dst[i] = Lerp(from[i], to[i], frac).
	/// Processes 8 pixels at a time using Vector256, then 4 using Vector128.
	/// </summary>
	public static void LerpBatch (ReadOnlySpan<ColorBgra> from, ReadOnlySpan<ColorBgra> to, byte frac, Span<ColorBgra> dst)
	{
		int length = dst.Length;
		int i = 0;

		if (Vector256.IsHardwareAccelerated && length >= PixelBatch8.Count) {
			var fracVec256 = Vector256.Create ((ushort) frac);
			var invFracVec256 = Vector256.Create ((ushort) (255 - frac));
			int vectorEnd = length - (length % PixelBatch8.Count);
			for (; i < vectorEnd; i += PixelBatch8.Count) {
				var fromBatch = PixelBatch8.Load (from.Slice (i, PixelBatch8.Count));
				var toBatch = PixelBatch8.Load (to.Slice (i, PixelBatch8.Count));

				var fromLo = fromBatch.WidenLow ();
				var toLo = toBatch.WidenLow ();
				var resultLo = PixelBatch8.DivBy255 (fromLo * invFracVec256 + toLo * fracVec256);

				var fromHi = fromBatch.WidenHigh ();
				var toHi = toBatch.WidenHigh ();
				var resultHi = PixelBatch8.DivBy255 (fromHi * invFracVec256 + toHi * fracVec256);

				PixelBatch8.NarrowSaturate (resultLo, resultHi).Store (dst.Slice (i, PixelBatch8.Count));
			}
		}

		if (Vector128.IsHardwareAccelerated && length - i >= PixelBatch4.Count) {
			var fracVec = Vector128.Create ((ushort) frac);
			var invFracVec = Vector128.Create ((ushort) (255 - frac));
			int vectorEnd = i + ((length - i) / PixelBatch4.Count * PixelBatch4.Count);
			for (; i < vectorEnd; i += PixelBatch4.Count) {
				var fromBatch = PixelBatch4.Load (from.Slice (i, PixelBatch4.Count));
				var toBatch = PixelBatch4.Load (to.Slice (i, PixelBatch4.Count));

				var fromLo = fromBatch.WidenLow ();
				var toLo = toBatch.WidenLow ();
				var resultLo = PixelBatch4.DivBy255 (fromLo * invFracVec + toLo * fracVec);

				var fromHi = fromBatch.WidenHigh ();
				var toHi = toBatch.WidenHigh ();
				var resultHi = PixelBatch4.DivBy255 (fromHi * invFracVec + toHi * fracVec);

				PixelBatch4.NarrowSaturate (resultLo, resultHi).Store (dst.Slice (i, PixelBatch4.Count));
			}
		}

		for (; i < length; ++i)
			dst[i] = Lerp (from[i], to[i], frac);
	}

	/// <summary>
	/// Blends four premultiplied colors together based on the given weight values.
	/// </summary>
	/// <returns>The blended color.</returns>
	/// <remarks>
	/// The weights should be 16-bit fixed point numbers that add up to 65536 ("1.0").
	/// 4W16IP means "4 colors, weights, 16-bit integer precision"
	/// </remarks>
	public static ColorBgra BlendColors4W16IP (in ColorBgra c1, uint w1, in ColorBgra c2, uint w2, in ColorBgra c3, uint w3, in ColorBgra c4, uint w4)
	{
#if DEBUG
		if ((w1 + w2 + w3 + w4) != 65536)
			throw new ArgumentException ($"{nameof (w1)} + {nameof (w2)} + {nameof (w3)} + {nameof (w4)} must equal 65536!");
#endif

		const uint ww = 32768;
		uint r = (c1.R * w1 + c2.R * w2 + c3.R * w3 + c4.R * w4 + ww) >> 16;
		uint g = (c1.G * w1 + c2.G * w2 + c3.G * w3 + c4.G * w4 + ww) >> 16;
		uint b = (c1.B * w1 + c2.B * w2 + c3.B * w3 + c4.B * w4 + ww) >> 16;
		uint a = (c1.A * w1 + c2.A * w2 + c3.A * w3 + c4.A * w4 + ww) >> 16;

		return FromBgra ((byte) b, (byte) g, (byte) r, (byte) a);
	}

	/// <summary>
	/// Smoothly blends the given colors together, assuming equal weighting for each one.
	/// It is assumed that pre-multiplied alpha is used.
	/// </summary>
	public static ColorBgra Blend (ReadOnlySpan<ColorBgra> colors, ColorBgra fallback)
	{
		if (colors.Length == 0)
			return fallback;
		else
			return Blend (colors);
	}

	/// <summary>
	/// Smoothly blends the given colors together, assuming equal weighting for each one.
	/// Uses SIMD to accumulate channel sums: Vector256 (8 pixels) then Vector128 (4 pixels).
	/// It is assumed that pre-multiplied alpha is used.
	/// </summary>
	public static ColorBgra Blend (ReadOnlySpan<ColorBgra> colors)
	{
		int count = colors.Length;

		if (count == 0)
			throw new InvalidOperationException ($"{nameof (colors)} is empty");

		// For small counts, scalar is fine
		if (count < PixelBatch4.Count || !Vector128.IsHardwareAccelerated) {
			long totalB = 0, totalG = 0, totalR = 0, totalA = 0;
			for (int i = 0; i < count; i++) {
				totalB += colors[i].B;
				totalG += colors[i].G;
				totalR += colors[i].R;
				totalA += colors[i].A;
			}
			return FromBgra (
				ClampToByte ((int) (totalB / count)),
				ClampToByte ((int) (totalG / count)),
				ClampToByte ((int) (totalR / count)),
				ClampToByte ((int) (totalA / count)));
		}

		var sumLo = Vector128<uint>.Zero;
		var sumHi = Vector128<uint>.Zero;
		int i2 = 0;

		// Vector256 accumulation: process 8 pixels at a time
		if (Vector256.IsHardwareAccelerated && count >= PixelBatch8.Count) {
			var sum256Lo = Vector256<uint>.Zero;
			var sum256Hi = Vector256<uint>.Zero;
			int vectorEnd256 = count - (count % PixelBatch8.Count);
			for (; i2 < vectorEnd256; i2 += PixelBatch8.Count) {
				var batch = PixelBatch8.Load (colors.Slice (i2, PixelBatch8.Count));
				var lo = batch.WidenLow ();
				var hi = batch.WidenHigh ();
				(var loLo, var loHi) = Vector256.Widen (lo);
				sum256Lo += loLo;
				sum256Hi += loHi;
				(var hiLo, var hiHi) = Vector256.Widen (hi);
				sum256Lo += hiLo;
				sum256Hi += hiHi;
			}
			// Reduce 256-bit sums to 128-bit
			sumLo = sum256Lo.GetLower () + sum256Lo.GetUpper ();
			sumHi = sum256Hi.GetLower () + sum256Hi.GetUpper ();
		}

		// Vector128 accumulation: process 4 pixels at a time
		if (count - i2 >= PixelBatch4.Count) {
			int vectorEnd = i2 + ((count - i2) / PixelBatch4.Count * PixelBatch4.Count);
			for (; i2 < vectorEnd; i2 += PixelBatch4.Count) {
				var batch = PixelBatch4.Load (colors.Slice (i2, PixelBatch4.Count));
				var lo = batch.WidenLow ();
				var hi = batch.WidenHigh ();
				(var loLo, var loHi) = Vector128.Widen (lo);
				sumLo += loLo;
				sumHi += loHi;
				(var hiLo, var hiHi) = Vector128.Widen (hi);
				sumLo += hiLo;
				sumHi += hiHi;
			}
		}

		uint[] sums = new uint[8];
		sumLo.CopyTo (sums);
		sumHi.CopyTo (sums, 4);

		long totalB2 = sums[0] + sums[4];
		long totalG2 = sums[1] + sums[5];
		long totalR2 = sums[2] + sums[6];
		long totalA2 = sums[3] + sums[7];

		for (; i2 < count; i2++) {
			totalB2 += colors[i2].B;
			totalG2 += colors[i2].G;
			totalR2 += colors[i2].R;
			totalA2 += colors[i2].A;
		}

		return FromBgra (
			ClampToByte ((int) (totalB2 / count)),
			ClampToByte ((int) (totalG2 / count)),
			ClampToByte ((int) (totalR2 / count)),
			ClampToByte ((int) (totalA2 / count)));
	}

	/// <summary>
	/// Returns a new ColorBgra with an updated alpha component.
	/// <remarks>The color components are also adjusted since premultiplied alpha is used.</remarks>
	/// </summary>
	public readonly ColorBgra NewAlpha (byte newA)
	{
		ColorBgra sc = ToStraightAlpha ();
		return new ColorBgra (sc.B, sc.G, sc.R, newA).ToPremultipliedAlpha ();
	}

	/// <summary>
	/// Brings the color channels from straight alpha in premultiplied alpha form.
	/// This is required for direct memory manipulation when writing on pixel buffers
	/// that use the premultiplied alpha form.
	/// See:
	/// https://en.wikipedia.org/wiki/Alpha_compositing
	/// </summary>
	/// <returns>A ColorBgra value in premultiplied alpha form</returns>
	public readonly ColorBgra ToPremultipliedAlpha ()
		=> FromBgra ((byte) (B * A / 255), (byte) (G * A / 255), (byte) (R * A / 255), A);

	/// <summary>
	/// Brings the color channels from premultiplied alpha in straight alpha form.
	/// This is required for direct memory manipulation when reading from pixel buffers
	/// that use the premultiplied alpha form.
	/// Note: It is expected that the R,G,B-values are less or equal to the A-values (as it is always the case in premultiplied alpha form)
	/// See:
	/// https://en.wikipedia.org/wiki/Alpha_compositing
	/// </summary>
	/// <returns>A ColorBgra value in straight alpha form</returns>
	public readonly ColorBgra ToStraightAlpha ()
	{
		if (A > 0)
			return FromBgra ((byte) (B * 255 / A), (byte) (G * 255 / A), (byte) (R * 255 / A), A);
		else
			return Zero;
	}

	/// <summary>
	/// SIMD-accelerated batch conversion from straight alpha to premultiplied alpha.
	/// Uses Vector256 (8 pixels) then Vector128 (4 pixels).
	/// </summary>
	public static void ToPremultipliedAlphaBatch (ReadOnlySpan<ColorBgra> src, Span<ColorBgra> dst)
	{
		int length = src.Length;
		int i = 0;
		var alphaMask16 = Vector128.Create ((ushort) 0, 0, 0, 0xFFFF, 0, 0, 0, 0xFFFF);

		if (Vector256.IsHardwareAccelerated && length >= PixelBatch8.Count) {
			var alphaMask16_256 = Vector256.Create (
				(ushort) 0, 0, 0, 0xFFFF, 0, 0, 0, 0xFFFF,
				0, 0, 0, 0xFFFF, 0, 0, 0, 0xFFFF);
			int vectorEnd = length - (length % PixelBatch8.Count);
			for (; i < vectorEnd; i += PixelBatch8.Count) {
				var batch = PixelBatch8.Load (src.Slice (i, PixelBatch8.Count));
				var alpha = batch.BroadcastAlpha ();

				var lo = batch.WidenLow ();
				var alphaLo = alpha.WidenLow ();
				var premulLo = PixelBatch8.DivBy255 (lo * alphaLo);
				premulLo = Vector256.ConditionalSelect (alphaMask16_256, alphaLo, premulLo);

				var hi = batch.WidenHigh ();
				var alphaHi = alpha.WidenHigh ();
				var premulHi = PixelBatch8.DivBy255 (hi * alphaHi);
				premulHi = Vector256.ConditionalSelect (alphaMask16_256, alphaHi, premulHi);

				PixelBatch8.NarrowSaturate (premulLo, premulHi).Store (dst.Slice (i, PixelBatch8.Count));
			}
		}

		if (Vector128.IsHardwareAccelerated && length - i >= PixelBatch4.Count) {
			int vectorEnd = i + ((length - i) / PixelBatch4.Count * PixelBatch4.Count);
			for (; i < vectorEnd; i += PixelBatch4.Count) {
				var batch = PixelBatch4.Load (src.Slice (i, PixelBatch4.Count));
				var alpha = batch.BroadcastAlpha ();

				var lo = batch.WidenLow ();
				var alphaLo = alpha.WidenLow ();
				var premulLo = PixelBatch4.DivBy255 (lo * alphaLo);
				premulLo = Vector128.ConditionalSelect (alphaMask16, alphaLo, premulLo);

				var hi = batch.WidenHigh ();
				var alphaHi = alpha.WidenHigh ();
				var premulHi = PixelBatch4.DivBy255 (hi * alphaHi);
				premulHi = Vector128.ConditionalSelect (alphaMask16, alphaHi, premulHi);

				PixelBatch4.NarrowSaturate (premulLo, premulHi).Store (dst.Slice (i, PixelBatch4.Count));
			}
		}

		for (; i < length; ++i)
			dst[i] = src[i].ToPremultipliedAlpha ();
	}

	/// <summary>
	/// SIMD-accelerated batch computation of intensity bytes.
	/// Uses the formula: intensity = (7471 * B + 38470 * G + 19595 * R) >> 16
	/// </summary>
	public static void GetIntensityByteBatch (ReadOnlySpan<ColorBgra> src, Span<byte> dst)
	{
		int length = src.Length;
		for (int i = 0; i < length; ++i)
			dst[i] = src[i].GetIntensityByte ();
	}

	public readonly struct Aggregate
	{
		public int B { get; }
		public int G { get; }
		public int R { get; }
		public int A { get; }

		public Aggregate ()
		{
			B = 0;
			G = 0;
			R = 0;
			A = 0;
		}

		private Aggregate (int b, int g, int r, int a)
		{
			B = b;
			G = g;
			R = r;
			A = a;
		}

		public Aggregate ScaledAdd (in ColorBgra color, int scale)
		{
			return new (
				b: B + color.B * scale,
				g: G + color.G * scale,
				r: R + color.R * scale,
				a: A + color.A * scale);
		}

		public ColorBgra Clamp ()
		{
			return FromBgra (
				b: ClampToByte (B),
				g: ClampToByte (G),
				r: ClampToByte (R),
				a: ClampToByte (A));
		}

		public static Aggregate operator + (in Aggregate blender, in ColorBgra color)
		{
			return new (
				b: blender.B + color.B,
				g: blender.G + color.G,
				r: blender.R + color.R,
				a: blender.A + color.A);
		}
	}

	public readonly struct Blender
	{
		public Aggregate Aggregate { get; }
		public int Count { get; }

		public Blender ()
		{
			Aggregate = new ();
			Count = 0;
		}

		private Blender (in Aggregate aggregate, int count)
		{
			Aggregate = aggregate;
			Count = count;
		}

		public Blender WeightedAdd (in ColorBgra color, int weight)
		{
			return new (
				aggregate: Aggregate.ScaledAdd (color, weight),
				count: Count + weight);
		}

		public static Blender operator + (in Blender blender, in ColorBgra color)
		{
			return new (
				aggregate: blender.Aggregate + color,
				count: blender.Count + 1);
		}

		public ColorBgra Blend ()
		{
			if (Count == 0)
				throw new InvalidOperationException ("No colors to blend");

			return FromBgra (
				b: ClampToByte (Aggregate.B / Count),
				g: ClampToByte (Aggregate.G / Count),
				r: ClampToByte (Aggregate.R / Count),
				a: ClampToByte (Aggregate.A / Count));
		}
	}
}
