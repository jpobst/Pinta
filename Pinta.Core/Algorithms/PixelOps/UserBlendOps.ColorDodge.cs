using System;
using System.Runtime.CompilerServices;

namespace Pinta.Core;

partial class UserBlendOps
{
	[Serializable]
	public sealed class ColorDodgeBlendOp : UserBlendOp
	{
		public static string StaticName
			=> "ColorDodge";

		public override ColorBgra Apply (in ColorBgra bottom, in ColorBgra top)
			=> ApplyStatic (bottom, top);

		public static ColorBgra ApplyStatic (in ColorBgra bottom, in ColorBgra top)
		{
			// Color Dodge brightens the bottom layer to increase contrast.
			//
			// - Dodging with black leaves the image unchanged.
			// - Dodging with white produces white.

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
				// ColorDodge: f(c'b, c'a) = min(1, c'b / (1 - c'a))
				// Premultiplied: min(Aa*Ab, Aa*Aa*Cb / (Aa - Ca))
				if (Ca >= Aa) return Aa * Ab;
				return Math.Min (Aa * Ab, Aa * Aa * Cb / (Aa - Ca));
			}
		}
	}
}
