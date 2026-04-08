using System;
using System.Runtime.CompilerServices;

namespace Pinta.Foundation;

partial class UserBlendOps
{
	/// <summary>
	/// Xor blend mode: bitwise XOR of pixel channels.
	/// In premultiplied space: requires unpremultiply, XOR, repremultiply.
	/// Uses scalar adapter since XOR is not a linear blend operation.
	/// </summary>
	[Serializable]
	public sealed class XorBlendOp : UserBlendOp
	{
		public static string StaticName => "Xor";

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
				// XOR in premultiplied space requires unpremultiply:
				// straight_b = Cb * 255 / Ab, straight_a = Ca * 255 / Aa
				// xor = straight_b ^ straight_a
				// result = xor * Ab * Aa / 255
				if (Ab == 0 || Aa == 0) return 0;
				int straightB = Cb * 255 / Ab;
				int straightA = Ca * 255 / Aa;
				return (straightB ^ straightA) * Ab * Aa / (255 * 255);
			}
		}
	}
}
