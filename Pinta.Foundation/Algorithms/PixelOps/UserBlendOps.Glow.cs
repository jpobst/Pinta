using System;
using System.Runtime.CompilerServices;

namespace Pinta.Foundation;

partial class UserBlendOps
{
	/// <summary>
	/// Glow blend mode: Ca^2 / (1-Cb) (in [0,1] space).
	/// Division-based, uses scalar adapter for SIMD load/store batching.
	/// </summary>
	[Serializable]
	public sealed class GlowBlendOp : UserBlendOp
	{
		public static string StaticName => "Glow";

		public override ColorBgra Apply (in ColorBgra lhs, in ColorBgra rhs)
			=> ApplyStatic (lhs, rhs);

		public static ColorBgra ApplyStatic (in ColorBgra bottom, in ColorBgra top)
		{
			if (top.A == 0) return bottom;
			if (bottom.A == 0) return top;

			return BlendOpHelper.ComputePremultiplied<ChannelBlend> (bottom, top);
		}

		private readonly struct ChannelBlend : BlendOpHelper.IChannelBlend
		{
			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static int BlendChannel (int Cb, int Ca, int Ab, int Aa)
			{
				// Glow in premultiplied: min(Aa*Ab, Ca*Ca*Ab / (Ab-Cb))
				// When Cb == Ab (fully saturated bottom), result = Aa*Ab
				int denom = Ab - Cb;
				if (denom == 0) return Aa * Ab;
				return Math.Min (Aa * Ab, Ca * Ca * Ab / denom);
			}
		}
	}
}
