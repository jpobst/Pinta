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
			var alphaMask = Vector128.Create ((ushort) 0, 0, 0, 0xFFFF, 0, 0, 0, 0xFFFF);

			// Process low 2 pixels (bytes 0-7)
			var botLo = bottom.WidenLow ();
			var topLo = top.WidenLow ();
			var botAlphaLo = bottom.BroadcastAlpha ().WidenLow ();
			var topAlphaLo = top.BroadcastAlpha ().WidenLow ();
			var invBotAlphaLo = ones - botAlphaLo;
			var invTopAlphaLo = ones - topAlphaLo;

			var blendLo = TBlend.BlendChannels (botLo, topLo, botAlphaLo, topAlphaLo);
			var resultLo = invBotAlphaLo * topLo + invTopAlphaLo * botLo + blendLo;
			resultLo = PixelBatch4.DivBy255Rounded (resultLo);

			var alphaOutLo = topAlphaLo + PixelBatch4.DivBy255Rounded (botAlphaLo * invTopAlphaLo);
			resultLo = Vector128.ConditionalSelect (alphaMask, alphaOutLo, resultLo);

			// Process high 2 pixels (bytes 8-15)
			var botHi = bottom.WidenHigh ();
			var topHi = top.WidenHigh ();
			var botAlphaHi = bottom.BroadcastAlpha ().WidenHigh ();
			var topAlphaHi = top.BroadcastAlpha ().WidenHigh ();
			var invBotAlphaHi = ones - botAlphaHi;
			var invTopAlphaHi = ones - topAlphaHi;

			var blendHi = TBlend.BlendChannels (botHi, topHi, botAlphaHi, topAlphaHi);
			var resultHi = invBotAlphaHi * topHi + invTopAlphaHi * botHi + blendHi;
			resultHi = PixelBatch4.DivBy255Rounded (resultHi);

			var alphaOutHi = topAlphaHi + PixelBatch4.DivBy255Rounded (botAlphaHi * invTopAlphaHi);
			resultHi = Vector128.ConditionalSelect (alphaMask, alphaOutHi, resultHi);

			return PixelBatch4.NarrowSaturate (resultLo, resultHi);
		}
	}

	/// <summary>
	/// Vector256 version of PremultipliedBlend: processes 8 pixels at a time.
	/// </summary>
	internal readonly struct PremultipliedBlend256<TBlend> : IPixelBlend8
		where TBlend : IVectorChannelBlend
	{
		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static PixelBatch8 Blend8 (PixelBatch8 bottom, PixelBatch8 top)
		{
			var ones = Vector256.Create ((ushort) 255);
			var alphaMask = Vector256.Create (
				(ushort) 0, 0, 0, 0xFFFF, 0, 0, 0, 0xFFFF,
				0, 0, 0, 0xFFFF, 0, 0, 0, 0xFFFF);

			// Process low 4 pixels
			var botLo = bottom.WidenLow ();
			var topLo = top.WidenLow ();
			var botAlphaLo = bottom.BroadcastAlpha ().WidenLow ();
			var topAlphaLo = top.BroadcastAlpha ().WidenLow ();
			var invBotAlphaLo = ones - botAlphaLo;
			var invTopAlphaLo = ones - topAlphaLo;

			var blendLo = TBlend.BlendChannels256 (botLo, topLo, botAlphaLo, topAlphaLo);
			var resultLo = invBotAlphaLo * topLo + invTopAlphaLo * botLo + blendLo;
			resultLo = PixelBatch8.DivBy255Rounded (resultLo);

			var alphaOutLo = topAlphaLo + PixelBatch8.DivBy255Rounded (botAlphaLo * invTopAlphaLo);
			resultLo = Vector256.ConditionalSelect (alphaMask, alphaOutLo, resultLo);

			// Process high 4 pixels
			var botHi = bottom.WidenHigh ();
			var topHi = top.WidenHigh ();
			var botAlphaHi = bottom.BroadcastAlpha ().WidenHigh ();
			var topAlphaHi = top.BroadcastAlpha ().WidenHigh ();
			var invBotAlphaHi = ones - botAlphaHi;
			var invTopAlphaHi = ones - topAlphaHi;

			var blendHi = TBlend.BlendChannels256 (botHi, topHi, botAlphaHi, topAlphaHi);
			var resultHi = invBotAlphaHi * topHi + invTopAlphaHi * botHi + blendHi;
			resultHi = PixelBatch8.DivBy255Rounded (resultHi);

			var alphaOutHi = topAlphaHi + PixelBatch8.DivBy255Rounded (botAlphaHi * invTopAlphaHi);
			resultHi = Vector256.ConditionalSelect (alphaMask, alphaOutHi, resultHi);

			return PixelBatch8.NarrowSaturate (resultLo, resultHi);
		}
	}

}
