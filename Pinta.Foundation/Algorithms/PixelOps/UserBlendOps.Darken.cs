using System;
using System.Runtime.CompilerServices;

namespace Pinta.Foundation;

partial class UserBlendOps
{
	[Serializable]
	public sealed class DarkenBlendOp : UserBlendOp
	{
		public static string StaticName => "Darken";

		public override ColorBgra Apply (in ColorBgra bottom, in ColorBgra top)
			=> ApplyStatic (bottom, top);

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
				=> Math.Min (Ab * Ca, Aa * Cb);
		}
	}
}
