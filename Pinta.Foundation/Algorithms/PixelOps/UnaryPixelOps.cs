/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) Rick Brewster, Tom Jackson, and past contributors.            //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
/////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Pinta.Foundation;

/// <summary>
/// Provides a set of standard UnaryPixelOps.
/// </summary>
public static class UnaryPixelOps
{
	/// <summary>
	/// Passes through the given color value.
	/// result(color) = color
	/// </summary>
	[Serializable]
	public sealed class Identity : UnaryPixelOp
	{
		public override ColorBgra Apply (in ColorBgra color)
			=> color;

		public override void Apply (Span<ColorBgra> dst) { }
	}

	/// <summary>
	/// Always returns a constant color.
	/// SIMD: broadcasts the constant across all pixels (Vector128 and Vector256).
	/// </summary>
	[Serializable]
	public sealed class Constant : UnaryPixelOp
	{
		private readonly ColorBgra set_color;
		private readonly Vector128<byte> simd_color;
		private readonly Vector256<byte> simd_color256;

		public override ColorBgra Apply (in ColorBgra color)
			=> set_color;

		public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> src)
		{
			int i = 0;
			if (Vector256.IsHardwareAccelerated && dst.Length >= PixelBatch8.Count) {
				int vectorEnd = dst.Length - (dst.Length % PixelBatch8.Count);
				for (; i < vectorEnd; i += PixelBatch8.Count)
					new PixelBatch8 (simd_color256).Store (dst.Slice (i, PixelBatch8.Count));
			}
			if (Vector128.IsHardwareAccelerated && dst.Length - i >= PixelBatch4.Count) {
				int vectorEnd = i + ((dst.Length - i) / PixelBatch4.Count * PixelBatch4.Count);
				for (; i < vectorEnd; i += PixelBatch4.Count)
					new PixelBatch4 (simd_color).Store (dst.Slice (i, PixelBatch4.Count));
			}
			for (; i < dst.Length; ++i)
				dst[i] = set_color;
		}

		public override void Apply (Span<ColorBgra> dst)
		{
			int i = 0;
			if (Vector256.IsHardwareAccelerated && dst.Length >= PixelBatch8.Count) {
				int vectorEnd = dst.Length - (dst.Length % PixelBatch8.Count);
				for (; i < vectorEnd; i += PixelBatch8.Count)
					new PixelBatch8 (simd_color256).Store (dst.Slice (i, PixelBatch8.Count));
			}
			if (Vector128.IsHardwareAccelerated && dst.Length - i >= PixelBatch4.Count) {
				int vectorEnd = i + ((dst.Length - i) / PixelBatch4.Count * PixelBatch4.Count);
				for (; i < vectorEnd; i += PixelBatch4.Count)
					new PixelBatch4 (simd_color).Store (dst.Slice (i, PixelBatch4.Count));
			}
			for (; i < dst.Length; ++i)
				dst[i] = set_color;
		}

		public Constant (ColorBgra setColor)
		{
			set_color = setColor;
			uint bgra = setColor.BGRA;
			simd_color = Vector128.Create (bgra, bgra, bgra, bgra).AsByte ();
			simd_color256 = Vector256.Create (bgra, bgra, bgra, bgra, bgra, bgra, bgra, bgra).AsByte ();
		}
	}

	/// <summary>
	/// Blends pixels with the specified constant color.
	/// SIMD: vectorized lerp with precomputed constant factors (Vector128/Vector256).
	/// </summary>
	[Serializable]
	public sealed class BlendConstant : UnaryPixelOp
	{
		private readonly ColorBgra blend_color;
		private readonly Vector128<ushort> simd_blend_lo;
		private readonly Vector128<ushort> simd_invA;
		private readonly Vector256<ushort> simd_blend_lo256;
		private readonly Vector256<ushort> simd_invA256;
		private readonly byte blend_alpha;

		public override ColorBgra Apply (in ColorBgra color)
		{
			int a = blend_color.A;
			int invA = 255 - a;

			int r = ((color.R * invA) + (blend_color.R * a)) / 256;
			int g = ((color.G * invA) + (blend_color.G * a)) / 256;
			int b = ((color.B * invA) + (blend_color.B * a)) / 256;
			byte a2 = ComputeAlpha (color.A, blend_color.A);

			return ColorBgra.FromBgra ((byte) b, (byte) g, (byte) r, a2);
		}

		public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> src)
		{
			int i = 0;

			if (Vector256.IsHardwareAccelerated && dst.Length >= PixelBatch8.Count) {
				int vectorEnd = dst.Length - (dst.Length % PixelBatch8.Count);
				for (; i < vectorEnd; i += PixelBatch8.Count) {
					var batch = PixelBatch8.Load (src.Slice (i, PixelBatch8.Count));
					BlendBatch8 (batch).Store (dst.Slice (i, PixelBatch8.Count));
				}
			}

			if (Vector128.IsHardwareAccelerated && dst.Length - i >= PixelBatch4.Count) {
				int vectorEnd = i + ((dst.Length - i) / PixelBatch4.Count * PixelBatch4.Count);
				for (; i < vectorEnd; i += PixelBatch4.Count) {
					var batch = PixelBatch4.Load (src.Slice (i, PixelBatch4.Count));
					BlendBatch4 (batch).Store (dst.Slice (i, PixelBatch4.Count));
				}
			}

			for (; i < dst.Length; ++i)
				dst[i] = Apply (src[i]);
		}

		public override void Apply (Span<ColorBgra> dst)
		{
			int i = 0;

			if (Vector256.IsHardwareAccelerated && dst.Length >= PixelBatch8.Count) {
				int vectorEnd = dst.Length - (dst.Length % PixelBatch8.Count);
				for (; i < vectorEnd; i += PixelBatch8.Count) {
					var slice = dst.Slice (i, PixelBatch8.Count);
					BlendBatch8 (PixelBatch8.Load (slice)).Store (slice);
				}
			}

			if (Vector128.IsHardwareAccelerated && dst.Length - i >= PixelBatch4.Count) {
				int vectorEnd = i + ((dst.Length - i) / PixelBatch4.Count * PixelBatch4.Count);
				for (; i < vectorEnd; i += PixelBatch4.Count) {
					var slice = dst.Slice (i, PixelBatch4.Count);
					BlendBatch4 (PixelBatch4.Load (slice)).Store (slice);
				}
			}

			for (; i < dst.Length; ++i)
				dst[i] = Apply (dst[i]);
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		private PixelBatch4 BlendBatch4 (PixelBatch4 batch)
		{
			// Process low 2 pixels
			var colorLo = batch.WidenLow ();
			var resultLo = (colorLo * simd_invA + simd_blend_lo) >>> 8;
			// Fix alpha: compute using ComputeAlpha formula
			var alphaLo = ComputeAlphaVector128 (colorLo);

			// Process high 2 pixels
			var colorHi = batch.WidenHigh ();
			var resultHi = (colorHi * simd_invA + simd_blend_lo) >>> 8;
			var alphaHi = ComputeAlphaVector128 (colorHi);

			var alphaMask = Vector128.Create ((ushort) 0, 0, 0, 0xFFFF, 0, 0, 0, 0xFFFF);
			resultLo = Vector128.ConditionalSelect (alphaMask, alphaLo, resultLo);
			resultHi = Vector128.ConditionalSelect (alphaMask, alphaHi, resultHi);

			return PixelBatch4.NarrowSaturate (resultLo, resultHi);
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		private PixelBatch8 BlendBatch8 (PixelBatch8 batch)
		{
			var colorLo = batch.WidenLow ();
			var resultLo = (colorLo * simd_invA256 + simd_blend_lo256) >>> 8;
			var alphaLo = ComputeAlphaVector256 (colorLo);

			var colorHi = batch.WidenHigh ();
			var resultHi = (colorHi * simd_invA256 + simd_blend_lo256) >>> 8;
			var alphaHi = ComputeAlphaVector256 (colorHi);

			var alphaMask = Vector256.Create (
				(ushort) 0, 0, 0, 0xFFFF, 0, 0, 0, 0xFFFF,
				0, 0, 0, 0xFFFF, 0, 0, 0, 0xFFFF);
			resultLo = Vector256.ConditionalSelect (alphaMask, alphaLo, resultLo);
			resultHi = Vector256.ConditionalSelect (alphaMask, alphaHi, resultHi);

			return PixelBatch8.NarrowSaturate (resultLo, resultHi);
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		private Vector128<ushort> ComputeAlphaVector128 (Vector128<ushort> colorWidened)
		{
			// ComputeAlpha formula: (la * (256 - (ra + (ra >> 7)))) >> 8 + ra
			var ra = Vector128.Create ((ushort) blend_alpha);
			var la = colorWidened; // alpha is at positions 3, 7
			var raMask = ra + Vector128.ShiftRightLogical (ra, 7);
			var c256 = Vector128.Create ((ushort) 256);
			var result = Vector128.ShiftRightLogical (la * (c256 - raMask), 8) + ra;
			return result;
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		private Vector256<ushort> ComputeAlphaVector256 (Vector256<ushort> colorWidened)
		{
			var ra = Vector256.Create ((ushort) blend_alpha);
			var la = colorWidened;
			var raMask = ra + Vector256.ShiftRightLogical (ra, 7);
			var c256 = Vector256.Create ((ushort) 256);
			var result = Vector256.ShiftRightLogical (la * (c256 - raMask), 8) + ra;
			return result;
		}

		public BlendConstant (ColorBgra blendColor)
		{
			blend_color = blendColor;
			blend_alpha = blendColor.A;

			ushort a = blendColor.A;
			ushort invA = (ushort) (255 - a);

			// Pre-multiply blend color by alpha for the lerp: blend_color.C * a
			ushort blendB = (ushort) (blendColor.B * a);
			ushort blendG = (ushort) (blendColor.G * a);
			ushort blendR = (ushort) (blendColor.R * a);

			simd_blend_lo = Vector128.Create (blendB, blendG, blendR, (ushort) 0, blendB, blendG, blendR, (ushort) 0);
			simd_invA = Vector128.Create (invA, invA, invA, (ushort) 255, invA, invA, invA, (ushort) 255);

			simd_blend_lo256 = Vector256.Create (
				blendB, blendG, blendR, (ushort) 0, blendB, blendG, blendR, (ushort) 0,
				blendB, blendG, blendR, (ushort) 0, blendB, blendG, blendR, (ushort) 0);
			simd_invA256 = Vector256.Create (
				invA, invA, invA, (ushort) 255, invA, invA, invA, (ushort) 255,
				invA, invA, invA, (ushort) 255, invA, invA, invA, (ushort) 255);
		}
	}

	/// <summary>
	/// Used to set a given channel of a pixel to a given, predefined color.
	/// SIMD: mask off the target channel and OR in the new value (Vector128/Vector256).
	/// </summary>
	[Serializable]
	public sealed class SetChannel : UnaryPixelOp
	{
		private readonly int channel;
		private readonly byte set_value;
		private readonly Vector128<byte> channel_mask_128;
		private readonly Vector128<byte> channel_value_128;
		private readonly Vector256<byte> channel_mask_256;
		private readonly Vector256<byte> channel_value_256;

		public override ColorBgra Apply (in ColorBgra color)
		{
			ColorBgra result = color;
			result[channel] = set_value;
			return result;
		}

		public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> src)
		{
			int i = 0;
			if (Vector256.IsHardwareAccelerated && dst.Length >= PixelBatch8.Count) {
				int vectorEnd = dst.Length - (dst.Length % PixelBatch8.Count);
				for (; i < vectorEnd; i += PixelBatch8.Count) {
					var batch = PixelBatch8.Load (src.Slice (i, PixelBatch8.Count));
					new PixelBatch8 ((batch.Data & channel_mask_256) | channel_value_256)
						.Store (dst.Slice (i, PixelBatch8.Count));
				}
			}
			if (Vector128.IsHardwareAccelerated && dst.Length - i >= PixelBatch4.Count) {
				int vectorEnd = i + ((dst.Length - i) / PixelBatch4.Count * PixelBatch4.Count);
				for (; i < vectorEnd; i += PixelBatch4.Count) {
					var batch = PixelBatch4.Load (src.Slice (i, PixelBatch4.Count));
					new PixelBatch4 ((batch.Data & channel_mask_128) | channel_value_128)
						.Store (dst.Slice (i, PixelBatch4.Count));
				}
			}
			for (; i < dst.Length; ++i)
				dst[i] = Apply (src[i]);
		}

		public override void Apply (Span<ColorBgra> dst)
		{
			int i = 0;
			if (Vector256.IsHardwareAccelerated && dst.Length >= PixelBatch8.Count) {
				int vectorEnd = dst.Length - (dst.Length % PixelBatch8.Count);
				for (; i < vectorEnd; i += PixelBatch8.Count) {
					var slice = dst.Slice (i, PixelBatch8.Count);
					var batch = PixelBatch8.Load (slice);
					new PixelBatch8 ((batch.Data & channel_mask_256) | channel_value_256).Store (slice);
				}
			}
			if (Vector128.IsHardwareAccelerated && dst.Length - i >= PixelBatch4.Count) {
				int vectorEnd = i + ((dst.Length - i) / PixelBatch4.Count * PixelBatch4.Count);
				for (; i < vectorEnd; i += PixelBatch4.Count) {
					var slice = dst.Slice (i, PixelBatch4.Count);
					var batch = PixelBatch4.Load (slice);
					new PixelBatch4 ((batch.Data & channel_mask_128) | channel_value_128).Store (slice);
				}
			}
			for (; i < dst.Length; ++i)
				dst[i] = Apply (dst[i]);
		}

		public SetChannel (int channel, byte setValue)
		{
			this.channel = channel;
			set_value = setValue;

			// Create mask: 0xFF everywhere except at channel offset (0x00 there)
			// Create value: 0x00 everywhere except at channel offset (setValue there)
			Span<byte> mask128 = stackalloc byte[16];
			Span<byte> val128 = stackalloc byte[16];
			mask128.Fill (0xFF);
			val128.Fill (0);
			for (int p = 0; p < 4; p++) {
				mask128[p * 4 + channel] = 0;
				val128[p * 4 + channel] = setValue;
			}
			channel_mask_128 = Vector128.Create (mask128);
			channel_value_128 = Vector128.Create (val128);

			Span<byte> mask256 = stackalloc byte[32];
			Span<byte> val256 = stackalloc byte[32];
			mask256.Fill (0xFF);
			val256.Fill (0);
			for (int p = 0; p < 8; p++) {
				mask256[p * 4 + channel] = 0;
				val256[p * 4 + channel] = setValue;
			}
			channel_mask_256 = Vector256.Create (mask256);
			channel_value_256 = Vector256.Create (val256);
		}
	}

	/// <summary>
	/// Specialization of SetChannel that sets the alpha channel.
	/// SIMD: mask off alpha and OR in new alpha value (Vector128 and Vector256).
	/// </summary>
	[Serializable]
	public sealed class SetAlphaChannel : UnaryPixelOp
	{
		private readonly uint add_value;
		private readonly Vector128<byte> simd_alpha;
		private readonly Vector256<byte> simd_alpha256;
		private static readonly Vector128<byte> ColorMask128 = Vector128.Create (
			(byte) 0xFF, 0xFF, 0xFF, 0, 0xFF, 0xFF, 0xFF, 0, 0xFF, 0xFF, 0xFF, 0, 0xFF, 0xFF, 0xFF, 0);
		private static readonly Vector256<byte> ColorMask256 = Vector256.Create (
			(byte) 0xFF, 0xFF, 0xFF, 0, 0xFF, 0xFF, 0xFF, 0, 0xFF, 0xFF, 0xFF, 0, 0xFF, 0xFF, 0xFF, 0,
			0xFF, 0xFF, 0xFF, 0, 0xFF, 0xFF, 0xFF, 0, 0xFF, 0xFF, 0xFF, 0, 0xFF, 0xFF, 0xFF, 0);

		public override ColorBgra Apply (in ColorBgra color)
			=> ColorBgra.FromUInt32 ((color.BGRA & 0x00ffffff) + add_value);

		public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> src)
		{
			int i = 0;
			if (Vector256.IsHardwareAccelerated && dst.Length >= PixelBatch8.Count) {
				int vectorEnd = dst.Length - (dst.Length % PixelBatch8.Count);
				for (; i < vectorEnd; i += PixelBatch8.Count) {
					var batch = PixelBatch8.Load (src.Slice (i, PixelBatch8.Count));
					new PixelBatch8 ((batch.Data & ColorMask256) | simd_alpha256).Store (dst.Slice (i, PixelBatch8.Count));
				}
			}
			if (Vector128.IsHardwareAccelerated && dst.Length - i >= PixelBatch4.Count) {
				int vectorEnd = i + ((dst.Length - i) / PixelBatch4.Count * PixelBatch4.Count);
				for (; i < vectorEnd; i += PixelBatch4.Count) {
					var batch = PixelBatch4.Load (src.Slice (i, PixelBatch4.Count));
					new PixelBatch4 ((batch.Data & ColorMask128) | simd_alpha).Store (dst.Slice (i, PixelBatch4.Count));
				}
			}
			for (; i < dst.Length; ++i)
				dst[i] = Apply (src[i]);
		}

		public override void Apply (Span<ColorBgra> dst)
		{
			int i = 0;
			if (Vector256.IsHardwareAccelerated && dst.Length >= PixelBatch8.Count) {
				int vectorEnd = dst.Length - (dst.Length % PixelBatch8.Count);
				for (; i < vectorEnd; i += PixelBatch8.Count) {
					var slice = dst.Slice (i, PixelBatch8.Count);
					var batch = PixelBatch8.Load (slice);
					new PixelBatch8 ((batch.Data & ColorMask256) | simd_alpha256).Store (slice);
				}
			}
			if (Vector128.IsHardwareAccelerated && dst.Length - i >= PixelBatch4.Count) {
				int vectorEnd = i + ((dst.Length - i) / PixelBatch4.Count * PixelBatch4.Count);
				for (; i < vectorEnd; i += PixelBatch4.Count) {
					var slice = dst.Slice (i, PixelBatch4.Count);
					var batch = PixelBatch4.Load (slice);
					new PixelBatch4 ((batch.Data & ColorMask128) | simd_alpha).Store (slice);
				}
			}
			for (; i < dst.Length; ++i)
				dst[i] = Apply (dst[i]);
		}

		public SetAlphaChannel (byte alphaValue)
		{
			add_value = (uint) alphaValue << 24;
			simd_alpha = Vector128.Create (
				(byte) 0, 0, 0, alphaValue, 0, 0, 0, alphaValue,
				0, 0, 0, alphaValue, 0, 0, 0, alphaValue);
			simd_alpha256 = Vector256.Create (
				(byte) 0, 0, 0, alphaValue, 0, 0, 0, alphaValue,
				0, 0, 0, alphaValue, 0, 0, 0, alphaValue,
				0, 0, 0, alphaValue, 0, 0, 0, alphaValue,
				0, 0, 0, alphaValue, 0, 0, 0, alphaValue);
		}
	}

	/// <summary>
	/// Specialization of SetAlphaChannel that always sets alpha to 255.
	/// SIMD: OR with alpha mask (Vector128 and Vector256).
	/// </summary>
	[Serializable]
	public sealed class SetAlphaChannelTo255 : UnaryPixelOp
	{
		private static readonly Vector128<byte> AlphaMask128 = Vector128.Create (
			(byte) 0, 0, 0, 0xFF, 0, 0, 0, 0xFF, 0, 0, 0, 0xFF, 0, 0, 0, 0xFF);
		private static readonly Vector256<byte> AlphaMask256 = Vector256.Create (
			(byte) 0, 0, 0, 0xFF, 0, 0, 0, 0xFF, 0, 0, 0, 0xFF, 0, 0, 0, 0xFF,
			0, 0, 0, 0xFF, 0, 0, 0, 0xFF, 0, 0, 0, 0xFF, 0, 0, 0, 0xFF);

		public override ColorBgra Apply (in ColorBgra color)
			=> ColorBgra.FromUInt32 (color.BGRA | 0xff000000);

		public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> src)
		{
			int i = 0;
			if (Vector256.IsHardwareAccelerated && dst.Length >= PixelBatch8.Count) {
				int vectorEnd = dst.Length - (dst.Length % PixelBatch8.Count);
				for (; i < vectorEnd; i += PixelBatch8.Count) {
					var batch = PixelBatch8.Load (src.Slice (i, PixelBatch8.Count));
					new PixelBatch8 (batch.Data | AlphaMask256).Store (dst.Slice (i, PixelBatch8.Count));
				}
			}
			if (Vector128.IsHardwareAccelerated && dst.Length - i >= PixelBatch4.Count) {
				int vectorEnd = i + ((dst.Length - i) / PixelBatch4.Count * PixelBatch4.Count);
				for (; i < vectorEnd; i += PixelBatch4.Count) {
					var batch = PixelBatch4.Load (src.Slice (i, PixelBatch4.Count));
					new PixelBatch4 (batch.Data | AlphaMask128).Store (dst.Slice (i, PixelBatch4.Count));
				}
			}
			for (; i < dst.Length; ++i)
				dst[i] = Apply (src[i]);
		}

		public override void Apply (Span<ColorBgra> dst)
		{
			int i = 0;
			if (Vector256.IsHardwareAccelerated && dst.Length >= PixelBatch8.Count) {
				int vectorEnd = dst.Length - (dst.Length % PixelBatch8.Count);
				for (; i < vectorEnd; i += PixelBatch8.Count) {
					var slice = dst.Slice (i, PixelBatch8.Count);
					var batch = PixelBatch8.Load (slice);
					new PixelBatch8 (batch.Data | AlphaMask256).Store (slice);
				}
			}
			if (Vector128.IsHardwareAccelerated && dst.Length - i >= PixelBatch4.Count) {
				int vectorEnd = i + ((dst.Length - i) / PixelBatch4.Count * PixelBatch4.Count);
				for (; i < vectorEnd; i += PixelBatch4.Count) {
					var slice = dst.Slice (i, PixelBatch4.Count);
					var batch = PixelBatch4.Load (slice);
					new PixelBatch4 (batch.Data | AlphaMask128).Store (slice);
				}
			}
			for (; i < dst.Length; ++i)
				dst[i] = Apply (dst[i]);
		}
	}

	/// <summary>
	/// Inverts a pixel's color, and passes through the alpha component.
	/// SIMD: For premultiplied alpha, invert = A - C for each color channel.
	/// Supports both Vector128 (4 pixels) and Vector256 (8 pixels).
	/// </summary>
	[Serializable]
	public sealed class Invert : UnaryPixelOp
	{
		public override ColorBgra Apply (in ColorBgra color)
		{
			return ColorBgra.FromBgra (
				b: (byte) (color.A - color.B),
				g: (byte) (color.A - color.G),
				r: (byte) (color.A - color.R),
				a: color.A);
		}

		public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> src)
		{
			int i = 0;
			if (Vector256.IsHardwareAccelerated && dst.Length >= PixelBatch8.Count) {
				int vectorEnd = dst.Length - (dst.Length % PixelBatch8.Count);
				for (; i < vectorEnd; i += PixelBatch8.Count) {
					var batch = PixelBatch8.Load (src.Slice (i, PixelBatch8.Count));
					var alpha = batch.BroadcastAlpha ();
					var inverted = new PixelBatch8 (alpha.Data - batch.Data);
					var result = new PixelBatch8 (
						Vector256.ConditionalSelect (PixelBatch8.AlphaMask.Data, batch.Data, inverted.Data));
					result.Store (dst.Slice (i, PixelBatch8.Count));
				}
			}
			if (Vector128.IsHardwareAccelerated && dst.Length - i >= PixelBatch4.Count) {
				int vectorEnd = i + ((dst.Length - i) / PixelBatch4.Count * PixelBatch4.Count);
				for (; i < vectorEnd; i += PixelBatch4.Count) {
					var batch = PixelBatch4.Load (src.Slice (i, PixelBatch4.Count));
					var alpha = batch.BroadcastAlpha ();
					var inverted = new PixelBatch4 (alpha.Data - batch.Data);
					var result = new PixelBatch4 (
						Vector128.ConditionalSelect (PixelBatch4.AlphaMask.Data, batch.Data, inverted.Data));
					result.Store (dst.Slice (i, PixelBatch4.Count));
				}
			}
			for (; i < dst.Length; ++i)
				dst[i] = Apply (src[i]);
		}

		public override void Apply (Span<ColorBgra> dst)
		{
			int i = 0;
			if (Vector256.IsHardwareAccelerated && dst.Length >= PixelBatch8.Count) {
				int vectorEnd = dst.Length - (dst.Length % PixelBatch8.Count);
				for (; i < vectorEnd; i += PixelBatch8.Count) {
					var slice = dst.Slice (i, PixelBatch8.Count);
					var batch = PixelBatch8.Load (slice);
					var alpha = batch.BroadcastAlpha ();
					var inverted = new PixelBatch8 (alpha.Data - batch.Data);
					new PixelBatch8 (
						Vector256.ConditionalSelect (PixelBatch8.AlphaMask.Data, batch.Data, inverted.Data))
						.Store (slice);
				}
			}
			if (Vector128.IsHardwareAccelerated && dst.Length - i >= PixelBatch4.Count) {
				int vectorEnd = i + ((dst.Length - i) / PixelBatch4.Count * PixelBatch4.Count);
				for (; i < vectorEnd; i += PixelBatch4.Count) {
					var slice = dst.Slice (i, PixelBatch4.Count);
					var batch = PixelBatch4.Load (slice);
					var alpha = batch.BroadcastAlpha ();
					var inverted = new PixelBatch4 (alpha.Data - batch.Data);
					new PixelBatch4 (
						Vector128.ConditionalSelect (PixelBatch4.AlphaMask.Data, batch.Data, inverted.Data))
						.Store (slice);
				}
			}
			for (; i < dst.Length; ++i)
				dst[i] = Apply (dst[i]);
		}
	}

	/// <summary>
	/// If the color is within the red tolerance, remove it.
	/// SIMD: scalar-in-batch for load/store benefit (HSV conversion prevents true vectorization).
	/// </summary>
	[Serializable]
	public sealed class RedEyeRemove : UnaryPixelOp
	{
		private readonly int tolerance;
		private readonly double set_saturation;

		public RedEyeRemove (int tol, int sat)
		{
			tolerance = tol;
			set_saturation = (double) sat / 100;
		}

		public override ColorBgra Apply (in ColorBgra color)
		{
			int saturation = GetSaturation (color);
			int difference = color.R - Math.Max (color.B, color.G);

			if (difference <= tolerance || saturation <= 100)
				return color;

			double i = 255.0 * color.GetIntensity ();
			byte ib = (byte) (i * set_saturation);

			return ColorBgra.FromBgra (color.B, color.G, ib, color.A);
		}

		public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> src)
		{
			int idx = 0;
			if (Vector256.IsHardwareAccelerated && dst.Length >= PixelBatch8.Count) {
				int vectorEnd = dst.Length - (dst.Length % PixelBatch8.Count);
				Span<uint> results = stackalloc uint[8];
				for (; idx < vectorEnd; idx += PixelBatch8.Count) {
					var batch = PixelBatch8.Load (src.Slice (idx, PixelBatch8.Count));
					var data = batch.Data.AsUInt32 ();
					for (int j = 0; j < 8; j++)
						results[j] = Unsafe.BitCast<ColorBgra, uint> (
							Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (j))));
					new PixelBatch8 (Vector256.Create (
						results[0], results[1], results[2], results[3],
						results[4], results[5], results[6], results[7]).AsByte ())
						.Store (dst.Slice (idx, PixelBatch8.Count));
				}
			}
			if (Vector128.IsHardwareAccelerated && dst.Length - idx >= PixelBatch4.Count) {
				int vectorEnd = idx + ((dst.Length - idx) / PixelBatch4.Count * PixelBatch4.Count);
				for (; idx < vectorEnd; idx += PixelBatch4.Count) {
					var batch = PixelBatch4.Load (src.Slice (idx, PixelBatch4.Count));
					var data = batch.Data.AsUInt32 ();
					new PixelBatch4 (Vector128.Create (
						Unsafe.BitCast<ColorBgra, uint> (Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (0)))),
						Unsafe.BitCast<ColorBgra, uint> (Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (1)))),
						Unsafe.BitCast<ColorBgra, uint> (Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (2)))),
						Unsafe.BitCast<ColorBgra, uint> (Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (3))))).AsByte ())
						.Store (dst.Slice (idx, PixelBatch4.Count));
				}
			}
			for (; idx < dst.Length; ++idx)
				dst[idx] = Apply (src[idx]);
		}

		public override void Apply (Span<ColorBgra> dst)
		{
			int idx = 0;
			if (Vector256.IsHardwareAccelerated && dst.Length >= PixelBatch8.Count) {
				int vectorEnd = dst.Length - (dst.Length % PixelBatch8.Count);
				Span<uint> results = stackalloc uint[8];
				for (; idx < vectorEnd; idx += PixelBatch8.Count) {
					var slice = dst.Slice (idx, PixelBatch8.Count);
					var batch = PixelBatch8.Load (slice);
					var data = batch.Data.AsUInt32 ();
					for (int j = 0; j < 8; j++)
						results[j] = Unsafe.BitCast<ColorBgra, uint> (
							Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (j))));
					new PixelBatch8 (Vector256.Create (
						results[0], results[1], results[2], results[3],
						results[4], results[5], results[6], results[7]).AsByte ())
						.Store (slice);
				}
			}
			if (Vector128.IsHardwareAccelerated && dst.Length - idx >= PixelBatch4.Count) {
				int vectorEnd = idx + ((dst.Length - idx) / PixelBatch4.Count * PixelBatch4.Count);
				for (; idx < vectorEnd; idx += PixelBatch4.Count) {
					var slice = dst.Slice (idx, PixelBatch4.Count);
					var batch = PixelBatch4.Load (slice);
					var data = batch.Data.AsUInt32 ();
					new PixelBatch4 (Vector128.Create (
						Unsafe.BitCast<ColorBgra, uint> (Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (0)))),
						Unsafe.BitCast<ColorBgra, uint> (Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (1)))),
						Unsafe.BitCast<ColorBgra, uint> (Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (2)))),
						Unsafe.BitCast<ColorBgra, uint> (Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (3))))).AsByte ())
						.Store (slice);
				}
			}
			for (; idx < dst.Length; ++idx)
				dst[idx] = Apply (dst[idx]);
		}

		private static int GetSaturation (in ColorBgra color)
		{
			double r = (double) color.R / 255;
			double g = (double) color.G / 255;
			double b = (double) color.B / 255;

			double min = Math.Min (Math.Min (r, g), b);
			double max = Math.Max (Math.Max (r, g), b);
			double delta = max - min;

			double s =
				(max == 0 || delta == 0)
				? 0
				: delta / max;

			return (int) (s * 255);
		}
	}

	/// <summary>
	/// Inverts a pixel's color and its alpha component.
	/// SIMD: bitwise NOT of all channels (Vector128 and Vector256).
	/// </summary>
	[Serializable]
	public sealed class InvertWithAlpha : UnaryPixelOp
	{
		public override ColorBgra Apply (in ColorBgra color)
			=> ColorBgra.FromBgra ((byte) (255 - color.B), (byte) (255 - color.G), (byte) (255 - color.R), (byte) (255 - color.A));

		public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> src)
		{
			int i = 0;
			if (Vector256.IsHardwareAccelerated && dst.Length >= PixelBatch8.Count) {
				var allOnes = Vector256.Create ((byte) 255);
				int vectorEnd = dst.Length - (dst.Length % PixelBatch8.Count);
				for (; i < vectorEnd; i += PixelBatch8.Count) {
					var batch = PixelBatch8.Load (src.Slice (i, PixelBatch8.Count));
					new PixelBatch8 (allOnes - batch.Data).Store (dst.Slice (i, PixelBatch8.Count));
				}
			}
			if (Vector128.IsHardwareAccelerated && dst.Length - i >= PixelBatch4.Count) {
				var allOnes128 = Vector128.Create ((byte) 255);
				int vectorEnd = i + ((dst.Length - i) / PixelBatch4.Count * PixelBatch4.Count);
				for (; i < vectorEnd; i += PixelBatch4.Count) {
					var batch = PixelBatch4.Load (src.Slice (i, PixelBatch4.Count));
					new PixelBatch4 (allOnes128 - batch.Data).Store (dst.Slice (i, PixelBatch4.Count));
				}
			}
			for (; i < dst.Length; ++i)
				dst[i] = Apply (src[i]);
		}

		public override void Apply (Span<ColorBgra> dst)
		{
			int i = 0;
			if (Vector256.IsHardwareAccelerated && dst.Length >= PixelBatch8.Count) {
				var allOnes = Vector256.Create ((byte) 255);
				int vectorEnd = dst.Length - (dst.Length % PixelBatch8.Count);
				for (; i < vectorEnd; i += PixelBatch8.Count) {
					var slice = dst.Slice (i, PixelBatch8.Count);
					var batch = PixelBatch8.Load (slice);
					new PixelBatch8 (allOnes - batch.Data).Store (slice);
				}
			}
			if (Vector128.IsHardwareAccelerated && dst.Length - i >= PixelBatch4.Count) {
				var allOnes128 = Vector128.Create ((byte) 255);
				int vectorEnd = i + ((dst.Length - i) / PixelBatch4.Count * PixelBatch4.Count);
				for (; i < vectorEnd; i += PixelBatch4.Count) {
					var slice = dst.Slice (i, PixelBatch4.Count);
					var batch = PixelBatch4.Load (slice);
					new PixelBatch4 (allOnes128 - batch.Data).Store (slice);
				}
			}
			for (; i < dst.Length; ++i)
				dst[i] = Apply (dst[i]);
		}
	}

	/// <summary>
	/// Averages the input color's red, green, and blue channels.
	/// SIMD: extracts per-pixel, computes average, broadcasts to all 3 channels (Vector128/Vector256).
	/// </summary>
	[Serializable]
	public sealed class AverageChannels : UnaryPixelOp
	{
		public override ColorBgra Apply (in ColorBgra color)
		{
			byte average = (byte) ((color.R + color.G + color.B) / 3);
			return ColorBgra.FromBgra (average, average, average, color.A);
		}

		public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> src)
		{
			int i = 0;
			if (Vector256.IsHardwareAccelerated && dst.Length >= PixelBatch8.Count) {
				int vectorEnd = dst.Length - (dst.Length % PixelBatch8.Count);
				for (; i < vectorEnd; i += PixelBatch8.Count) {
					var batch = PixelBatch8.Load (src.Slice (i, PixelBatch8.Count));
					AverageBatch8 (batch).Store (dst.Slice (i, PixelBatch8.Count));
				}
			}
			if (Vector128.IsHardwareAccelerated && dst.Length - i >= PixelBatch4.Count) {
				int vectorEnd = i + ((dst.Length - i) / PixelBatch4.Count * PixelBatch4.Count);
				for (; i < vectorEnd; i += PixelBatch4.Count) {
					var batch = PixelBatch4.Load (src.Slice (i, PixelBatch4.Count));
					AverageBatch4 (batch).Store (dst.Slice (i, PixelBatch4.Count));
				}
			}
			for (; i < dst.Length; ++i)
				dst[i] = Apply (src[i]);
		}

		public override void Apply (Span<ColorBgra> dst)
		{
			int i = 0;
			if (Vector256.IsHardwareAccelerated && dst.Length >= PixelBatch8.Count) {
				int vectorEnd = dst.Length - (dst.Length % PixelBatch8.Count);
				for (; i < vectorEnd; i += PixelBatch8.Count) {
					var slice = dst.Slice (i, PixelBatch8.Count);
					AverageBatch8 (PixelBatch8.Load (slice)).Store (slice);
				}
			}
			if (Vector128.IsHardwareAccelerated && dst.Length - i >= PixelBatch4.Count) {
				int vectorEnd = i + ((dst.Length - i) / PixelBatch4.Count * PixelBatch4.Count);
				for (; i < vectorEnd; i += PixelBatch4.Count) {
					var slice = dst.Slice (i, PixelBatch4.Count);
					AverageBatch4 (PixelBatch4.Load (slice)).Store (slice);
				}
			}
			for (; i < dst.Length; ++i)
				dst[i] = Apply (dst[i]);
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		private static PixelBatch4 AverageBatch4 (PixelBatch4 batch)
		{
			// Extract individual pixels, compute average, rebuild
			var data = batch.Data.AsUInt32 ();
			var p0 = Unsafe.BitCast<uint, ColorBgra> (data.GetElement (0));
			var p1 = Unsafe.BitCast<uint, ColorBgra> (data.GetElement (1));
			var p2 = Unsafe.BitCast<uint, ColorBgra> (data.GetElement (2));
			var p3 = Unsafe.BitCast<uint, ColorBgra> (data.GetElement (3));

			byte a0 = (byte) ((p0.R + p0.G + p0.B) / 3);
			byte a1 = (byte) ((p1.R + p1.G + p1.B) / 3);
			byte a2 = (byte) ((p2.R + p2.G + p2.B) / 3);
			byte a3 = (byte) ((p3.R + p3.G + p3.B) / 3);

			return new PixelBatch4 (Vector128.Create (
				Unsafe.BitCast<ColorBgra, uint> (ColorBgra.FromBgra (a0, a0, a0, p0.A)),
				Unsafe.BitCast<ColorBgra, uint> (ColorBgra.FromBgra (a1, a1, a1, p1.A)),
				Unsafe.BitCast<ColorBgra, uint> (ColorBgra.FromBgra (a2, a2, a2, p2.A)),
				Unsafe.BitCast<ColorBgra, uint> (ColorBgra.FromBgra (a3, a3, a3, p3.A))).AsByte ());
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		private static PixelBatch8 AverageBatch8 (PixelBatch8 batch)
		{
			var data = batch.Data.AsUInt32 ();
			Span<uint> results = stackalloc uint[8];
			for (int j = 0; j < 8; j++) {
				var p = Unsafe.BitCast<uint, ColorBgra> (data.GetElement (j));
				byte avg = (byte) ((p.R + p.G + p.B) / 3);
				results[j] = Unsafe.BitCast<ColorBgra, uint> (ColorBgra.FromBgra (avg, avg, avg, p.A));
			}
			return new PixelBatch8 (Vector256.Create (
				results[0], results[1], results[2], results[3],
				results[4], results[5], results[6], results[7]).AsByte ());
		}
	}

	/// <summary>
	/// Desaturates a pixel using luminance weights.
	/// SIMD: Compute weighted intensity for 4/8 pixels at a time.
	/// </summary>
	[Serializable]
	public sealed class Desaturate : UnaryPixelOp
	{
		public override ColorBgra Apply (in ColorBgra color)
		{
			byte i = color.GetIntensityByte ();
			return ColorBgra.FromBgra (i, i, i, color.A);
		}

		public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> src)
		{
			int idx = 0;
			if (Vector256.IsHardwareAccelerated && dst.Length >= PixelBatch8.Count) {
				int vectorEnd = dst.Length - (dst.Length % PixelBatch8.Count);
				for (; idx < vectorEnd; idx += PixelBatch8.Count) {
					var batch = PixelBatch8.Load (src.Slice (idx, PixelBatch8.Count));
					DesaturateBatch8 (batch).Store (dst.Slice (idx, PixelBatch8.Count));
				}
			}
			if (Vector128.IsHardwareAccelerated && dst.Length - idx >= PixelBatch4.Count) {
				int vectorEnd = idx + ((dst.Length - idx) / PixelBatch4.Count * PixelBatch4.Count);
				for (; idx < vectorEnd; idx += PixelBatch4.Count) {
					var batch = PixelBatch4.Load (src.Slice (idx, PixelBatch4.Count));
					DesaturateBatch4 (batch).Store (dst.Slice (idx, PixelBatch4.Count));
				}
			}
			for (; idx < dst.Length; ++idx)
				dst[idx] = Apply (src[idx]);
		}

		public override void Apply (Span<ColorBgra> dst)
		{
			int idx = 0;
			if (Vector256.IsHardwareAccelerated && dst.Length >= PixelBatch8.Count) {
				int vectorEnd = dst.Length - (dst.Length % PixelBatch8.Count);
				for (; idx < vectorEnd; idx += PixelBatch8.Count) {
					var slice = dst.Slice (idx, PixelBatch8.Count);
					DesaturateBatch8 (PixelBatch8.Load (slice)).Store (slice);
				}
			}
			if (Vector128.IsHardwareAccelerated && dst.Length - idx >= PixelBatch4.Count) {
				int vectorEnd = idx + ((dst.Length - idx) / PixelBatch4.Count * PixelBatch4.Count);
				for (; idx < vectorEnd; idx += PixelBatch4.Count) {
					var slice = dst.Slice (idx, PixelBatch4.Count);
					DesaturateBatch4 (PixelBatch4.Load (slice)).Store (slice);
				}
			}
			for (; idx < dst.Length; ++idx)
				dst[idx] = Apply (dst[idx]);
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		private static PixelBatch4 DesaturateBatch4 (PixelBatch4 batch)
		{
			var data = batch.Data.AsUInt32 ();
			var p0 = Unsafe.BitCast<uint, ColorBgra> (data.GetElement (0));
			var p1 = Unsafe.BitCast<uint, ColorBgra> (data.GetElement (1));
			var p2 = Unsafe.BitCast<uint, ColorBgra> (data.GetElement (2));
			var p3 = Unsafe.BitCast<uint, ColorBgra> (data.GetElement (3));

			byte i0 = p0.GetIntensityByte (), i1 = p1.GetIntensityByte ();
			byte i2 = p2.GetIntensityByte (), i3 = p3.GetIntensityByte ();

			return new PixelBatch4 (Vector128.Create (
				Unsafe.BitCast<ColorBgra, uint> (ColorBgra.FromBgra (i0, i0, i0, p0.A)),
				Unsafe.BitCast<ColorBgra, uint> (ColorBgra.FromBgra (i1, i1, i1, p1.A)),
				Unsafe.BitCast<ColorBgra, uint> (ColorBgra.FromBgra (i2, i2, i2, p2.A)),
				Unsafe.BitCast<ColorBgra, uint> (ColorBgra.FromBgra (i3, i3, i3, p3.A))).AsByte ());
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		private static PixelBatch8 DesaturateBatch8 (PixelBatch8 batch)
		{
			var data = batch.Data.AsUInt32 ();
			Span<uint> results = stackalloc uint[8];
			for (int j = 0; j < 8; j++) {
				var p = Unsafe.BitCast<uint, ColorBgra> (data.GetElement (j));
				byte intensity = p.GetIntensityByte ();
				results[j] = Unsafe.BitCast<ColorBgra, uint> (ColorBgra.FromBgra (intensity, intensity, intensity, p.A));
			}
			return new PixelBatch8 (Vector256.Create (
				results[0], results[1], results[2], results[3],
				results[4], results[5], results[6], results[7]).AsByte ());
		}
	}

	[Serializable]
	public sealed class LuminosityCurve : UnaryPixelOp
	{
		public byte[] Curve { get; }
		public LuminosityCurve ()
		{
			var curve = new byte[256];
			for (int i = 0; i < 256; ++i) {
				curve[i] = (byte) i;
			}
			Curve = curve;
		}

		public override ColorBgra Apply (in ColorBgra color)
		{
			byte lumi = color.GetIntensityByte ();
			int diff = Curve[lumi] - lumi;

			return ColorBgra.FromBgraClamped (
				b: color.B + diff,
				g: color.G + diff,
				r: color.R + diff,
				a: color.A);
		}

		public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> src)
		{
			int i = 0;
			if (Vector256.IsHardwareAccelerated && dst.Length >= PixelBatch8.Count) {
				int vectorEnd = dst.Length - (dst.Length % PixelBatch8.Count);
				for (; i < vectorEnd; i += PixelBatch8.Count) {
					var batch = PixelBatch8.Load (src.Slice (i, PixelBatch8.Count));
					ApplyLutBatch8 (batch).Store (dst.Slice (i, PixelBatch8.Count));
				}
			}
			if (Vector128.IsHardwareAccelerated && dst.Length - i >= PixelBatch4.Count) {
				int vectorEnd = i + ((dst.Length - i) / PixelBatch4.Count * PixelBatch4.Count);
				for (; i < vectorEnd; i += PixelBatch4.Count) {
					var batch = PixelBatch4.Load (src.Slice (i, PixelBatch4.Count));
					ApplyLutBatch4 (batch).Store (dst.Slice (i, PixelBatch4.Count));
				}
			}
			for (; i < dst.Length; ++i)
				dst[i] = Apply (src[i]);
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		private PixelBatch4 ApplyLutBatch4 (PixelBatch4 batch)
		{
			var data = batch.Data.AsUInt32 ();
			return new PixelBatch4 (Vector128.Create (
				Unsafe.BitCast<ColorBgra, uint> (Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (0)))),
				Unsafe.BitCast<ColorBgra, uint> (Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (1)))),
				Unsafe.BitCast<ColorBgra, uint> (Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (2)))),
				Unsafe.BitCast<ColorBgra, uint> (Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (3))))).AsByte ());
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		private PixelBatch8 ApplyLutBatch8 (PixelBatch8 batch)
		{
			var data = batch.Data.AsUInt32 ();
			Span<uint> results = stackalloc uint[8];
			for (int j = 0; j < 8; j++)
				results[j] = Unsafe.BitCast<ColorBgra, uint> (Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (j))));
			return new PixelBatch8 (Vector256.Create (
				results[0], results[1], results[2], results[3],
				results[4], results[5], results[6], results[7]).AsByte ());
		}
	}

	[Serializable]
	public class ChannelCurve : UnaryPixelOp
	{
		public byte[] CurveB { get; internal set; }
		public byte[] CurveG { get; internal set; }
		public byte[] CurveR { get; internal set; }

		public ChannelCurve ()
		{
			var curveB = new byte[256];
			var curveG = new byte[256];
			var curveR = new byte[256];
			for (int i = 0; i < 256; ++i) {
				curveB[i] = (byte) i;
				curveG[i] = (byte) i;
				curveR[i] = (byte) i;
			}
			CurveB = curveB;
			CurveG = curveG;
			CurveR = curveR;
		}

		public override ColorBgra Apply (in ColorBgra color)
			=> ColorBgra.FromBgra (
				b: CurveB[color.B],
				g: CurveG[color.G],
				r: CurveR[color.R],
				a: color.A);

		public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> src)
		{
			int i = 0;
			if (Vector256.IsHardwareAccelerated && dst.Length >= PixelBatch8.Count) {
				int vectorEnd = dst.Length - (dst.Length % PixelBatch8.Count);
				for (; i < vectorEnd; i += PixelBatch8.Count) {
					var batch = PixelBatch8.Load (src.Slice (i, PixelBatch8.Count));
					ApplyCurveBatch8 (batch).Store (dst.Slice (i, PixelBatch8.Count));
				}
			}
			if (Vector128.IsHardwareAccelerated && dst.Length - i >= PixelBatch4.Count) {
				int vectorEnd = i + ((dst.Length - i) / PixelBatch4.Count * PixelBatch4.Count);
				for (; i < vectorEnd; i += PixelBatch4.Count) {
					var batch = PixelBatch4.Load (src.Slice (i, PixelBatch4.Count));
					ApplyCurveBatch4 (batch).Store (dst.Slice (i, PixelBatch4.Count));
				}
			}
			for (; i < dst.Length; ++i)
				dst[i] = Apply (src[i]);
		}

		public override void Apply (Span<ColorBgra> dst)
		{
			int i = 0;
			if (Vector256.IsHardwareAccelerated && dst.Length >= PixelBatch8.Count) {
				int vectorEnd = dst.Length - (dst.Length % PixelBatch8.Count);
				for (; i < vectorEnd; i += PixelBatch8.Count) {
					var slice = dst.Slice (i, PixelBatch8.Count);
					ApplyCurveBatch8 (PixelBatch8.Load (slice)).Store (slice);
				}
			}
			if (Vector128.IsHardwareAccelerated && dst.Length - i >= PixelBatch4.Count) {
				int vectorEnd = i + ((dst.Length - i) / PixelBatch4.Count * PixelBatch4.Count);
				for (; i < vectorEnd; i += PixelBatch4.Count) {
					var slice = dst.Slice (i, PixelBatch4.Count);
					ApplyCurveBatch4 (PixelBatch4.Load (slice)).Store (slice);
				}
			}
			for (; i < dst.Length; ++i)
				dst[i] = Apply (dst[i]);
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		private PixelBatch4 ApplyCurveBatch4 (PixelBatch4 batch)
		{
			var data = batch.Data.AsUInt32 ();
			return new PixelBatch4 (Vector128.Create (
				Unsafe.BitCast<ColorBgra, uint> (Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (0)))),
				Unsafe.BitCast<ColorBgra, uint> (Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (1)))),
				Unsafe.BitCast<ColorBgra, uint> (Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (2)))),
				Unsafe.BitCast<ColorBgra, uint> (Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (3))))).AsByte ());
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		private PixelBatch8 ApplyCurveBatch8 (PixelBatch8 batch)
		{
			var data = batch.Data.AsUInt32 ();
			Span<uint> results = stackalloc uint[8];
			for (int j = 0; j < 8; j++)
				results[j] = Unsafe.BitCast<ColorBgra, uint> (Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (j))));
			return new PixelBatch8 (Vector256.Create (
				results[0], results[1], results[2], results[3],
				results[4], results[5], results[6], results[7]).AsByte ());
		}
	}

	[Serializable]
	public sealed class Level : ChannelCurve, ICloneable
	{
		public const float MinGamma = 0.1f;
		public const float MaxGamma = 10.0f;
		private ColorBgra color_in_low;
		public ColorBgra ColorInLow {
			get => color_in_low;

			set {
				byte r = (value.R == 255) ? (byte) 254 : value.R;
				byte g = (value.G == 255) ? (byte) 254 : value.G;
				byte b = (value.B == 255) ? (byte) 254 : value.B;
				color_in_high = ColorBgra.FromBgra (
					b: (color_in_high.B < b + 1) ? (byte) (r + 1) : color_in_high.B,
					g: (color_in_high.G < g + 1) ? (byte) (r + 1) : color_in_high.G,
					r: (color_in_high.R < r + 1) ? (byte) (r + 1) : color_in_high.R,
					a: color_in_high.A);
				color_in_low = ColorBgra.FromBgra (b, g, r, value.A);
				UpdateLookupTable ();
			}
		}

		private ColorBgra color_in_high;
		public ColorBgra ColorInHigh {
			get => color_in_high;

			set {
				byte r = (value.R == 0) ? (byte) 1 : value.R;
				byte g = (value.G == 0) ? (byte) 1 : value.G;
				byte b = (value.B == 0) ? (byte) 1 : value.B;
				color_in_low = ColorBgra.FromBgra (
					b: (color_in_low.B > b - 1) ? (byte) (r - 1) : color_in_low.B,
					g: (color_in_low.G > g - 1) ? (byte) (r - 1) : color_in_low.G,
					r: (color_in_low.R > r - 1) ? (byte) (r - 1) : color_in_low.R,
					a: color_in_low.A);
				color_in_high = ColorBgra.FromBgra (b, g, r, value.A);
				UpdateLookupTable ();
			}
		}

		private ColorBgra color_out_low;
		public ColorBgra ColorOutLow {
			get => color_out_low;

			set {
				byte r = (value.R == 255) ? (byte) 254 : value.R;
				byte g = (value.G == 255) ? (byte) 254 : value.G;
				byte b = (value.B == 255) ? (byte) 254 : value.B;
				color_out_high = ColorBgra.FromBgra (
					b: (color_out_high.B < b + 1) ? (byte) (b + 1) : color_out_high.B,
					g: (color_out_high.G < g + 1) ? (byte) (g + 1) : color_out_high.G,
					r: (color_out_high.R < r + 1) ? (byte) (r + 1) : color_out_high.R,
					a: color_out_high.A);
				color_out_low = ColorBgra.FromBgra (b, g, r, value.A);
				UpdateLookupTable ();
			}
		}

		private ColorBgra color_out_high;
		public ColorBgra ColorOutHigh {
			get => color_out_high;
			set {
				byte r = (value.R == 0) ? (byte) 1 : value.R;
				byte g = (value.G == 0) ? (byte) 1 : value.G;
				byte b = (value.B == 0) ? (byte) 1 : value.B;
				color_out_low = ColorBgra.FromBgra (
					b: (color_out_low.B > b - 1) ? (byte) (b - 1) : color_out_low.B,
					g: (color_out_low.G > g - 1) ? (byte) (g - 1) : color_out_low.G,
					r: (color_out_low.R > r - 1) ? (byte) (r - 1) : color_out_low.R,
					a: color_out_low.A);
				color_out_high = ColorBgra.FromBgra (b, g, r, value.A);
				UpdateLookupTable ();
			}
		}

		private readonly float[] gamma = new float[3];
		public float GetGamma (int index)
		{
			if (index < 0 || index >= 3)
				throw new ArgumentOutOfRangeException (nameof (index), index, "Index must be between 0 and 2");

			return gamma[index];
		}

		public void SetGamma (int index, float val)
		{
			if (index < 0 || index >= 3)
				throw new ArgumentOutOfRangeException (nameof (index), index, "Index must be between 0 and 2");

			gamma[index] = Math.Clamp (val, MinGamma, MaxGamma);
			UpdateLookupTable ();
		}

		public bool IsValid { get; private set; } = true;

		public static Level AutoFromLoMdHi (ColorBgra lo, ColorBgra md, ColorBgra hi)
		{
			float[] gamma = new float[3];
			for (int i = 0; i < 3; i++) {
				gamma[i] =
					(lo[i] < md[i] && md[i] < hi[i])
					? Math.Clamp (
						MathF.Log (0.5f, (md[i] - lo[i]) / (float) (hi[i] - lo[i])),
						MinGamma,
						MaxGamma)
					: 1.0f;
			}
			return new Level (lo, hi, gamma, ColorBgra.Black, ColorBgra.White);
		}

		private void UpdateLookupTable ()
		{
			for (int i = 0; i < 3; i++) {
				if (color_out_high[i] < color_out_low[i] ||
				    color_in_high[i] <= color_in_low[i] ||
				    gamma[i] < 0) {
					IsValid = false;
					return;
				}

				for (int j = 0; j < 256; j++) {
					ColorBgra col = Apply (j, j, j);
					CurveB[j] = col.B;
					CurveG[j] = col.G;
					CurveR[j] = col.R;
				}
			}
		}

		public Level () : this (
			ColorBgra.Black,
			ColorBgra.White,
			[1, 1, 1],
			ColorBgra.Black,
			ColorBgra.White)
		{ }

		public Level (ColorBgra in_lo, ColorBgra in_hi, float[] gamma, ColorBgra out_lo, ColorBgra out_hi)
		{
			color_in_low = in_lo;
			color_in_high = in_hi;
			color_out_low = out_lo;
			color_out_high = out_hi;

			if (gamma.Length != 3)
				throw new ArgumentException ($"{nameof (gamma)} must be a float[3]", nameof (gamma));

			this.gamma = gamma;
			UpdateLookupTable ();
		}

		public ColorBgra Apply (float r, float g, float b)
		{
			ColorBgra ret = new ColorBgra ();
			ReadOnlySpan<float> input = [b, g, r];

			for (int i = 0; i < 3; i++) {
				float v = (input[i] - color_in_low[i]);

				if (v < 0) {
					ret[i] = color_out_low[i];
				} else if (v + color_in_low[i] >= color_in_high[i]) {
					ret[i] = color_out_high[i];
				} else {
					ret[i] = (byte) Math.Clamp (
						color_out_low[i] + (color_out_high[i] - color_out_low[i]) * Math.Pow (v / (color_in_high[i] - color_in_low[i]), gamma[i]),
						0.0f,
						255.0f);
				}
			}

			return ret;
		}

		public void UnApply (ColorBgra after, Span<float> beforeOut, Span<float> slopesOut)
		{
			if (beforeOut.Length != 3)
				throw new ArgumentException ($"{nameof (beforeOut)} must be a float[3]", nameof (beforeOut));

			if (slopesOut.Length != 3)
				throw new ArgumentException ($"{nameof (slopesOut)} must be a float[3]", nameof (slopesOut));

			for (int i = 0; i < 3; i++) {

				beforeOut[i] = color_in_low[i] + (color_in_high[i] - color_in_low[i]) *
				    MathF.Pow ((float) (after[i] - color_out_low[i]) / (color_out_high[i] - color_out_low[i]), 1 / gamma[i]);

				slopesOut[i] = (color_in_high[i] - color_in_low[i]) / ((color_out_high[i] - color_out_low[i]) * gamma[i]) *
				    MathF.Pow ((float) (after[i] - color_out_low[i]) / (color_out_high[i] - color_out_low[i]), 1 / gamma[i] - 1);

				if (float.IsInfinity (slopesOut[i]) || float.IsNaN (slopesOut[i]))
					slopesOut[i] = 0;
			}
		}

		public object Clone ()
		{
			Level copy = new Level (color_in_low, color_in_high, (float[]) gamma.Clone (), color_out_low, color_out_high) {
				CurveB = [.. CurveB],
				CurveG = [.. CurveG],
				CurveR = [.. CurveR]
			};

			return copy;
		}
	}

	[Serializable]
	public sealed class HueSaturationLightness : UnaryPixelOp
	{
		private readonly int hue_delta;
		private readonly int sat_factor;
		private readonly UnaryPixelOp blend_op;

		public HueSaturationLightness (int hueDelta, int satDelta, int lightness)
		{
			hue_delta = hueDelta;
			sat_factor = (satDelta * 1024) / 100;
			blend_op = lightness switch {
				0 => new Identity (),
				> 0 => new BlendConstant (ColorBgra.FromBgra (255, 255, 255, (byte) (lightness * 255 / 100))),
				_ => new BlendConstant (ColorBgra.FromBgra (0, 0, 0, (byte) (-lightness * 255 / 100))),
			};
		}

		public override ColorBgra Apply (in ColorBgra src_color)
		{
			// adjust saturation
			byte intensity = src_color.GetIntensityByte ();
			ColorBgra color = ColorBgra.FromBgra (
				b: FoundationUtility.ClampToByte ((intensity * 1024 + (src_color.B - intensity) * sat_factor) >> 10),
				g: FoundationUtility.ClampToByte ((intensity * 1024 + (src_color.G - intensity) * sat_factor) >> 10),
				r: FoundationUtility.ClampToByte ((intensity * 1024 + (src_color.R - intensity) * sat_factor) >> 10),
				a: src_color.A);

			HsvColor hsvColor = HsvColor.FromBgra (color);
			int newHue = (int) hsvColor.Hue;

			newHue += hue_delta;

			while (newHue < 0) { newHue += 360; }

			while (newHue > 360) { newHue -= 360; }

			ColorBgra newColor = (hsvColor with { Hue = newHue }).ToColorBgra ();
			newColor = blend_op.Apply (newColor).NewAlpha (color.A);

			return newColor;
		}

		public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> src)
		{
			int i = 0;
			// Scalar-in-batch: extract pixels, apply scalar, repack for SIMD load/store benefit
			if (Vector256.IsHardwareAccelerated && dst.Length >= PixelBatch8.Count) {
				int vectorEnd = dst.Length - (dst.Length % PixelBatch8.Count);
				Span<uint> results = stackalloc uint[8];
				for (; i < vectorEnd; i += PixelBatch8.Count) {
					var batch = PixelBatch8.Load (src.Slice (i, PixelBatch8.Count));
					var data = batch.Data.AsUInt32 ();
					for (int j = 0; j < 8; j++)
						results[j] = Unsafe.BitCast<ColorBgra, uint> (
							Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (j))));
					new PixelBatch8 (Vector256.Create (
						results[0], results[1], results[2], results[3],
						results[4], results[5], results[6], results[7]).AsByte ())
						.Store (dst.Slice (i, PixelBatch8.Count));
				}
			}
			if (Vector128.IsHardwareAccelerated && dst.Length - i >= PixelBatch4.Count) {
				int vectorEnd = i + ((dst.Length - i) / PixelBatch4.Count * PixelBatch4.Count);
				for (; i < vectorEnd; i += PixelBatch4.Count) {
					var batch = PixelBatch4.Load (src.Slice (i, PixelBatch4.Count));
					var data = batch.Data.AsUInt32 ();
					new PixelBatch4 (Vector128.Create (
						Unsafe.BitCast<ColorBgra, uint> (Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (0)))),
						Unsafe.BitCast<ColorBgra, uint> (Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (1)))),
						Unsafe.BitCast<ColorBgra, uint> (Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (2)))),
						Unsafe.BitCast<ColorBgra, uint> (Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (3))))).AsByte ())
						.Store (dst.Slice (i, PixelBatch4.Count));
				}
			}
			for (; i < dst.Length; ++i)
				dst[i] = Apply (src[i]);
		}

		public override void Apply (Span<ColorBgra> dst)
		{
			int i = 0;
			if (Vector256.IsHardwareAccelerated && dst.Length >= PixelBatch8.Count) {
				int vectorEnd = dst.Length - (dst.Length % PixelBatch8.Count);
				Span<uint> results = stackalloc uint[8];
				for (; i < vectorEnd; i += PixelBatch8.Count) {
					var slice = dst.Slice (i, PixelBatch8.Count);
					var batch = PixelBatch8.Load (slice);
					var data = batch.Data.AsUInt32 ();
					for (int j = 0; j < 8; j++)
						results[j] = Unsafe.BitCast<ColorBgra, uint> (
							Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (j))));
					new PixelBatch8 (Vector256.Create (
						results[0], results[1], results[2], results[3],
						results[4], results[5], results[6], results[7]).AsByte ())
						.Store (slice);
				}
			}
			if (Vector128.IsHardwareAccelerated && dst.Length - i >= PixelBatch4.Count) {
				int vectorEnd = i + ((dst.Length - i) / PixelBatch4.Count * PixelBatch4.Count);
				for (; i < vectorEnd; i += PixelBatch4.Count) {
					var slice = dst.Slice (i, PixelBatch4.Count);
					var batch = PixelBatch4.Load (slice);
					var data = batch.Data.AsUInt32 ();
					new PixelBatch4 (Vector128.Create (
						Unsafe.BitCast<ColorBgra, uint> (Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (0)))),
						Unsafe.BitCast<ColorBgra, uint> (Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (1)))),
						Unsafe.BitCast<ColorBgra, uint> (Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (2)))),
						Unsafe.BitCast<ColorBgra, uint> (Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (3))))).AsByte ())
						.Store (slice);
				}
			}
			for (; i < dst.Length; ++i)
				dst[i] = Apply (dst[i]);
		}
	}

	[Serializable]
	public sealed class PosterizePixel : UnaryPixelOp
	{
		private readonly ImmutableArray<byte> red_levels;
		private readonly ImmutableArray<byte> green_levels;
		private readonly ImmutableArray<byte> blue_levels;

		public PosterizePixel (int red, int green, int blue)
		{
			red_levels = CalcLevels (red);
			green_levels = CalcLevels (green);
			blue_levels = CalcLevels (blue);
		}

		private static ImmutableArray<byte> CalcLevels (int levelCount)
		{
			Span<byte> t1 = stackalloc byte[levelCount];

			for (int i = 1; i < levelCount; i++)
				t1[i] = (byte) ((255 * i) / (levelCount - 1));

			var levels = ImmutableArray.CreateBuilder<byte> (256);
			levels.Count = 256;

			int j = 0;
			int k = 0;

			for (int i = 0; i < 256; i++) {
				levels[i] = t1[j];

				k += levelCount;

				if (k > 255) {
					k -= 255;
					j++;
				}
			}

			return levels.MoveToImmutable ();
		}

		public override ColorBgra Apply (in ColorBgra color)
			=> ColorBgra.FromBgra (
				b: blue_levels[color.B],
				g: green_levels[color.G],
				r: red_levels[color.R],
				a: color.A);

		public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> src)
		{
			int i = 0;
			if (Vector256.IsHardwareAccelerated && dst.Length >= PixelBatch8.Count) {
				int vectorEnd = dst.Length - (dst.Length % PixelBatch8.Count);
				for (; i < vectorEnd; i += PixelBatch8.Count) {
					var batch = PixelBatch8.Load (src.Slice (i, PixelBatch8.Count));
					ApplyBatch8 (batch).Store (dst.Slice (i, PixelBatch8.Count));
				}
			}
			if (Vector128.IsHardwareAccelerated && dst.Length - i >= PixelBatch4.Count) {
				int vectorEnd = i + ((dst.Length - i) / PixelBatch4.Count * PixelBatch4.Count);
				for (; i < vectorEnd; i += PixelBatch4.Count) {
					var batch = PixelBatch4.Load (src.Slice (i, PixelBatch4.Count));
					ApplyBatch4 (batch).Store (dst.Slice (i, PixelBatch4.Count));
				}
			}
			for (; i < dst.Length; ++i)
				dst[i] = Apply (src[i]);
		}

		public override void Apply (Span<ColorBgra> dst)
		{
			int i = 0;
			if (Vector256.IsHardwareAccelerated && dst.Length >= PixelBatch8.Count) {
				int vectorEnd = dst.Length - (dst.Length % PixelBatch8.Count);
				for (; i < vectorEnd; i += PixelBatch8.Count) {
					var slice = dst.Slice (i, PixelBatch8.Count);
					ApplyBatch8 (PixelBatch8.Load (slice)).Store (slice);
				}
			}
			if (Vector128.IsHardwareAccelerated && dst.Length - i >= PixelBatch4.Count) {
				int vectorEnd = i + ((dst.Length - i) / PixelBatch4.Count * PixelBatch4.Count);
				for (; i < vectorEnd; i += PixelBatch4.Count) {
					var slice = dst.Slice (i, PixelBatch4.Count);
					ApplyBatch4 (PixelBatch4.Load (slice)).Store (slice);
				}
			}
			for (; i < dst.Length; ++i)
				dst[i] = Apply (dst[i]);
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		private PixelBatch4 ApplyBatch4 (PixelBatch4 batch)
		{
			var data = batch.Data.AsUInt32 ();
			return new PixelBatch4 (Vector128.Create (
				Unsafe.BitCast<ColorBgra, uint> (Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (0)))),
				Unsafe.BitCast<ColorBgra, uint> (Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (1)))),
				Unsafe.BitCast<ColorBgra, uint> (Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (2)))),
				Unsafe.BitCast<ColorBgra, uint> (Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (3))))).AsByte ());
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		private PixelBatch8 ApplyBatch8 (PixelBatch8 batch)
		{
			var data = batch.Data.AsUInt32 ();
			Span<uint> results = stackalloc uint[8];
			for (int j = 0; j < 8; j++)
				results[j] = Unsafe.BitCast<ColorBgra, uint> (Apply (Unsafe.BitCast<uint, ColorBgra> (data.GetElement (j))));
			return new PixelBatch8 (Vector256.Create (
				results[0], results[1], results[2], results[3],
				results[4], results[5], results[6], results[7]).AsByte ());
		}
	}
}
