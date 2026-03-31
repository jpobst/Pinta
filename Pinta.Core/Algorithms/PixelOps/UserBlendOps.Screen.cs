using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Pinta.Core;

partial class UserBlendOps
{
	[Serializable]
	public sealed class ScreenBlendOp : UserBlendOp
	{
		public static string StaticName
			=> "Screen";

		public override ColorBgra Apply (in ColorBgra bottom, in ColorBgra top)
			=> ApplyStatic (bottom, top);

		public static ColorBgra ApplyStatic (in ColorBgra bottom, in ColorBgra top)
		{
			// The Screen blend mode is the inverse of the Multiply blend mode.
			// 
			// It results in a lighter color, akin to projecting multiple images
			// onto the same screen:
			//
			// - Screening any color with black leaves the original color unchanged.
			// - Screening any color with white results in white.

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
				=> Aa * Cb + Ab * Ca - Ca * Cb;

			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static Vector128<ushort> BlendChannel (
				Vector128<ushort> Cb, Vector128<ushort> Ca,
				Vector128<ushort> Ab, Vector128<ushort> Aa)
				// Intermediate sum Aa*Cb + Ab*Ca can exceed ushort, but the final
				// result (after subtracting Ca*Cb) fits in [0, 65025].
				// Wrapping ushort arithmetic produces the correct result because
				// the overflow cancels out in the subtraction.
				=> Aa * Cb + Ab * Ca - Ca * Cb;
		}
	}
}
