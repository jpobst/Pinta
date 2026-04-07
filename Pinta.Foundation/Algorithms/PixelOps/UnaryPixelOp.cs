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
/// Interface for unary pixel operations that provide vectorized SIMD paths.
/// </summary>
internal interface IUnaryPixelOp
{
	/// <summary>Applies the operation to 4 pixels using SIMD.</summary>
	static abstract PixelBatch4 Apply4 (PixelBatch4 src);
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
	/// Processes 4 pixels at a time with Vector128, scalar fallback for remainder.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	internal static void ApplyLoop<T> (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> src)
		where T : IUnaryPixelOp
	{
		int length = dst.Length;
		int i = 0;

		if (Vector128.IsHardwareAccelerated && length >= PixelBatch4.Count) {
			int vectorEnd = length - (length % PixelBatch4.Count);
			for (; i < vectorEnd; i += PixelBatch4.Count) {
				var batch = PixelBatch4.Load (src.Slice (i, PixelBatch4.Count));
				var result = T.Apply4 (batch);
				result.Store (dst.Slice (i, PixelBatch4.Count));
			}
		}

		// Scalar fallback - not available here since T is IUnaryPixelOp, not a UnaryPixelOp instance
		// The remaining pixels will be processed in the calling method.
	}

	/// <summary>
	/// SIMD-accelerated in-place batch processing loop for unary ops.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	internal static void ApplyLoopInPlace<T> (Span<ColorBgra> dst)
		where T : IUnaryPixelOp
	{
		int length = dst.Length;
		int i = 0;

		if (Vector128.IsHardwareAccelerated && length >= PixelBatch4.Count) {
			int vectorEnd = length - (length % PixelBatch4.Count);
			for (; i < vectorEnd; i += PixelBatch4.Count) {
				var slice = dst.Slice (i, PixelBatch4.Count);
				var batch = PixelBatch4.Load (slice);
				var result = T.Apply4 (batch);
				result.Store (slice);
			}
		}
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
