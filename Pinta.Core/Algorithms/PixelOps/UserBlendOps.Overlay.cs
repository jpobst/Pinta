using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Pinta.Core;

partial class UserBlendOps
{
	[Serializable]
	public sealed class OverlayBlendOp : UserBlendOp
	{
		public static string StaticName
			=> "Overlay";

		public override ColorBgra Apply (in ColorBgra bottom, in ColorBgra top)
			=> ApplyStatic (bottom, top);

		public static ColorBgra ApplyStatic (in ColorBgra bottom, in ColorBgra top)
		{
			// Overlay combines Multiply and Screen blend modes.
			// Where the bottom layer is dark, it multiplies; where it is light, it screens.
			//
			// - Overlaying with 50% gray leaves the image unchanged.
			// - Overlaying with black/white pushes toward black/white.

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
			{
				// In premultiplied space:
				// if 2*Cb < Ab (bottom is dark): multiply path = 2 * Cb * Ca
				// else (bottom is light): screen path = Aa*Ab - 2*(Ab-Cb)*(Aa-Ca)
				if (2 * Cb < Ab)
					return 2 * Cb * Ca;
				else
					return Aa * Ab - 2 * (Ab - Cb) * (Aa - Ca);
			}

			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static Vector128<ushort> BlendChannel (
				Vector128<ushort> Cb, Vector128<ushort> Ca,
				Vector128<ushort> Ab, Vector128<ushort> Aa)
			{
				// Both paths fit in ushort:
				// Multiply path: 2*Cb*Ca where Cb < Ab/2, so max = 2*127*255 = 64770
				// Screen path: Aa*Ab - 2*(Ab-Cb)*(Aa-Ca) where Ab-Cb ≤ Ab/2 ≤ 127
				Vector128<ushort> mulPath = (Cb + Cb) * Ca;
				Vector128<ushort> screenPath = Aa * Ab - ((Ab - Cb) + (Ab - Cb)) * (Aa - Ca);

				Vector128<ushort> isDark = Vector128.LessThan (Cb + Cb, Ab).AsUInt16 ();
				return Vector128.ConditionalSelect (isDark, mulPath, screenPath);
			}
		}
	}
}
