using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Pinta.Foundation;

partial class UserBlendOps
{
	[Serializable]
	public sealed class OverlayBlendOp : UserBlendOp
	{
		public static string StaticName => "Overlay";

		public override ColorBgra Apply (in ColorBgra lhs, in ColorBgra rhs)
			=> ApplyStatic (lhs, rhs);

		public static ColorBgra ApplyStatic (in ColorBgra bottom, in ColorBgra top)
		{
			if (top.A == 0) return bottom;
			if (bottom.A == 0) return top;

			return BlendOpHelper.ComputePremultiplied<ChannelBlend> (bottom, top);
		}

		public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> lhs, ReadOnlySpan<ColorBgra> rhs)
			=> ApplyLoop<BlendOpHelper.PremultipliedBlend<VectorChannelBlend>> (dst, lhs, rhs);

		private readonly struct ChannelBlend : BlendOpHelper.IChannelBlend
		{
			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static int BlendChannel (int Cb, int Ca, int Ab, int Aa)
			{
				// Overlay: if Cb < half, multiply; else screen
				if (Cb * 2 < Ab)
					return 2 * Ca * Cb;
				else
					return Aa * Ab - 2 * (Ab - Cb) * (Aa - Ca);
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
				// Multiply path: 2 * Ca * Cb
				var multiply = two * Ca * Cb;
				// Screen path: Aa * Ab - 2 * (Ab - Cb) * (Aa - Ca)
				var screen = Aa * Ab - two * (Ab - Cb) * (Aa - Ca);
				// Condition: Cb * 2 < Ab
				var condition = Vector128.LessThan (Cb * two, Ab);
				return Vector128.ConditionalSelect (condition, multiply, screen);
			}
		}
	}
}
