using System;
using System.Runtime.CompilerServices;

namespace Pinta.Foundation;

partial class UserBlendOps
{
	/// <summary>
	/// Negation blend mode: 255 - |255 - a - b| mapped to premultiplied space.
	/// In premultiplied: Aa*Ab - |Aa*Ab - 2*Ca*Cb|
	/// Uses scalar adapter since the formula can overflow ushort.
	/// </summary>
	[Serializable]
	public sealed class NegationBlendOp : UserBlendOp
	{
		public static string StaticName => "Negation";

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
				// Negation in premultiplied: Aa*Ab - |Aa*Ab - 2*Ca*Cb|
				int product = Aa * Ab;
				int inner = product - 2 * Ca * Cb;
				return product - Math.Abs (inner);
			}
		}
	}
}
