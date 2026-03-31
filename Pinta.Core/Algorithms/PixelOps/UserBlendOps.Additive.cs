using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Pinta.Core;

partial class UserBlendOps
{
	[Serializable]
	public sealed class AdditiveBlendOp : UserBlendOp
	{
		public static string StaticName
			=> "Additive";

		public override ColorBgra Apply (in ColorBgra bottom, in ColorBgra top)
			=> ApplyStatic (bottom, top);

		public static ColorBgra ApplyStatic (in ColorBgra bottom, in ColorBgra top)
		{
			// The Additive blend mode adds the color channels of the two layers together,
			// clamping to the maximum value.
			//
			// - Adding any color with black leaves the original color unchanged.
			// - Adding any color with white results in white.

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
				=> Math.Min (Aa * Ab, Aa * Cb + Ab * Ca);

			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static Vector128<ushort> BlendChannel (
				Vector128<ushort> Cb, Vector128<ushort> Ca,
				Vector128<ushort> Ab, Vector128<ushort> Aa)
			{
				// min(Aa*Ab, Aa*Cb + Ab*Ca)
				// The sum Aa*Cb + Ab*Ca can overflow ushort (max 130050).
				// On overflow, true sum > 65535 > 65025 >= Aa*Ab, so min = Aa*Ab.
				Vector128<ushort> prod1 = Aa * Cb;
				Vector128<ushort> sum = prod1 + Ab * Ca; // wrapping add
									 // If sum wrapped around (sum < prod1), set to max to ensure min picks Aa*Ab
				Vector128<ushort> overflow = Vector128.LessThan (sum, prod1).AsUInt16 ();
				sum |= overflow; // 0xFFFF where overflow, preserving non-overflow lanes
				return Vector128.Min (Aa * Ab, sum);
			}
		}
	}
}
