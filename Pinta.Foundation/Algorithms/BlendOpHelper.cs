using System.Runtime.CompilerServices;

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
}
