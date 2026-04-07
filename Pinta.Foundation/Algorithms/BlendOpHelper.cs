using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Pinta.Foundation;

internal static class BlendOpHelper
{
	// A 'separable' blend mode acts on each color channel independently
	//
	// The general formula for a separable blend mode **with premultiplied alpha** is:
	//
	// C_out = (1 - A_b) * C_a
	//       + (1 - A_a) * C_b
	//       + Blend(C_a, C_b)
	//
	// Where:
	//
	// - C refers to the premultiplied color channels (R, G, B)
	// - A refers to the alpha channel
	// - a refers to the top layer color (rhs)
	// - b refers to the bottom layer color (lhs)

	public interface IChannelBlend
	{
		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		static abstract int BlendChannel (int Cb, int Ca, int Ab, int Aa);
	}

	/// <summary>
	/// Scalar premultiplied alpha blend for a single pixel.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static ColorBgra ComputePremultiplied<TChannelBlend> (in ColorBgra bottom, in ColorBgra top)
		where TChannelBlend : IChannelBlend
	{
		int inverseTopAlpha = 255 - top.A;
		int inverseBottomAlpha = 255 - bottom.A;

		int topContributionB = inverseBottomAlpha * top.B;
		int topContributionG = inverseBottomAlpha * top.G;
		int topContributionR = inverseBottomAlpha * top.R;

		int bottomContributionB = inverseTopAlpha * bottom.B;
		int bottomContributionG = inverseTopAlpha * bottom.G;
		int bottomContributionR = inverseTopAlpha * bottom.R;

		int blendedB = TChannelBlend.BlendChannel (bottom.B, top.B, bottom.A, top.A);
		int blendedG = TChannelBlend.BlendChannel (bottom.G, top.G, bottom.A, top.A);
		int blendedR = TChannelBlend.BlendChannel (bottom.R, top.R, bottom.A, top.A);

		int preRoundingB = topContributionB + bottomContributionB + blendedB;
		int preRoundingG = topContributionG + bottomContributionG + blendedG;
		int preRoundingR = topContributionR + bottomContributionR + blendedR;

		const int ROUNDING_ADDEND = 128;

		byte outB = FoundationUtility.ClampToByte ((preRoundingB + ROUNDING_ADDEND) / 255);
		byte outG = FoundationUtility.ClampToByte ((preRoundingG + ROUNDING_ADDEND) / 255);
		byte outR = FoundationUtility.ClampToByte ((preRoundingR + ROUNDING_ADDEND) / 255);

		byte outA = FoundationUtility.ClampToByte (top.A + (bottom.A * inverseTopAlpha + ROUNDING_ADDEND) / 255);

		return ColorBgra.FromBgra (outB, outG, outR, outA);
	}

	// ============================================================================
	// SIMD Premultiplied Blend Infrastructure
	// ============================================================================

	/// <summary>
	/// Generic SIMD adapter that converts an IVectorChannelBlend into a full
	/// 4-pixel premultiplied alpha blend using Vector128.
	///
	/// This implements the full premultiplied alpha compositing formula:
	///   C_out = ((1-Ab)*Ca + (1-Aa)*Cb + Blend(Cb,Ca,Ab,Aa) + 128) / 255
	///   A_out = Aa + Ab*(1-Aa)/255
	/// </summary>
	internal readonly struct PremultipliedBlend<TBlend> : IPixelBlend
		where TBlend : IVectorChannelBlend
	{
		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static PixelBatch4 Blend4 (PixelBatch4 bottom, PixelBatch4 top)
		{
			var ones = Vector128.Create ((ushort) 255);

			// Process low 2 pixels (bytes 0-7)
			var botLo = bottom.WidenLow ();
			var topLo = top.WidenLow ();
			var botAlphaLo = WidenAlphaBroadcast (bottom.BroadcastAlpha ().WidenLow ());
			var topAlphaLo = WidenAlphaBroadcast (top.BroadcastAlpha ().WidenLow ());
			var invBotAlphaLo = ones - botAlphaLo;
			var invTopAlphaLo = ones - topAlphaLo;

			// C_out = (invBotAlpha*top + invTopAlpha*bot + Blend(bot,top,botA,topA) + 128) / 255
			var blendLo = TBlend.BlendChannels (botLo, topLo, botAlphaLo, topAlphaLo);
			var resultLo = invBotAlphaLo * topLo + invTopAlphaLo * botLo + blendLo;
			resultLo = PixelBatch4.DivBy255Rounded (resultLo);

			// Alpha: outA = topA + (botA * invTopA + 128) / 255
			var alphaOutLo = topAlphaLo + PixelBatch4.DivBy255Rounded (botAlphaLo * invTopAlphaLo);

			// Merge alpha into color result
			resultLo = BlendAlphaIntoResult (resultLo, alphaOutLo);

			// Process high 2 pixels (bytes 8-15)
			var botHi = bottom.WidenHigh ();
			var topHi = top.WidenHigh ();
			var botAlphaHi = WidenAlphaBroadcast (bottom.BroadcastAlpha ().WidenHigh ());
			var topAlphaHi = WidenAlphaBroadcast (top.BroadcastAlpha ().WidenHigh ());
			var invBotAlphaHi = ones - botAlphaHi;
			var invTopAlphaHi = ones - topAlphaHi;

			var blendHi = TBlend.BlendChannels (botHi, topHi, botAlphaHi, topAlphaHi);
			var resultHi = invBotAlphaHi * topHi + invTopAlphaHi * botHi + blendHi;
			resultHi = PixelBatch4.DivBy255Rounded (resultHi);

			var alphaOutHi = topAlphaHi + PixelBatch4.DivBy255Rounded (botAlphaHi * invTopAlphaHi);
			resultHi = BlendAlphaIntoResult (resultHi, alphaOutHi);

			return PixelBatch4.NarrowSaturate (resultLo, resultHi);
		}

		/// <summary>Ensures the alpha channel in the ushort vector is already broadcast.</summary>
		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		private static Vector128<ushort> WidenAlphaBroadcast (Vector128<ushort> v) => v;

		/// <summary>Replace the alpha channel in result with the computed alpha.</summary>
		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		private static Vector128<ushort> BlendAlphaIntoResult (Vector128<ushort> result, Vector128<ushort> alpha)
		{
			// Alpha positions are at indices 3 and 7 in the ushort vector
			// Since alpha was broadcast, we already have alpha at all positions
			// But we need to put the outAlpha only in the alpha slots
			var alphaMask = Vector128.Create ((ushort) 0, 0, 0, 0xFFFF, 0, 0, 0, 0xFFFF);
			return Vector128.ConditionalSelect (alphaMask, alpha, result);
		}
	}

	/// <summary>
	/// Adapter for blend ops that can't vectorize their BlendChannel (division-based).
	/// Uses scalar premultiplied blend per pixel but still gets called from the SIMD loop.
	/// </summary>
	internal readonly struct ScalarPremultipliedBlend<TChannelBlend> : IPixelBlend
		where TChannelBlend : IChannelBlend
	{
		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static PixelBatch4 Blend4 (PixelBatch4 bottom, PixelBatch4 top)
		{
			// Extract 4 pixels, blend each with the scalar path, repack
			var botData = bottom.Data.AsUInt32 ();
			var topData = top.Data.AsUInt32 ();

			var b0 = Unsafe.BitCast<uint, ColorBgra> (botData.GetElement (0));
			var t0 = Unsafe.BitCast<uint, ColorBgra> (topData.GetElement (0));
			var r0 = ComputePremultiplied<TChannelBlend> (b0, t0);

			var b1 = Unsafe.BitCast<uint, ColorBgra> (botData.GetElement (1));
			var t1 = Unsafe.BitCast<uint, ColorBgra> (topData.GetElement (1));
			var r1 = ComputePremultiplied<TChannelBlend> (b1, t1);

			var b2 = Unsafe.BitCast<uint, ColorBgra> (botData.GetElement (2));
			var t2 = Unsafe.BitCast<uint, ColorBgra> (topData.GetElement (2));
			var r2 = ComputePremultiplied<TChannelBlend> (b2, t2);

			var b3 = Unsafe.BitCast<uint, ColorBgra> (botData.GetElement (3));
			var t3 = Unsafe.BitCast<uint, ColorBgra> (topData.GetElement (3));
			var r3 = ComputePremultiplied<TChannelBlend> (b3, t3);

			return new PixelBatch4 (Vector128.Create (
				Unsafe.BitCast<ColorBgra, uint> (r0),
				Unsafe.BitCast<ColorBgra, uint> (r1),
				Unsafe.BitCast<ColorBgra, uint> (r2),
				Unsafe.BitCast<ColorBgra, uint> (r3)).AsByte ());
		}
	}
}
