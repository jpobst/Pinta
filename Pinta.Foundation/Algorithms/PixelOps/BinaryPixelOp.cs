/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
/////////////////////////////////////////////////////////////////////////////////

using System;

namespace Pinta.Foundation;

/// <summary>
/// Defines a way to operate on a pixel, or a region of pixels, in a binary fashion.
/// That is, it is a simple function F that takes two parameters and returns a
/// result of the form: c = F(a, b)
/// </summary>
[Serializable]
public abstract class BinaryPixelOp : PixelOp
{
	public abstract ColorBgra Apply (in ColorBgra lhs, in ColorBgra rhs);

	public virtual void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> lhs, ReadOnlySpan<ColorBgra> rhs)
	{
		for (int i = 0; i < dst.Length; ++i)
			dst[i] = Apply (lhs[i], rhs[i]);
	}

	/// <summary>
	/// Provides a default implementation for performing dst = F(lhs, rhs) over some rectangle of interest.
	/// </summary>
	public void Apply (PixelBuffer dst, PointI dstOffset,
			  PixelBuffer lhs, PointI lhsOffset,
			  PixelBuffer rhs, PointI rhsOffset,
			  Size roiSize)
	{
		// Bounds checking only enabled in Debug builds.
#if DEBUG
		var dstRect = new RectangleI (dstOffset, roiSize);
		var lhsRect = new RectangleI (lhsOffset, roiSize);
		var rhsRect = new RectangleI (rhsOffset, roiSize);

		var dstClip = RectangleI.Intersect (dstRect, dst.GetBounds ());
		var lhsClip = RectangleI.Intersect (lhsRect, lhs.GetBounds ());
		var rhsClip = RectangleI.Intersect (rhsRect, rhs.GetBounds ());

		if (dstRect != dstClip) {
			throw new ArgumentOutOfRangeException (nameof (roiSize), "Destination roi out of bounds");
		}

		if (lhsRect != lhsClip) {
			throw new ArgumentOutOfRangeException (nameof (roiSize), "lhs roi out of bounds");
		}

		if (rhsRect != rhsClip) {
			throw new ArgumentOutOfRangeException (nameof (roiSize), "rhs roi out of bounds");
		}
#endif

		int width = roiSize.Width;
		int height = roiSize.Height;
		var lhs_data = lhs.GetReadOnlyPixelData ();
		int lhs_width = lhs.Width;
		var rhs_data = rhs.GetReadOnlyPixelData ();
		int rhs_width = rhs.Width;
		var dst_data = dst.GetPixelData ();
		int dst_width = dst.Width;

		for (int row = 0; row < height; ++row) {
			Apply (dst_data.Slice ((dstOffset.Y + row) * dst_width + dstOffset.X, width),
			       lhs_data.Slice ((lhsOffset.Y + row) * lhs_width + lhsOffset.X, width),
			       rhs_data.Slice ((rhsOffset.Y + row) * rhs_width + rhsOffset.X, width));
		}
	}

	public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> src)
	{
		for (int i = 0; i < src.Length; ++i)
			dst[i] = Apply (dst[i], src[i]);
	}

	public void Apply (PixelBuffer dst, PixelBuffer src)
	{
		if (dst.GetSize () != src.GetSize ()) {
			throw new ArgumentException ("dst.Size != src.Size");
		}

		var src_data = src.GetReadOnlyPixelData ();
		var dst_data = dst.GetPixelData ();
		int width = src.Width;

		for (int y = 0; y < dst.Height; ++y) {
			Apply (dst_data.Slice (y * width, width),
			      src_data.Slice (y * width, width));
		}
	}

	public void Apply (PixelBuffer dst, PixelBuffer lhs, PixelBuffer rhs)
	{
		if (dst.GetSize () != lhs.GetSize ()) {
			throw new ArgumentException ("dst.Size != lhs.Size");
		}

		if (lhs.GetSize () != rhs.GetSize ()) {
			throw new ArgumentException ("lhs.Size != rhs.Size");
		}

		var lhs_data = lhs.GetReadOnlyPixelData ();
		var rhs_data = rhs.GetReadOnlyPixelData ();
		var dst_data = dst.GetPixelData ();
		int width = dst.Width;

		for (int y = 0; y < dst.Height; ++y) {
			Apply (dst_data.Slice (y * width, width),
			      lhs_data.Slice (y * width, width),
			      rhs_data.Slice (y * width, width));
		}
	}

	protected BinaryPixelOp ()
	{
	}
}
