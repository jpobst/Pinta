using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Pinta.Foundation;

partial class UserBlendOps
{
	[Serializable]
	public sealed class DifferenceBlendOp : UserBlendOp
	{
		public static string StaticName => "Difference";

		public override ColorBgra Apply (in ColorBgra lhs, in ColorBgra rhs)
			=> ApplyStatic (lhs, rhs);

		public static ColorBgra ApplyStatic (in ColorBgra bottom, in ColorBgra top)
		{
			return BlendOpHelper.ComputePremultiplied<ChannelBlend> (bottom, top);
		}

		public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> lhs, ReadOnlySpan<ColorBgra> rhs)
			=> ApplyLoop<BlendOpHelper.PremultipliedBlend<VectorChannelBlend>,
				     BlendOpHelper.PremultipliedBlend256<VectorChannelBlend>> (dst, lhs, rhs);

		private readonly struct ChannelBlend : BlendOpHelper.IChannelBlend
		{
			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static int BlendChannel (int Cb, int Ca, int Ab, int Aa)
				=> Math.Abs (Cb * Aa - Ca * Ab);
		}

		private readonly struct VectorChannelBlend : IVectorChannelBlend
		{
			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static Vector128<ushort> BlendChannels (
				Vector128<ushort> Cb, Vector128<ushort> Ca,
				Vector128<ushort> Ab, Vector128<ushort> Aa)
			{
				var a = Cb * Aa;
				var b = Ca * Ab;
				var gte = Vector128.GreaterThanOrEqual (a, b);
				return Vector128.ConditionalSelect (gte, a - b, b - a);
			}

			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			public static Vector256<ushort> BlendChannels256 (
				Vector256<ushort> Cb, Vector256<ushort> Ca,
				Vector256<ushort> Ab, Vector256<ushort> Aa)
			{
				var a = Cb * Aa;
				var b = Ca * Ab;
				var gte = Vector256.GreaterThanOrEqual (a, b);
				return Vector256.ConditionalSelect (gte, a - b, b - a);
			}
		}
	}
}
