using System;
using System.Runtime.CompilerServices;

namespace Pinta.Core;

partial class UserBlendOps
{
	[Serializable]
	public sealed class ColorBurnBlendOp : UserBlendOp
	{
		public static string StaticName
			=> "ColorBurn";

		public override ColorBgra Apply (in ColorBgra bottom, in ColorBgra top)
			=> ApplyStatic (bottom, top);

		public static ColorBgra ApplyStatic (in ColorBgra bottom, in ColorBgra top)
		{
			// Color Burn darkens the bottom layer to increase contrast.
			//
			// - Burning with white leaves the image unchanged.
			// - Burning with black produces black.

			if (top.A == 0) return bottom;
			if (bottom.A == 0) return top;

			return BlendOpHelper.ComputePremultiplied<ChannelBlend> (bottom, top);
		}

		public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> lhs, ReadOnlySpan<ColorBgra> rhs)
			=> ApplyLoop<BlendOpHelper.ScalarPremultipliedBlend<ChannelBlend>> (dst, lhs, rhs);

		private readonly struct ChannelBlend : BlendOpHelper.IChannelBlend
		{
			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static int BlendChannel (int Cb, int Ca, int Ab, int Aa)
			{
				// ColorBurn: f(c'b, c'a) = max(0, 1 - (1-c'b)/c'a)
				// Premultiplied: max(0, Aa*Ab*Ca - Aa*Aa*(Ab-Cb)) / Ca
				if (Ca == 0) return 0;
				int numerator = Aa * Ab * Ca - Aa * Aa * (Ab - Cb);
				return Math.Max (0, numerator / Ca);
			}
		}
	}
}
