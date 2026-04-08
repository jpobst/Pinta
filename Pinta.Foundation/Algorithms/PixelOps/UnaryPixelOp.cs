/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) Rick Brewster, Tom Jackson, and past contributors.            //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
/////////////////////////////////////////////////////////////////////////////////

using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Pinta.Foundation;

/// <summary>
/// Interface for unary pixel operations that provide vectorized SIMD paths (128-bit).
/// </summary>
internal interface IUnaryPixelOp
{
	/// <summary>Applies the operation to 4 pixels using SIMD.</summary>
	static abstract PixelBatch4 Apply4 (PixelBatch4 src);
}

/// <summary>
/// Interface for unary pixel operations that provide 256-bit SIMD paths.
/// </summary>
internal interface IUnaryPixelOp8
{
	/// <summary>Applies the operation to 8 pixels using SIMD.</summary>
	static abstract PixelBatch8 Apply8 (PixelBatch8 src);
}

/// <summary>
/// Defines a way to operate on a pixel, or a region of pixels, in a unary fashion.
/// That is, it is a simple function F that takes one parameter and returns a
/// result of the form: d = F(c)
/// </summary>
[Serializable]
public abstract class UnaryPixelOp : PixelOp
{
	public UnaryPixelOp ()
	{
	}

	public abstract ColorBgra Apply (in ColorBgra color);

	public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> src)
	{
		for (int i = 0; i < src.Length; ++i)
			dst[i] = Apply (src[i]);
	}

	public virtual void Apply (Span<ColorBgra> dst)
	{
		for (int i = 0; i < dst.Length; ++i)
			dst[i] = Apply (dst[i]);
	}

	/// <summary>
	/// SIMD-accelerated batch processing loop for unary ops.
	/// Processes 8 pixels at a time with Vector256, then 4 with Vector128,
	/// with scalar fallback for remainder.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	internal void ApplyLoop<T4, T8> (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> src)
		where T4 : IUnaryPixelOp
		where T8 : IUnaryPixelOp8
	{
		int length = dst.Length;
		int i = 0;

		if (Vector256.IsHardwareAccelerated && length >= PixelBatch8.Count) {
			int vectorEnd = length - (length % PixelBatch8.Count);
			for (; i < vectorEnd; i += PixelBatch8.Count) {
				var batch = PixelBatch8.Load (src.Slice (i, PixelBatch8.Count));
				T8.Apply8 (batch).Store (dst.Slice (i, PixelBatch8.Count));
			}
		}

		if (Vector128.IsHardwareAccelerated && length - i >= PixelBatch4.Count) {
			int vectorEnd = length - ((length - i) % PixelBatch4.Count);
			for (; i < vectorEnd; i += PixelBatch4.Count) {
				var batch = PixelBatch4.Load (src.Slice (i, PixelBatch4.Count));
				T4.Apply4 (batch).Store (dst.Slice (i, PixelBatch4.Count));
			}
		}

		for (; i < length; ++i)
			dst[i] = Apply (src[i]);
	}

	/// <summary>
	/// SIMD-accelerated in-place batch processing loop for unary ops.
	/// Processes 8 pixels at a time with Vector256, then 4 with Vector128.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	internal void ApplyLoopInPlace<T4, T8> (Span<ColorBgra> dst)
		where T4 : IUnaryPixelOp
		where T8 : IUnaryPixelOp8
	{
		int length = dst.Length;
		int i = 0;

		if (Vector256.IsHardwareAccelerated && length >= PixelBatch8.Count) {
			int vectorEnd = length - (length % PixelBatch8.Count);
			for (; i < vectorEnd; i += PixelBatch8.Count) {
				var slice = dst.Slice (i, PixelBatch8.Count);
				T8.Apply8 (PixelBatch8.Load (slice)).Store (slice);
			}
		}

		if (Vector128.IsHardwareAccelerated && length - i >= PixelBatch4.Count) {
			int vectorEnd = length - ((length - i) % PixelBatch4.Count);
			for (; i < vectorEnd; i += PixelBatch4.Count) {
				var slice = dst.Slice (i, PixelBatch4.Count);
				T4.Apply4 (PixelBatch4.Load (slice)).Store (slice);
			}
		}

		for (; i < length; ++i)
			dst[i] = Apply (dst[i]);
	}

	private void ApplyRectangle (PixelBuffer surface, RectangleI rect)
	{
		var data = surface.GetPixelData ();
		int width = surface.Width;
		for (int y = rect.Top; y <= rect.Bottom; ++y) {
			Apply (data.Slice (y * width + rect.Left, rect.Width));
		}
	}

	public void Apply (PixelBuffer surface, ReadOnlySpan<RectangleI> roi, int startIndex, int length)
	{
		RectangleI regionBounds = FoundationUtility.GetRegionBounds (roi, startIndex, length);

		if (regionBounds != RectangleI.Intersect (surface.GetBounds (), regionBounds))
			throw new ArgumentOutOfRangeException (nameof (roi), "Region is out of bounds");

		for (int x = startIndex; x < startIndex + length; ++x)
			ApplyRectangle (surface, roi[x]);
	}

	public void Apply (PixelBuffer surface, ReadOnlySpan<RectangleI> roi)
	{
		Apply (surface, roi, 0, roi.Length);
	}

	public void Apply (PixelBuffer surface, RectangleI roi)
	{
		ApplyRectangle (surface, roi);
	}

	public void Apply (PixelBuffer dst, PixelBuffer src, RectangleI roi)
	{
		var src_data = src.GetReadOnlyPixelData ();
		var dst_data = dst.GetPixelData ();
		int src_width = src.Width;
		int dst_width = dst.Width;

		for (int y = roi.Y; y <= roi.Bottom; ++y) {
			Apply (dst_data.Slice (y * dst_width + roi.X, roi.Width),
			      src_data.Slice (y * src_width + roi.X, roi.Width));
		}
	}

	public void Apply (PixelBuffer dst, PixelBuffer src, ReadOnlySpan<RectangleI> rois)
	{
		foreach (RectangleI roi in rois)
			Apply (dst, src, roi);
	}
}
