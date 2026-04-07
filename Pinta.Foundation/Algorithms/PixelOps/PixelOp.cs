/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) Rick Brewster, Tom Jackson, and past contributors.            //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
/////////////////////////////////////////////////////////////////////////////////

using System;

namespace Pinta.Foundation;

/// <summary>
/// Base class for pixel operations that take a source and destination buffer.
/// </summary>
[Serializable]
public abstract class PixelOp
{
	public PixelOp ()
	{
	}

	/// <summary>
	/// Computes alpha for r OVER l operation.
	/// </summary>
	public static byte ComputeAlpha (byte la, byte ra)
	{
		return (byte) (((la * (256 - (ra + (ra >> 7)))) >> 8) + ra);
	}

	public void Apply (PixelBuffer dst, PixelBuffer src, ReadOnlySpan<RectangleI> rois, int startIndex, int length)
	{
		for (int i = startIndex; i < startIndex + length; ++i)
			ApplyBase (dst, rois[i].Location, src, rois[i].Location, rois[i].Size);
	}

	public void Apply (PixelBuffer dst, PointI dstOffset, PixelBuffer src, PointI srcOffset, Size roiSize)
	{
		ApplyBase (dst, dstOffset, src, srcOffset, roiSize);
	}

	/// <summary>
	/// Provides a default implementation for performing dst = F(dst, src) or F(src) over some rectangle
	/// of interest.
	/// </summary>
	public void ApplyBase (PixelBuffer dst, PointI dstOffset, PixelBuffer src, PointI srcOffset, Size roiSize)
	{
		// Create bounding rectangles for each surface
		var dstRect = new RectangleI (dstOffset, roiSize);

		if (dstRect.Width == 0 || dstRect.Height == 0)
			return;

		var srcRect = new RectangleI (srcOffset, roiSize);

		if (srcRect.Width == 0 || srcRect.Height == 0)
			return;

		// Clip those rectangles to the surface bounding rectangles
		var dstClip = RectangleI.Intersect (dstRect, dst.GetBounds ());
		var srcClip = RectangleI.Intersect (srcRect, src.GetBounds ());

		// If any of those Rectangles actually got clipped, then throw an exception
		if (dstRect != dstClip)
			throw new ArgumentOutOfRangeException
			(
			    nameof (roiSize),
			    "Destination roi out of bounds" +
			    $", dst.Size=({dst.Width},{dst.Height}" +
			    ", dst.Bounds=" + dst.GetBounds ().ToString () +
			    ", dstOffset=" + dstOffset.ToString () +
			    $", src.Size=({src.Width},{src.Height}" +
			    ", srcOffset=" + srcOffset.ToString () +
			    ", roiSize=" + roiSize.ToString () +
			    ", dstRect=" + dstRect.ToString () +
			    ", dstClip=" + dstClip.ToString () +
			    ", srcRect=" + srcRect.ToString () +
			    ", srcClip=" + srcClip.ToString ()
			);

		if (srcRect != srcClip)
			throw new ArgumentOutOfRangeException (nameof (roiSize), "Source roi out of bounds");

		// Cache the width and height properties
		int width = roiSize.Width;
		int height = roiSize.Height;
		var src_data = src.GetReadOnlyPixelData ();
		int src_width = src.Width;
		var dst_data = dst.GetPixelData ();
		int dst_width = dst.Width;

		// Do the work.
		for (int row = 0; row < height; ++row) {
			Apply (dst_data.Slice ((dstOffset.Y + row) * dst_width + dstOffset.X, width),
			       src_data.Slice ((srcOffset.Y + row) * src_width + srcOffset.X, width));
		}
	}

	public virtual void Apply (PixelBuffer dst, PointI dstOffset, PixelBuffer src, PointI srcOffset, int scanLength)
	{
		Apply (dst.GetPixelData ().Slice (dstOffset.Y * dst.Width + dstOffset.X, scanLength),
		       src.GetPixelData ().Slice (srcOffset.Y * src.Width + srcOffset.X, scanLength));
	}

	public abstract void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> src);
}
