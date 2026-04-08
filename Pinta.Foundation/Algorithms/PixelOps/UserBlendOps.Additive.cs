using System;
using System.Runtime.CompilerServices;

namespace Pinta.Foundation;

partial class UserBlendOps
{
	/// <summary>
	/// Additive blend mode (also known as Linear Dodge).
	/// In premultiplied space: saturating add clamped to combined alpha.
	/// Uses scalar adapter since the additive formula can overflow ushort.
	/// </summary>
	[Serializable]
	public sealed class AdditiveBlendOp : UserBlendOp
	{
		public static string StaticName => "Additive";

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
				// Additive: sum of both contributions, clamped to combined alpha product
				=> Math.Min (Ab * Ca + Aa * Cb, Aa * Ab);
		}
	}
}
