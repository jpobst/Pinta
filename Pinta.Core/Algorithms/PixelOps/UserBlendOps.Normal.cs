using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Pinta.Core;

partial class UserBlendOps
{
	[Serializable]
	public sealed class NormalBlendOp : UserBlendOp
	{
		public static string StaticName
			=> "Normal";

		public override ColorBgra Apply (in ColorBgra lhs, in ColorBgra rhs)
			=> ApplyStatic (lhs, rhs);

		public static ColorBgra ApplyStatic (in ColorBgra bottom, in ColorBgra top)
		{
			if (top.A == 255) return top; // Top layer is fully opaque
			if (top.A == 0) return bottom; // Top layer is fully transparent

			return BlendOpHelper.ComputePremultiplied<ChannelBlend> (bottom, top);
		}

		public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> lhs, ReadOnlySpan<ColorBgra> rhs)
			=> ApplyLoop<NormalPixelBlend> (dst, lhs, rhs);

		/// <summary>
		/// Optimized SIMD implementation for Normal blend.
		/// Uses the simplified formula: result = top + DivBy255((255 - topAlpha) * bottom + 128),
		/// which stays entirely in ushort space (no widening to uint needed).
		/// </summary>
		private readonly struct NormalPixelBlend : BlendOpHelper.IPixelBlend
		{
			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static ColorBgra Apply (in ColorBgra lhs, in ColorBgra rhs)
				=> ApplyStatic (lhs, rhs);

			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static Vector128<byte> Apply (Vector128<byte> lhs, Vector128<byte> rhs)
			{
				// Shuffle mask: broadcast alpha byte to all 4 channels of each pixel
				// Input:  B0 G0 R0 A0 | B1 G1 R1 A1 | B2 G2 R2 A2 | B3 G3 R3 A3
				// Output: A0 A0 A0 A0 | A1 A1 A1 A1 | A2 A2 A2 A2 | A3 A3 A3 A3
				Vector128<byte> alphaMask = Vector128.Create (
					(byte) 3, 3, 3, 3, 7, 7, 7, 7, 11, 11, 11, 11, 15, 15, 15, 15);
				Vector128<ushort> v128 = Vector128.Create ((ushort) 128);
				Vector128<ushort> vOne = Vector128.Create ((ushort) 1);
				Vector128<ushort> v255u = Vector128.Create ((ushort) 255);
				Vector128<byte> v255b = Vector128.Create ((byte) 255);

				Vector128<byte> bottom = lhs;
				Vector128<byte> top = rhs;

				// Broadcast alpha from top to all channels within each pixel
				Vector128<byte> topAlpha = Vector128.Shuffle (top, alphaMask);

				// invTopAlpha = 255 - topAlpha
				Vector128<byte> invTopAlpha = v255b - topAlpha;

				// Widen bytes to ushorts for multiplication
				(Vector128<ushort> bottomLo, Vector128<ushort> bottomHi) = Vector128.Widen (bottom);
				(Vector128<ushort> topLo, Vector128<ushort> topHi) = Vector128.Widen (top);
				(Vector128<ushort> invAlphaLo, Vector128<ushort> invAlphaHi) = Vector128.Widen (invTopAlpha);

				// product = invTopAlpha * bottom + 128  (max: 255*255+128 = 65153, fits in ushort)
				Vector128<ushort> prodLo = invAlphaLo * bottomLo + v128;
				Vector128<ushort> prodHi = invAlphaHi * bottomHi + v128;

				// Exact integer division by 255: (x + (x >> 8) + 1) >> 8
				// Max intermediate: 65153 + 254 + 1 = 65408 (fits in ushort)
				Vector128<ushort> divLo = (prodLo + (prodLo >>> 8) + vOne) >>> 8;
				Vector128<ushort> divHi = (prodHi + (prodHi >>> 8) + vOne) >>> 8;

				// result = top + div, clamped to [0, 255] for non-premultiplied edge cases
				Vector128<ushort> resultLo = Vector128.Min (topLo + divLo, v255u);
				Vector128<ushort> resultHi = Vector128.Min (topHi + divHi, v255u);

				// Narrow back to bytes
				Vector128<byte> result = Vector128.Narrow (resultLo, resultHi);

				// Where topAlpha == 0, use bottom unchanged (matches scalar early exit)
				Vector128<byte> isTransparent = Vector128.Equals (topAlpha, Vector128<byte>.Zero);
				result = Vector128.ConditionalSelect (isTransparent, bottom, result);

				return result;
			}

			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static Vector256<byte> Apply (Vector256<byte> lhs, Vector256<byte> rhs)
				=> Vector256.Create (
					Apply (lhs.GetLower (), rhs.GetLower ()),
					Apply (lhs.GetUpper (), rhs.GetUpper ()));
		}

		private readonly struct ChannelBlend : BlendOpHelper.IChannelBlend
		{
			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static int BlendChannel (int Cb, int Ca, int Ab, int Aa)
				=> Ab * Ca;
		}
	}
}
