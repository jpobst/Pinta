using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Pinta.Core;

partial class UserBlendOps
{
	[Serializable]
	public sealed class XorBlendOp : UserBlendOp
	{
		public static string StaticName
			=> "Xor";

		public override ColorBgra Apply (in ColorBgra lhs, in ColorBgra rhs)
			=> ApplyStatic (lhs, rhs);

		public static ColorBgra ApplyStatic (in ColorBgra bottom, in ColorBgra top)
		{
			// The Xor blend mode XORs the channel values of the two layers.
			// This is a bitwise operation that produces somewhat unpredictable visual results.

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
				// XOR operates on the raw premultiplied channel bytes.
				// In the premultiplied blend formula, the blend contribution
				// is weighted by Aa*Ab (the overlap region).
				=> (Cb ^ Ca) * Ab * Aa / 255;

			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static Vector128<ushort> BlendChannel (
				Vector128<ushort> Cb, Vector128<ushort> Ca,
				Vector128<ushort> Ab, Vector128<ushort> Aa)
			{
				// (Cb ^ Ca) * Ab * Aa / 255
				// Cb ^ Ca fits in byte [0,255], then * Ab * Aa max = 255*65025 overflows ushort.
				// We use the fact that (Cb ^ Ca) ≤ 255, Ab ≤ 255, Aa ≤ 255:
				// (Cb ^ Ca) * Ab ≤ 65025 fits in ushort, then * Aa / 255 via DivBy255.
				Vector128<ushort> xorVal = Cb ^ Ca;
				Vector128<ushort> product = xorVal * Ab; // max 65025, fits ushort
									 // DivBy255 of (product * Aa): we need (product * Aa + 128) / 255
									 // product * Aa max = 65025 * 255 = 16581375, needs uint
				(Vector128<uint> prodLo, Vector128<uint> prodHi) = Vector128.Widen (product);
				(Vector128<uint> aaLo, Vector128<uint> aaHi) = Vector128.Widen (Aa);
				Vector128<uint> mulLo = prodLo * aaLo;
				Vector128<uint> mulHi = prodHi * aaHi;
				// DivBy255: (x + 128) / 255 ≈ (x + (x >> 8) + 129) >> 8 for x ≤ 16581375
				Vector128<uint> v128u = Vector128.Create ((uint) 128);
				Vector128<uint> vOne = Vector128.Create ((uint) 1);
				Vector128<uint> v0xFF = Vector128.Create ((uint) 0xFF);
				Vector128<uint> resLo = DivBy255Wide (mulLo + v128u, v0xFF, vOne);
				Vector128<uint> resHi = DivBy255Wide (mulHi + v128u, v0xFF, vOne);
				return Vector128.Min (Vector128.Narrow (resLo, resHi), Vector128.Create ((ushort) 65025));
			}

			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			private static Vector128<uint> DivBy255Wide (
				Vector128<uint> x,
				Vector128<uint> v0xFF, Vector128<uint> vOne)
			{
				Vector128<uint> hi = x >>> 8;
				Vector128<uint> lo = x & v0xFF;
				Vector128<uint> inner = hi + lo;
				Vector128<uint> divInner = (inner + (inner >>> 8) + vOne) >>> 8;
				return hi + divInner;
			}
		}
	}
}
