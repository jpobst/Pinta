using System;
using System.Runtime.CompilerServices;

namespace Pinta.Foundation;

partial class UserBlendOps
{
	[Serializable]
	public sealed class OverlayBlendOp : UserBlendOp
	{
		public static string StaticName => "Overlay";

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
				// Overlay: if Cb < half, multiply; else screen
				if (Cb * 2 < Ab)
					return 2 * Ca * Cb;
				else
					return Aa * Ab - 2 * (Ab - Cb) * (Aa - Ca);
			}
		}
	}
}
