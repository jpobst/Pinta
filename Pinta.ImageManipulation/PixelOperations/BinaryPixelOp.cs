/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
//                                                                             //
// Ported to Pinta by: Jonathan Pobst <monkey@jpobst.com>                      //
/////////////////////////////////////////////////////////////////////////////////

using System;

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

		public void Apply (ISurface dst, ISurface src)
		{
			if (dst.Size != src.Size)
				throw new ArgumentException ("dst.Size != src.Size");

			Apply (dst, src, src.Bounds);
		}

		public void Apply (ISurface dst, ISurface src, Rectangle roi)
		{
			unsafe {
				for (var y = roi.Y; y <= roi.Bottom; ++y) {
					var dstPtr = dst.GetRowAddress (y);
					var srcPtr = src.GetRowAddress (y);
					Apply (dstPtr, srcPtr, roi.Width);
				}
			}
		}

		public void Apply (ISurface dst, ISurface lhs, ISurface rhs)
		{
			if (dst.Size != lhs.Size)
				throw new ArgumentException ("dst.Size != lhs.Size");

			if (lhs.Size != rhs.Size)
				throw new ArgumentException ("lhs.Size != rhs.Size");

			Apply (dst, lhs, rhs, dst.Bounds);
		}

		public void Apply (ISurface dst, ISurface lhs, ISurface rhs, Rectangle roi)
		{
			unsafe {
				for (var y = roi.Y; y <= roi.Bottom; ++y) {
					var dstPtr = dst.GetRowAddress (y);
					var lhsPtr = lhs.GetRowAddress (y);
					var rhsPtr = rhs.GetRowAddress (y);

					Apply (dstPtr, lhsPtr, rhsPtr, roi.Width);
				}
			}
		}

		public virtual void Apply (ColorBgra* dst, ColorBgra* lhs, ColorBgra* rhs, int length)
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

		public unsafe override void Apply (ColorBgra* dst, ColorBgra* src, int length)
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
