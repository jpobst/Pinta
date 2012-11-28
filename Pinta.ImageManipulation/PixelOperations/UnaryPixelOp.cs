/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) Rick Brewster, Tom Jackson, and past contributors.            //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
/////////////////////////////////////////////////////////////////////////////////

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Pinta.ImageManipulation
{
	/// <summary>
	/// Defines a way to operate on a pixel, or a region of pixels, in a unary fashion.
	/// That is, it is a simple function F that takes one parameter and returns a
	/// result of the form: d = F(c)
	/// </summary>
	[Serializable]
	public unsafe abstract class UnaryPixelOp : PixelOp
	{
		public abstract ColorBgra Apply (ColorBgra color);

		public unsafe override void Apply (ColorBgra* dst, ColorBgra* src, int length)
		{
			unsafe {
				while (length > 0) {
					*dst = Apply (*src);
					++dst;
					++src;
					--length;
				}
			}
		}

		public unsafe virtual void Apply (ColorBgra* ptr, int length)
		{
			unsafe {
				while (length > 0) {
					*ptr = Apply (*ptr);
					++ptr;
					--length;
				}
			}
		}

		private unsafe void ApplyRectangle (ISurface surface, Rectangle rect)
		{
			Parallel.For (rect.Top, rect.Bottom, (i) => {
				Apply (surface.GetRowAddress (i), rect.Width);
			});
			//for (var y = rect.Top; y <= rect.Bottom; ++y) {
			//        var ptr = surface.GetRowAddress (y);
			//        Apply (ptr, rect.Width);
			//}
		}

		public void Apply (ISurface surface, Rectangle[] roi, int startIndex, int length)
		{
			Rectangle regionBounds = Utility.GetRegionBounds (roi, startIndex, length);

			if (regionBounds != Rectangle.Intersect (surface.Bounds, regionBounds))
				throw new ArgumentOutOfRangeException ("roi", "Region is out of bounds");

			unsafe {
				for (int x = startIndex; x < startIndex + length; ++x)
					ApplyRectangle (surface, roi[x]);
			}
		}

		public void Apply (ISurface surface, Rectangle[] roi)
		{
			Apply (surface, roi, 0, roi.Length);
		}

		public unsafe void Apply (ISurface surface, Rectangle roi)
		{
			ApplyRectangle (surface, roi);
		}

		public override void Apply (ISurface dst, Point dstOffset, ISurface src, Point srcOffset, int scanLength)
		{
			Apply (dst.GetPointAddress (dstOffset), src.GetPointAddress (srcOffset), scanLength);
		}

		public void Apply (ISurface dst, ISurface src, Rectangle roi)
		{
			for (int y = roi.Y; y <= roi.Bottom; ++y) {
				ColorBgra* dstPtr = dst.GetPointAddress (roi.X, y);
				ColorBgra* srcPtr = src.GetPointAddress (roi.X, y);
				Apply (dstPtr, srcPtr, roi.Width);
			}
		}

		public void Apply (ISurface dst, ISurface src, Rectangle[] rois)
		{
			foreach (Rectangle roi in rois)
				Apply (dst, src, roi);
		}
	}
}
