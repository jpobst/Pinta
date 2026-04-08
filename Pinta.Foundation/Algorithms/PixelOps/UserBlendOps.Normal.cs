using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Pinta.Foundation;

partial class UserBlendOps
{
	[Serializable]
	public sealed class NormalBlendOp : UserBlendOp
	{
		public static string StaticName => "Normal";

		public override ColorBgra Apply (in ColorBgra lhs, in ColorBgra rhs)
			=> ApplyStatic (lhs, rhs);

		public static ColorBgra ApplyStatic (in ColorBgra bottom, in ColorBgra top)
		{
			if (top.A == 255) return top;
			if (top.A == 0) return bottom;

			return BlendOpHelper.ComputePremultiplied<ChannelBlend> (bottom, top);
		}

		public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> lhs, ReadOnlySpan<ColorBgra> rhs)
		{
			ApplyLoop<SimdBlend4, SimdBlend8> (dst, lhs, rhs);
		}

		private readonly struct ChannelBlend : BlendOpHelper.IChannelBlend
		{
			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static int BlendChannel (int Cb, int Ca, int Ab, int Aa)
				=> Ab * Ca;
		}

		private readonly struct SimdBlend4 : IPixelBlend
		{
			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static PixelBatch4 Blend4 (PixelBatch4 bottom, PixelBatch4 top)
			{
				if (top.AllOpaque ()) return top;
				if (top.AllTransparent ()) return bottom;
				return BlendOpHelper.PremultipliedBlend<VectorChannelBlend>.Blend4 (bottom, top);
			}
		}

		private readonly struct SimdBlend8 : IPixelBlend8
		{
			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static PixelBatch8 Blend8 (PixelBatch8 bottom, PixelBatch8 top)
			{
				if (top.AllOpaque ()) return top;
				if (top.AllTransparent ()) return bottom;
				return BlendOpHelper.PremultipliedBlend256<VectorChannelBlend>.Blend8 (bottom, top);
			}
		}

		private readonly struct VectorChannelBlend : IVectorChannelBlend
		{
			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static Vector128<ushort> BlendChannels (
				Vector128<ushort> Cb, Vector128<ushort> Ca,
				Vector128<ushort> Ab, Vector128<ushort> Aa)
				=> Ab * Ca;

			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static Vector256<ushort> BlendChannels256 (
				Vector256<ushort> Cb, Vector256<ushort> Ca,
				Vector256<ushort> Ab, Vector256<ushort> Aa)
				=> Ab * Ca;
		}
	}
}
