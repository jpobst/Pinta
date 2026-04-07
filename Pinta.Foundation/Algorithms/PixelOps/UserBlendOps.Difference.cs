using System;
using System.Runtime.CompilerServices;

namespace Pinta.Foundation;

partial class UserBlendOps
{
	[Serializable]
	public sealed class DifferenceBlendOp : UserBlendOp
	{
		public static string StaticName => "Difference";

		public override ColorBgra Apply (in ColorBgra lhs, in ColorBgra rhs)
			=> ApplyStatic (lhs, rhs);

		public static ColorBgra ApplyStatic (in ColorBgra bottom, in ColorBgra top)
		{
			return BlendOpHelper.ComputePremultiplied<ChannelBlend> (bottom, top);
		}

		private readonly struct ChannelBlend : BlendOpHelper.IChannelBlend
		{
			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static int BlendChannel (int Cb, int Ca, int Ab, int Aa)
				=> Math.Abs (Cb * Aa - Ca * Ab);
		}
	}
}
