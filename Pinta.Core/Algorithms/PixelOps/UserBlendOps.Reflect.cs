using System;
using System.Runtime.CompilerServices;

namespace Pinta.Core;

partial class UserBlendOps
{
	[Serializable]
	public sealed class ReflectBlendOp : UserBlendOp
	{
		public static string StaticName
			=> "Reflect";

		public override ColorBgra Apply (in ColorBgra bottom, in ColorBgra top)
			=> ApplyStatic (bottom, top);

		public static ColorBgra ApplyStatic (in ColorBgra bottom, in ColorBgra top)
		{
			// The Reflect blend mode creates a reflection-like effect.
			//
			// - Reflecting with white produces white.
			// - Reflecting with black leaves the bottom layer unchanged.

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
				// Reflect: f(c'b, c'a) = min(1, c'b^2 / (1 - c'a))
				// Premultiplied: min(Aa*Ab, Cb*Cb*Aa*Aa / (Ab*(Aa - Ca)))
				if (Ca >= Aa) return Aa * Ab;
				long numerator = (long) Cb * Cb * Aa * Aa;
				int denominator = Ab * (Aa - Ca);
				return Math.Min (Aa * Ab, (int) (numerator / denominator));
			}
		}
	}
}
