using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Pinta.Foundation;

partial class UserBlendOps
{
	[Serializable]
	public sealed class LightenBlendOp : UserBlendOp
	{
		public static string StaticName => "Lighten";

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
				=> Math.Max (Ab * Ca, Aa * Cb);
		}

		private readonly struct VectorChannelBlend : IVectorChannelBlend
		{
			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static Vector128<ushort> BlendChannels (
				Vector128<ushort> Cb, Vector128<ushort> Ca,
				Vector128<ushort> Ab, Vector128<ushort> Aa)
				=> Vector128.Max (Ab * Ca, Aa * Cb);

			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static Vector256<ushort> BlendChannels256 (
				Vector256<ushort> Cb, Vector256<ushort> Ca,
				Vector256<ushort> Ab, Vector256<ushort> Aa)
				=> Vector256.Max (Ab * Ca, Aa * Cb);
		}
	}
}
