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
	public unsafe abstract class UnaryPixelOp : PixelOp
	{
		public abstract ColorBgra Apply (ColorBgra color);

		public void Apply (ISurface surface)
		{
			Apply (surface, surface.Bounds);
		}

		public void Apply (ISurface surface, Rectangle roi)
		{
			if (Settings.SingleThreaded || roi.Height <= 1) {
				for (var y = roi.Y; y <= roi.Bottom; ++y) {
					var dstPtr = surface.GetPointAddress (roi.X, y);
					Apply (dstPtr, roi.Width);
				}
			} else {
				ParallelExtensions.OrderedFor (roi.Y, roi.Bottom + 1, (y) => {
					var dstPtr = surface.GetPointAddress (roi.X, y);
					Apply (dstPtr, roi.Width);
				});
			}
		}

		public void Apply (ISurface src, ISurface dst)
		{
			if (src.Bounds != dst.Bounds)
				throw new InvalidOperationException ("Source and destination surfaces must be the same size or use an overload with a specified bounds.");

			Apply (src, dst, src.Bounds);
		}

		public void Apply (ISurface src, ISurface dst, Rectangle roi)
		{
			if (Settings.SingleThreaded) {
				for (var y = roi.Y; y <= roi.Bottom; ++y) {
					var dstPtr = dst.GetPointAddress (roi.X, y);
					var srcPtr = src.GetPointAddress (roi.X, y);
					Apply (srcPtr, dstPtr, roi.Width);
				}
			} else {
				ParallelExtensions.OrderedFor (roi.Y, roi.Bottom + 1, (y) => {
					var dstPtr = dst.GetPointAddress (roi.X, y);
					var srcPtr = src.GetPointAddress (roi.X, y);
					Apply (srcPtr, dstPtr, roi.Width);
				});
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

		public unsafe override void Apply (ColorBgra* src, ColorBgra* dst, int length)
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
	}
}
