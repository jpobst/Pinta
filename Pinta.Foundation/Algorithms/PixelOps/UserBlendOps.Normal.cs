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

		/// <summary>
		/// SIMD-accelerated batch blend with early-exit optimization.
		/// Detects fully opaque/transparent batches and uses fast copy paths.
		/// </summary>
		public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> lhs, ReadOnlySpan<ColorBgra> rhs)
		{
			ApplyLoop<SimdBlend> (dst, lhs, rhs);
		}

		private readonly struct ChannelBlend : BlendOpHelper.IChannelBlend
		{
			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static int BlendChannel (int Cb, int Ca, int Ab, int Aa)
				=> Ab * Ca;
		}

		/// <summary>
		/// SIMD implementation with early-exit: if all 4 pixels are opaque, copy top;
		/// if all transparent, copy bottom; otherwise full blend.
		/// </summary>
		private readonly struct SimdBlend : IPixelBlend
		{
			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static PixelBatch4 Blend4 (PixelBatch4 bottom, PixelBatch4 top)
			{
				// Early-exit: fully opaque top → just return top
				if (top.AllOpaque ())
					return top;

				// Early-exit: fully transparent top → just return bottom
				if (top.AllTransparent ())
					return bottom;

				// Full SIMD premultiplied blend
				return BlendOpHelper.PremultipliedBlend<VectorChannelBlend>.Blend4 (bottom, top);
			}
		}

		private readonly struct VectorChannelBlend : IVectorChannelBlend
		{
			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static Vector128<ushort> BlendChannels (
				Vector128<ushort> Cb, Vector128<ushort> Ca,
				Vector128<ushort> Ab, Vector128<ushort> Aa)
				=> Ab * Ca;
		}
	}
}
