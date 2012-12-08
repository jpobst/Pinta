/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
//                                                                             //
// Ported to Pinta by: Jonathan Pobst <monkey@jpobst.com>                      //
/////////////////////////////////////////////////////////////////////////////////

using System;
using System.Threading.Tasks;

namespace Pinta.ImageManipulation
{
	/// <summary>
	/// Defines a way to operate on a pixel, or a region of pixels, in a binary fashion.
	/// That is, it is a simple function F that takes two parameters and returns a
	/// result of the form: c = F(a, b)
	/// </summary>
	public unsafe abstract class BinaryPixelOp : PixelOp
	{
		public abstract ColorBgra Apply (ColorBgra lhs, ColorBgra rhs);

		public void Apply (ISurface src, ISurface dst)
		{
			if (dst.Size != src.Size)
				throw new ArgumentException ("dst.Size != src.Size");

			Apply (src, dst, src.Bounds);
		}

		public unsafe void Apply (ISurface src, ISurface dst, Rectangle roi)
		{
			if (Settings.SingleThreaded || roi.Height <= 1) {
				for (var y = roi.Y; y <= roi.Bottom; ++y) {
					var dstPtr = dst.GetRowAddress (y);
					var srcPtr = src.GetRowAddress (y);
					Apply (srcPtr, dstPtr, roi.Width);
				}
			} else {
				ParallelExtensions.OrderedFor (roi.Y, roi.Bottom + 1, (y) => {
					var dstPtr = dst.GetRowAddress (y);
					var srcPtr = src.GetRowAddress (y);
					Apply (srcPtr, dstPtr, roi.Width);
				});
			}
		}

		public void Apply (ISurface lhs, ISurface rhs, ISurface dst)
		{
			if (dst.Size != lhs.Size)
				throw new ArgumentException ("dst.Size != lhs.Size");

			if (lhs.Size != rhs.Size)
				throw new ArgumentException ("lhs.Size != rhs.Size");

			Apply (lhs, rhs, dst, dst.Bounds);
		}

		public unsafe void Apply (ISurface lhs, ISurface rhs, ISurface dst, Rectangle roi)
		{
			if (Settings.SingleThreaded || roi.Height <= 1) {
				for (var y = roi.Y; y <= roi.Bottom; ++y) {
					var dstPtr = dst.GetRowAddress (y);
					var lhsPtr = lhs.GetRowAddress (y);
					var rhsPtr = rhs.GetRowAddress (y);

					Apply (lhsPtr, rhsPtr, dstPtr, roi.Width);
				}
			} else {
				ParallelExtensions.OrderedFor (roi.Y, roi.Bottom + 1, (y) => {
					var dstPtr = dst.GetRowAddress (y);
					var lhsPtr = lhs.GetRowAddress (y);
					var rhsPtr = rhs.GetRowAddress (y);

					Apply (lhsPtr, rhsPtr, dstPtr, roi.Width);
				});
			}
		}

		public virtual void Apply (ColorBgra* lhs, ColorBgra* rhs, ColorBgra* dst, int length)
		{
			unsafe {
				while (length > 0) {
					*dst = Apply (*lhs, *rhs);
					++dst;
					++lhs;
					++rhs;
					--length;
				}
			}
		}

		public unsafe override void Apply (ColorBgra* src, ColorBgra* dst, int length)
		{
			unsafe {
				while (length > 0) {
					*dst = Apply (*dst, *src);
					++dst;
					++src;
					--length;
				}
			}
		}
	}
}
