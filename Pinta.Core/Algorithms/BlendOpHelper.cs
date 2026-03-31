using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Pinta.Core;

internal static class BlendOpHelper
{
	// A 'separable' blend mode acts on each color channel independently
	// 
	// The general formula for a separable blend mode **with premultiplied alpha** is:
	// 
	// C_out = (1 - A_b) * C_a
	//       + (1 - A_a) * C_b
	//       + Blend(C_a, C_b)
	// 
	// Where:
	// 
	// - C refers to the premultiplied color channels (R, G, B)
	// - A refers to the alpha channel
	// - a refers to the top layer color (rhs)
	// - b refers to the bottom layer color (lhs)
	// 
	// This helper is meant for blend ops that use bytes and integer arithmetic for efficiency.
	// These calculations achieve similar results to operations with channels ranging from 0 to 1,
	// except that these channels are being represented by bytes. That is, ranging from 0 to 255.
	// 
	// This is achieved by scaling the calculations by a factor of 255 with respect to their
	// "theoretical" counterparts, and then scaling everything back (see the `ROUNDING_ADDEND`
	// constant, which is a neat trick for using the truncation operator for rounding).

	/// <summary>
	/// Scalar channel blend interface. Implementations provide the blend-mode-specific
	/// per-channel computation.
	/// </summary>
	public interface IChannelBlend
	{
		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		static abstract int BlendChannel (int Cb, int Ca, int Ab, int Aa);
	}

	/// <summary>
	/// Extends <see cref="IChannelBlend"/> with a SIMD version of the channel blend
	/// operating on <see cref="Vector128{T}"/> of <see cref="ushort"/>.
	/// Each vector holds 8 channel values (2 pixels × 4 channels in BGRA order).
	/// Ab and Aa are broadcast to all channels within each pixel.
	/// The result of BlendChannel must fit in ushort (max 65535).
	/// </summary>
	public interface IVectorChannelBlend : IChannelBlend
	{
		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		static abstract Vector128<ushort> BlendChannel (
			Vector128<ushort> Cb, Vector128<ushort> Ca,
			Vector128<ushort> Ab, Vector128<ushort> Aa);
	}

	/// <summary>
	/// Pixel-level SIMD blend interface. Implementations provide the complete blend
	/// operation for packed pixel vectors (4 pixels per Vector128, 8 per Vector256).
	/// </summary>
	public interface IPixelBlend
	{
		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		static abstract ColorBgra Apply (in ColorBgra lhs, in ColorBgra rhs);

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		static abstract Vector128<byte> Apply (Vector128<byte> lhs, Vector128<byte> rhs);

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		static abstract Vector256<byte> Apply (Vector256<byte> lhs, Vector256<byte> rhs);
	}

	/// <summary>
	/// Adapter struct that wraps an <see cref="IVectorChannelBlend"/> into an <see cref="IPixelBlend"/>,
	/// using the general premultiplied alpha compositing formula for both scalar and SIMD paths.
	/// Includes early exits for fully transparent top/bottom pixels (matching the scalar blend ops).
	/// </summary>
	public readonly struct PremultipliedBlend<TChannel> : IPixelBlend
		where TChannel : struct, IVectorChannelBlend
	{
		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static ColorBgra Apply (in ColorBgra lhs, in ColorBgra rhs)
		{
			if (rhs.A == 0) return lhs;
			if (lhs.A == 0) return rhs;
			return ComputePremultiplied<TChannel> (lhs, rhs);
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static Vector128<byte> Apply (Vector128<byte> lhs, Vector128<byte> rhs)
			=> ComputePremultiplied128<TChannel> (lhs, rhs);

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static Vector256<byte> Apply (Vector256<byte> lhs, Vector256<byte> rhs)
			=> ComputePremultiplied256<TChannel> (lhs, rhs);
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static ColorBgra ComputePremultiplied<TChannelBlend> (in ColorBgra bottom, in ColorBgra top)
		where TChannelBlend : IChannelBlend
	{
		int inverseTopAlpha = 255 - top.A;
		int inverseBottomAlpha = 255 - bottom.A;

		int topContributionB = inverseBottomAlpha * top.B;
		int topContributionG = inverseBottomAlpha * top.G;
		int topContributionR = inverseBottomAlpha * top.R;

		int bottomContributionB = inverseTopAlpha * bottom.B;
		int bottomContributionG = inverseTopAlpha * bottom.G;
		int bottomContributionR = inverseTopAlpha * bottom.R;

		int blendedB = TChannelBlend.BlendChannel (bottom.B, top.B, bottom.A, top.A);
		int blendedG = TChannelBlend.BlendChannel (bottom.G, top.G, bottom.A, top.A);
		int blendedR = TChannelBlend.BlendChannel (bottom.R, top.R, bottom.A, top.A);

		int preRoundingB = topContributionB + bottomContributionB + blendedB;
		int preRoundingG = topContributionG + bottomContributionG + blendedG;
		int preRoundingR = topContributionR + bottomContributionR + blendedR;

		const int ROUNDING_ADDEND = 128;

		byte outB = Utility.ClampToByte ((preRoundingB + ROUNDING_ADDEND) / 255);
		byte outG = Utility.ClampToByte ((preRoundingG + ROUNDING_ADDEND) / 255);
		byte outR = Utility.ClampToByte ((preRoundingR + ROUNDING_ADDEND) / 255);

		byte outA = Utility.ClampToByte (top.A + (bottom.A * inverseTopAlpha + ROUNDING_ADDEND) / 255);

		return ColorBgra.FromBgra (outB, outG, outR, outA);
	}

	/// <summary>
	/// SIMD implementation of the premultiplied alpha compositing formula for 4 pixels
	/// packed in a <see cref="Vector128{T}"/> of bytes.
	/// Products are computed in ushort (each fits in 16 bits), then widened to uint for
	/// the sum to avoid overflow. DivBy255 uses the decomposition:
	/// <c>x / 255 = (x >> 8) + DivBy255_small((x >> 8) + (x &amp; 0xFF))</c>
	/// which is exact for x in [0, 195203].
	/// Alpha is computed separately using the standard formula.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static Vector128<byte> ComputePremultiplied128<TChannelBlend> (Vector128<byte> bottom, Vector128<byte> top)
		where TChannelBlend : IVectorChannelBlend
	{
		// Shuffle mask: broadcast alpha byte to all 4 channels of each pixel
		Vector128<byte> alphaMask = Vector128.Create (
			(byte) 3, 3, 3, 3, 7, 7, 7, 7, 11, 11, 11, 11, 15, 15, 15, 15);
		Vector128<byte> v255b = Vector128.Create ((byte) 255);
		Vector128<ushort> v255u = Vector128.Create ((ushort) 255);
		Vector128<ushort> v128u = Vector128.Create ((ushort) 128);
		Vector128<ushort> vOne = Vector128.Create ((ushort) 1);
		// Mask to select alpha channel positions (index 3 and 7 in each ushort half)
		Vector128<ushort> alphaSelect = Vector128.Create (
			(ushort) 0, 0, 0, 0xFFFF, 0, 0, 0, 0xFFFF);

		// Broadcast alpha from each pixel to all its channels
		Vector128<byte> topAlpha = Vector128.Shuffle (top, alphaMask);
		Vector128<byte> botAlpha = Vector128.Shuffle (bottom, alphaMask);
		Vector128<byte> invTopAlpha = v255b - topAlpha;
		Vector128<byte> invBotAlpha = v255b - botAlpha;

		// Widen to ushort (each half covers 2 pixels)
		(Vector128<ushort> botLo, Vector128<ushort> botHi) = Vector128.Widen (bottom);
		(Vector128<ushort> topLo, Vector128<ushort> topHi) = Vector128.Widen (top);
		(Vector128<ushort> invTopAlphaLo, Vector128<ushort> invTopAlphaHi) = Vector128.Widen (invTopAlpha);
		(Vector128<ushort> invBotAlphaLo, Vector128<ushort> invBotAlphaHi) = Vector128.Widen (invBotAlpha);
		(Vector128<ushort> topAlphaLo, Vector128<ushort> topAlphaHi) = Vector128.Widen (topAlpha);
		(Vector128<ushort> botAlphaLo, Vector128<ushort> botAlphaHi) = Vector128.Widen (botAlpha);

		// --- Process lo half (pixels 0-1) and hi half (pixels 2-3) ---
		Vector128<ushort> resultLo = ComputeHalf<TChannelBlend> (
			botLo, topLo, invTopAlphaLo, invBotAlphaLo, topAlphaLo, botAlphaLo,
			v255u, v128u, vOne, alphaSelect);
		Vector128<ushort> resultHi = ComputeHalf<TChannelBlend> (
			botHi, topHi, invTopAlphaHi, invBotAlphaHi, topAlphaHi, botAlphaHi,
			v255u, v128u, vOne, alphaSelect);

		// Narrow ushort → byte
		Vector128<byte> result = Vector128.Narrow (resultLo, resultHi);

		// Where topAlpha == 0, use bottom unchanged (handles non-premultiplied edge cases)
		Vector128<byte> isTopTransparent = Vector128.Equals (topAlpha, Vector128<byte>.Zero);
		result = Vector128.ConditionalSelect (isTopTransparent, bottom, result);

		// Where botAlpha == 0, use top unchanged
		Vector128<byte> isBotTransparent = Vector128.Equals (botAlpha, Vector128<byte>.Zero);
		result = Vector128.ConditionalSelect (isBotTransparent, top, result);

		return result;
	}

	/// <summary>
	/// Processes one half (2 pixels, 8 ushort channels) of the premultiplied alpha formula.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	private static Vector128<ushort> ComputeHalf<TChannelBlend> (
		Vector128<ushort> bot, Vector128<ushort> top,
		Vector128<ushort> invTopAlpha, Vector128<ushort> invBotAlpha,
		Vector128<ushort> topAlpha, Vector128<ushort> botAlpha,
		Vector128<ushort> v255u, Vector128<ushort> v128u, Vector128<ushort> vOne,
		Vector128<ushort> alphaSelect)
		where TChannelBlend : IVectorChannelBlend
	{
		// Products in ushort (each max 255*255 = 65025, fits in ushort)
		Vector128<ushort> topContrib = invBotAlpha * top;
		Vector128<ushort> botContrib = invTopAlpha * bot;
		Vector128<ushort> blend = TChannelBlend.BlendChannel (bot, top, botAlpha, topAlpha);

		// Widen to uint for safe addition (sum can exceed ushort max of 65535)
		(Vector128<uint> tc0, Vector128<uint> tc1) = Vector128.Widen (topContrib);
		(Vector128<uint> bc0, Vector128<uint> bc1) = Vector128.Widen (botContrib);
		(Vector128<uint> bl0, Vector128<uint> bl1) = Vector128.Widen (blend);

		Vector128<uint> v128_u32 = Vector128.Create ((uint) 128);
		Vector128<uint> vOne_u32 = Vector128.Create ((uint) 1);
		Vector128<uint> v0xFF_u32 = Vector128.Create ((uint) 0xFF);

		// Sum in uint + rounding addend
		Vector128<uint> sum0 = tc0 + bc0 + bl0 + v128_u32;
		Vector128<uint> sum1 = tc1 + bc1 + bl1 + v128_u32;

		// DivBy255 using decomposition: x/255 = (x>>8) + DivBy255_small((x>>8) + (x & 0xFF))
		// where DivBy255_small(y) = (y + (y>>8) + 1) >> 8, exact for y ≤ 65534
		// The decomposition is exact for x ≤ 195203 (covers our max of ~195203)
		Vector128<uint> colorRes0 = DivBy255Wide (sum0, v0xFF_u32, vOne_u32);
		Vector128<uint> colorRes1 = DivBy255Wide (sum1, v0xFF_u32, vOne_u32);

		// Narrow uint → ushort
		Vector128<ushort> colorResult = Vector128.Narrow (colorRes0, colorRes1);
		colorResult = Vector128.Min (colorResult, v255u);

		// Compute correct alpha: Aa + DivBy255((255-Aa) * Ab + 128)
		Vector128<ushort> alphaProduct = invTopAlpha * botAlpha + v128u;
		Vector128<ushort> alphaDiv = (alphaProduct + (alphaProduct >>> 8) + vOne) >>> 8;
		Vector128<ushort> correctAlpha = Vector128.Min (topAlpha + alphaDiv, v255u);

		// Replace alpha channels in result with correct alpha
		return Vector128.ConditionalSelect (alphaSelect, correctAlpha, colorResult);
	}

	/// <summary>
	/// Exact integer division by 255 for uint values up to 195203.
	/// Uses the decomposition: x/255 = (x >> 8) + ((x >> 8) + (x &amp; 0xFF) + (((x >> 8) + (x &amp; 0xFF)) >> 8) + 1) >> 8
	/// </summary>
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

	/// <summary>
	/// SIMD implementation for 8 pixels packed in a <see cref="Vector256{T}"/>.
	/// Splits into two <see cref="Vector128{T}"/> halves and processes each independently.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static Vector256<byte> ComputePremultiplied256<TChannelBlend> (Vector256<byte> bottom, Vector256<byte> top)
		where TChannelBlend : IVectorChannelBlend
	{
		return Vector256.Create (
			ComputePremultiplied128<TChannelBlend> (bottom.GetLower (), top.GetLower ()),
			ComputePremultiplied128<TChannelBlend> (bottom.GetUpper (), top.GetUpper ()));
	}
}
