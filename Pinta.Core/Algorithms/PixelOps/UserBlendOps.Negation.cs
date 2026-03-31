using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Pinta.Core;

partial class UserBlendOps
{
	[Serializable]
	public sealed class NegationBlendOp : UserBlendOp
	{
		public static string StaticName
			=> "Negation";

		public override ColorBgra Apply (in ColorBgra bottom, in ColorBgra top)
			=> ApplyStatic (bottom, top);

		public static ColorBgra ApplyStatic (in ColorBgra bottom, in ColorBgra top)
		{
			// The Negation blend mode is similar to Difference, but produces a
			// softer result. It inverts the color differences rather than
			// taking absolute differences.

			if (top.A == 0) return bottom;
			if (bottom.A == 0) return top;

			return BlendOpHelper.ComputePremultiplied<ChannelBlend> (bottom, top);
		}

		public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> lhs, ReadOnlySpan<ColorBgra> rhs)
			=> ApplyLoop<BlendOpHelper.PremultipliedBlend<ChannelBlend>> (dst, lhs, rhs);

		private readonly struct ChannelBlend : BlendOpHelper.IVectorChannelBlend
		{
			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static int BlendChannel (int Cb, int Ca, int Ab, int Aa)
				=> Aa * Ab - Math.Abs (Aa * Ab - Aa * Cb - Ab * Ca);

			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static Vector128<ushort> BlendChannel (
				Vector128<ushort> Cb, Vector128<ushort> Ca,
				Vector128<ushort> Ab, Vector128<ushort> Aa)
			{
				// Aa*Ab - |Aa*Ab - (Aa*Cb + Ab*Ca)|
				// The sum Aa*Cb + Ab*Ca can exceed ushort, so widen to uint.
				Vector128<ushort> x = Aa * Ab;
				(Vector128<uint> xLo, Vector128<uint> xHi) = Vector128.Widen (x);
				(Vector128<uint> p1Lo, Vector128<uint> p1Hi) = Vector128.Widen (Aa * Cb);
				(Vector128<uint> p2Lo, Vector128<uint> p2Hi) = Vector128.Widen (Ab * Ca);

				Vector128<uint> yLo = p1Lo + p2Lo;
				Vector128<uint> yHi = p1Hi + p2Hi;

				// |x - y| via max - min
				Vector128<uint> absLo = Vector128.Max (xLo, yLo) - Vector128.Min (xLo, yLo);
				Vector128<uint> absHi = Vector128.Max (xHi, yHi) - Vector128.Min (xHi, yHi);

				// x - |x - y|, result fits in ushort [0, 65025]
				return Vector128.Narrow (xLo - absLo, xHi - absHi);
			}
		}
	}
}
