using System;
using System.Runtime.CompilerServices;

namespace Pinta.Foundation;

partial class UserBlendOps
{
	/// <summary>
	/// Reflect blend mode: Cb^2 / (1-Ca) (in [0,1] space).
	/// Division-based, uses scalar adapter for SIMD load/store batching.
	/// </summary>
	[Serializable]
	public sealed class ReflectBlendOp : UserBlendOp
	{
		public static string StaticName => "Reflect";

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
				// Reflect in premultiplied: min(Aa*Ab, Cb*Cb*Aa / (Aa-Ca))
				// When Ca == Aa (fully saturated top), result = Aa*Ab
				int denom = Aa - Ca;
				if (denom == 0) return Aa * Ab;
				return Math.Min (Aa * Ab, Cb * Cb * Aa / denom);
			}
		}
	}
}
