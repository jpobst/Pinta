using System;
using System.Runtime.CompilerServices;

namespace Pinta.Core;

partial class UserBlendOps
{
	[Serializable]
	public sealed class GlowBlendOp : UserBlendOp
	{
		public static string StaticName
			=> "Glow";

		public override ColorBgra Apply (in ColorBgra bottom, in ColorBgra top)
			=> ApplyStatic (bottom, top);

		public static ColorBgra ApplyStatic (in ColorBgra bottom, in ColorBgra top)
		{
			// The Glow blend mode is the inverse of Reflect (layers swapped).
			//
			// - Glowing with black leaves the image unchanged.
			// - Glowing with white (over non-white) produces white.

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
				// Glow: f(c'b, c'a) = min(1, c'a^2 / (1 - c'b))
				// Premultiplied: min(Aa*Ab, Ca*Ca*Ab*Ab / (Aa*(Ab - Cb)))
				if (Cb >= Ab) return Aa * Ab;
				long numerator = (long) Ca * Ca * Ab * Ab;
				int denominator = Aa * (Ab - Cb);
				return Math.Min (Aa * Ab, (int) (numerator / denominator));
			}
		}
	}
}
