using System;
using System.Runtime.CompilerServices;

namespace Pinta.Foundation;

partial class UserBlendOps
{
	/// <summary>
	/// ColorBurn blend mode: darkens the bottom layer to reflect the top layer.
	/// Formula: 1 - (1-Cb)/Ca (in [0,1] space), or 255 - (255-Cb)*255/Ca in byte space.
	/// Division-based, uses scalar adapter for SIMD load/store batching.
	/// </summary>
	[Serializable]
	public sealed class ColorBurnBlendOp : UserBlendOp
	{
		public static string StaticName => "ColorBurn";

		public override ColorBgra Apply (in ColorBgra lhs, in ColorBgra rhs)
			=> ApplyStatic (lhs, rhs);

		public static ColorBgra ApplyStatic (in ColorBgra bottom, in ColorBgra top)
		{
			if (top.A == 0) return bottom;
			if (bottom.A == 0) return top;

			return BlendOpHelper.ComputePremultiplied<ChannelBlend> (bottom, top);
		}

		public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> lhs, ReadOnlySpan<ColorBgra> rhs)
			=> ApplyLoop<BlendOpHelper.ScalarPremultipliedBlend<ChannelBlend>,
				     BlendOpHelper.ScalarPremultipliedBlend256<ChannelBlend>> (dst, lhs, rhs);

		private readonly struct ChannelBlend : BlendOpHelper.IChannelBlend
		{
			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static int BlendChannel (int Cb, int Ca, int Ab, int Aa)
			{
				// ColorBurn in premultiplied: Aa*Ab - min(Aa*Ab, (Ab-Cb)*Aa*Aa/Ca)
				// When Ca == 0, result is 0
				if (Ca == 0) return 0;
				int product = Aa * Ab;
				int burn = (Ab - Cb) * Aa * Aa / Ca;
				return Math.Max (0, product - burn);
			}
		}
	}
}
