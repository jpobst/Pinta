﻿/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
//                                                                             //
// Ported to Pinta by: Jonathan Pobst <monkey@jpobst.com>                      //
/////////////////////////////////////////////////////////////////////////////////

using System;
using Pinta.ImageManipulation.PixelBlendOperations;

namespace Pinta.ImageManipulation.Effects
{
	public class GlowEffect : BaseEffect
	{
		private int radius;
		private int brightness;
		private int contrast;

		private GaussianBlurEffect blur_effect;
		private BrightnessContrastEffect contrast_effect;
		private ScreenBlendOp screen_op;

		public GlowEffect (int radius, int brightness, int contrast)
		{
			if (radius < 1 || radius > 20)
				throw new ArgumentOutOfRangeException ("radius");
			if (brightness < -100 || brightness > 100)
				throw new ArgumentOutOfRangeException ("brightness");
			if (contrast < -100 || contrast > 100)
				throw new ArgumentOutOfRangeException ("contrast");

			this.radius = radius;
			this.brightness = brightness;
			this.contrast = contrast;

			blur_effect = new GaussianBlurEffect (radius);
			contrast_effect = new BrightnessContrastEffect (brightness, contrast);
			screen_op = new ScreenBlendOp ();
		}

		#region Algorithm Code Ported From PDN
		protected unsafe override void RenderLine (ISurface src, ISurface dest, Rectangle roi)
		{
			blur_effect.Render (src, dest, roi);
			contrast_effect.Render (dest, dest, roi);

			for (var y = roi.Top; y <= roi.Bottom; ++y) {
				var dstPtr = dest.GetPointAddress (roi.Left, y);
				var srcPtr = src.GetPointAddress (roi.Left, y);

				screen_op.Apply (srcPtr, dstPtr, dstPtr, roi.Width);
			}
		}
		#endregion
	}
}
