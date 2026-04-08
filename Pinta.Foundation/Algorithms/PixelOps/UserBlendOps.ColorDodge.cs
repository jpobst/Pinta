using System;
using System.Runtime.CompilerServices;

namespace Pinta.Foundation;

partial class UserBlendOps
{
	/// <summary>
	/// ColorDodge blend mode: brightens the bottom layer to reflect the top layer.
	/// Formula: Cb / (1-Ca) (in [0,1] space), or Cb*255/(255-Ca) in byte space.
	/// Division-based, uses scalar adapter for SIMD load/store batching.
	/// </summary>
	[Serializable]
	public sealed class ColorDodgeBlendOp : UserBlendOp
	{
		public static string StaticName => "ColorDodge";

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
				// ColorDodge in premultiplied: min(Aa*Ab, Cb*Aa*Aa / (Aa-Ca))
				// When Ca == Aa (fully saturated), result = Aa*Ab
				int denom = Aa - Ca;
				if (denom == 0) return Aa * Ab;
				return Math.Min (Aa * Ab, Cb * Aa * Aa / denom);
			}
		}
	}
}
