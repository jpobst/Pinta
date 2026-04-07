using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Pinta.Foundation;

partial class UserBlendOps
{
	/// <summary>
	/// Hard Light blend mode: like Overlay but with the layers swapped.
	/// </summary>
	[Serializable]
	public sealed class HardLightBlendOp : UserBlendOp
	{
		public static string StaticName => "Hard Light";

		public override ColorBgra Apply (in ColorBgra bottom, in ColorBgra top)
			=> ApplyStatic (bottom, top);

		public static ColorBgra ApplyStatic (in ColorBgra bottom, in ColorBgra top)
		{
			if (top.A == 0) return bottom;
			if (bottom.A == 0) return top;

			return BlendOpHelper.ComputePremultiplied<ChannelBlend> (bottom, top);
		}

		public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> lhs, ReadOnlySpan<ColorBgra> rhs)
			=> ApplyLoop<BlendOpHelper.PremultipliedBlend<VectorChannelBlend>,
				     BlendOpHelper.PremultipliedBlend256<VectorChannelBlend>> (dst, lhs, rhs);

		private readonly struct ChannelBlend : BlendOpHelper.IChannelBlend
		{
			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static int BlendChannel (int Cb, int Ca, int Ab, int Aa)
			{
				// HardLight: if Ca < half, multiply; else screen (swapped from Overlay)
				if (Ca * 2 < Aa)
					return 2 * Ca * Cb;
				else
					return Aa * Ab - 2 * (Aa - Ca) * (Ab - Cb);
			}
		}

		private readonly struct VectorChannelBlend : IVectorChannelBlend
		{
			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static Vector128<ushort> BlendChannels (
				Vector128<ushort> Cb, Vector128<ushort> Ca,
				Vector128<ushort> Ab, Vector128<ushort> Aa)
			{
				var two = Vector128.Create ((ushort) 2);
				var multiply = two * Ca * Cb;
				var screen = Aa * Ab - two * (Aa - Ca) * (Ab - Cb);
				var condition = Vector128.LessThan (Ca * two, Aa);
				return Vector128.ConditionalSelect (condition, multiply, screen);
			}

			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static Vector256<ushort> BlendChannels256 (
				Vector256<ushort> Cb, Vector256<ushort> Ca,
				Vector256<ushort> Ab, Vector256<ushort> Aa)
			{
				var two = Vector256.Create ((ushort) 2);
				var multiply = two * Ca * Cb;
				var screen = Aa * Ab - two * (Aa - Ca) * (Ab - Cb);
				var condition = Vector256.LessThan (Ca * two, Aa);
				return Vector256.ConditionalSelect (condition, multiply, screen);
			}
		}
	}

	/// <summary>
	/// Soft Light blend mode: a gentler version of Hard Light.
	/// Uses the Pegtop formula. Division-based, uses scalar adapter.
	/// </summary>
	[Serializable]
	public sealed class SoftLightBlendOp : UserBlendOp
	{
		public static string StaticName => "Soft Light";

		public override ColorBgra Apply (in ColorBgra bottom, in ColorBgra top)
			=> ApplyStatic (bottom, top);

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
			{
				// Soft Light (Pegtop formula):
				// f(Cb, Ca) = (255 - 2*Ca) * Cb^2 / 255 + 2*Ca*Cb
				// All in premultiplied space
				if (Ab == 0 || Aa == 0) return 0;
				int twoCA = 2 * Ca;
				// result = (Ab - 2*Ca/Aa * Ab) * Cb^2 / (Ab*255) + 2*Ca/Aa * Cb
				// Simplified to avoid division: use Pegtop formula
				return (Ab * Aa - twoCA * Ab) * Cb / (255 * Aa) * Cb / Ab + twoCA * Cb / Aa;
			}
		}
	}

	/// <summary>
	/// Color blend mode: preserves the luma of the bottom and uses the hue+saturation of the top.
	/// </summary>
	[Serializable]
	public sealed class ColorBlendOp : UserBlendOp
	{
		public static string StaticName => "Color";

		public override ColorBgra Apply (in ColorBgra bottom, in ColorBgra top)
			=> ApplyStatic (bottom, top);

		public static ColorBgra ApplyStatic (in ColorBgra bottom, in ColorBgra top)
		{
			if (top.A == 0) return bottom;
			if (bottom.A == 0) return top;

			// Unpremultiply
			double bR = bottom.R, bG = bottom.G, bB = bottom.B;
			double tR = top.R, tG = top.G, tB = top.B;

			// Calculate luminance of bottom
			double lumB = 0.299 * bR + 0.587 * bG + 0.114 * bB;

			// Calculate luminance of top
			double lumT = 0.299 * tR + 0.587 * tG + 0.114 * tB;

			// Adjust top channels to match bottom luminance
			double diff = lumB - lumT;
			double fR = Math.Clamp (tR + diff, 0, 255);
			double fG = Math.Clamp (tG + diff, 0, 255);
			double fB = Math.Clamp (tB + diff, 0, 255);

			// Apply alpha compositing
			return AlphaComposite (bottom, top, (byte) fR, (byte) fG, (byte) fB);
		}
	}

	/// <summary>
	/// Luminosity blend mode: preserves the hue+saturation of the bottom and uses the luma of the top.
	/// </summary>
	[Serializable]
	public sealed class LuminosityBlendOp : UserBlendOp
	{
		public static string StaticName => "Luminosity";

		public override ColorBgra Apply (in ColorBgra bottom, in ColorBgra top)
			=> ApplyStatic (bottom, top);

		public static ColorBgra ApplyStatic (in ColorBgra bottom, in ColorBgra top)
		{
			if (top.A == 0) return bottom;
			if (bottom.A == 0) return top;

			double bR = bottom.R, bG = bottom.G, bB = bottom.B;
			double tR = top.R, tG = top.G, tB = top.B;

			double lumB = 0.299 * bR + 0.587 * bG + 0.114 * bB;
			double lumT = 0.299 * tR + 0.587 * tG + 0.114 * tB;

			double diff = lumT - lumB;
			double fR = Math.Clamp (bR + diff, 0, 255);
			double fG = Math.Clamp (bG + diff, 0, 255);
			double fB = Math.Clamp (bB + diff, 0, 255);

			return AlphaComposite (bottom, top, (byte) fR, (byte) fG, (byte) fB);
		}
	}

	/// <summary>
	/// Hue blend mode: preserves the luminosity and saturation of the bottom and uses the hue of the top.
	/// </summary>
	[Serializable]
	public sealed class HueBlendOp : UserBlendOp
	{
		public static string StaticName => "Hue";

		public override ColorBgra Apply (in ColorBgra bottom, in ColorBgra top)
			=> ApplyStatic (bottom, top);

		public static ColorBgra ApplyStatic (in ColorBgra bottom, in ColorBgra top)
		{
			if (top.A == 0) return bottom;
			if (bottom.A == 0) return top;

			HsvColor bHsv = HsvColor.FromBgra (bottom);
			HsvColor tHsv = HsvColor.FromBgra (top);

			// Use hue from top, saturation and value from bottom
			HsvColor result = new (tHsv.Hue, bHsv.Saturation, bHsv.Value);
			ColorBgra rgb = result.ToColorBgra ();

			return AlphaComposite (bottom, top, rgb.R, rgb.G, rgb.B);
		}
	}

	/// <summary>
	/// Saturation blend mode: preserves the luminosity and hue of the bottom and uses the saturation of the top.
	/// </summary>
	[Serializable]
	public sealed class SaturationBlendOp : UserBlendOp
	{
		public static string StaticName => "Saturation";

		public override ColorBgra Apply (in ColorBgra bottom, in ColorBgra top)
			=> ApplyStatic (bottom, top);

		public static ColorBgra ApplyStatic (in ColorBgra bottom, in ColorBgra top)
		{
			if (top.A == 0) return bottom;
			if (bottom.A == 0) return top;

			HsvColor bHsv = HsvColor.FromBgra (bottom);
			HsvColor tHsv = HsvColor.FromBgra (top);

			// Use saturation from top, hue and value from bottom
			HsvColor result = new (bHsv.Hue, tHsv.Saturation, bHsv.Value);
			ColorBgra rgb = result.ToColorBgra ();

			return AlphaComposite (bottom, top, rgb.R, rgb.G, rgb.B);
		}
	}

	/// <summary>
	/// Alpha-composite a blended result onto the bottom layer.
	/// </summary>
	private static ColorBgra AlphaComposite (in ColorBgra bottom, in ColorBgra top, byte fR, byte fG, byte fB)
	{
		int lhsA = bottom.A;
		int rhsA = top.A;
		int y = lhsA * (255 - rhsA) + 0x80;
		y = ((y >> 8) + y) >> 8;
		int totalA = y + rhsA;

		if (totalA == 0) return ColorBgra.FromUInt32 (0);

		int x = lhsA * rhsA + 0x80;
		x = ((x >> 8) + x) >> 8;
		int z = rhsA - x;

		int masIndex = totalA * 3;
		uint taM = FoundationUtility.MasTable[masIndex];
		uint taA = FoundationUtility.MasTable[masIndex + 1];
		uint taS = FoundationUtility.MasTable[masIndex + 2];

		uint b = (uint) ((((long) (bottom.B * y + top.B * z + fB * x) * taM) + taA) >> (int) taS);
		uint g = (uint) ((((long) (bottom.G * y + top.G * z + fG * x) * taM) + taA) >> (int) taS);
		uint r = (uint) ((((long) (bottom.R * y + top.R * z + fR * x) * taM) + taA) >> (int) taS);

		int a = lhsA * (255 - rhsA) + 0x80;
		a = ((a >> 8) + a) >> 8;
		a += rhsA;

		return ColorBgra.FromUInt32 (b + (g << 8) + (r << 16) + ((uint) a << 24));
	}
}
