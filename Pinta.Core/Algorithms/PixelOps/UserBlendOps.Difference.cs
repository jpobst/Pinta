using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Pinta.Core;

partial class UserBlendOps
{
	[Serializable]
	public sealed class DifferenceBlendOp : UserBlendOp
	{
		public static string StaticName
			=> "Difference";

		public override ColorBgra Apply (in ColorBgra lhs, in ColorBgra rhs)
			=> ApplyStatic (lhs, rhs);

		public static ColorBgra ApplyStatic (in ColorBgra bottom, in ColorBgra top)
		{
			// This blend mode subtracts the darker of the two colors from the lighter color
			// 
			// - Since all the components of black are zero, if one of the colors is black there is no change
			// - Since all the components of white are 255, the channels of the other color are "inverted" in a way

			return BlendOpHelper.ComputePremultiplied<ChannelBlend> (bottom, top);
		}

		public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> lhs, ReadOnlySpan<ColorBgra> rhs)
			=> ApplyLoop<BlendOpHelper.PremultipliedBlend<ChannelBlend>> (dst, lhs, rhs);

		private readonly struct ChannelBlend : BlendOpHelper.IVectorChannelBlend
		{
			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static int BlendChannel (int Cb, int Ca, int Ab, int Aa)
				=> Math.Abs (Cb * Aa - Ca * Ab);

			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static Vector128<ushort> BlendChannel (
				Vector128<ushort> Cb, Vector128<ushort> Ca,
				Vector128<ushort> Ab, Vector128<ushort> Aa)
			{
				// |Cb*Aa - Ca*Ab| = max(Cb*Aa, Ca*Ab) - min(Cb*Aa, Ca*Ab)
				Vector128<ushort> a = Cb * Aa;
				Vector128<ushort> b = Ca * Ab;
				return Vector128.Max (a, b) - Vector128.Min (a, b);
			}
		}
	}
}
