/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
//                                                                             //
// Ported to Pinta by: Jonathan Pobst <monkey@jpobst.com>                      //
/////////////////////////////////////////////////////////////////////////////////

using System;
using Pinta.ImageManipulation.UnaryPixelOperations;
using Pinta.ImageManipulation.PixelBlendOperations;

namespace Pinta.ImageManipulation.Effects
{
	public class PencilSketchEffect : BaseEffect
	{
		private GaussianBlurEffect blurEffect;
		private DesaturateOp desaturateOp;
		//private InvertColorsEffect invertEffect;
		//private BrightnessContrastEffect bacAdjustment;
		private ColorDodgeBlendOp colorDodgeOp;

		private int pencil_size;
		private int color_range;

		public PencilSketchEffect (int pencilSize, int colorRange)
		{
			if (pencilSize < 1 || pencilSize > 20)
				throw new ArgumentOutOfRangeException ("pencilSize");
			if (colorRange < -20 || colorRange > 20)
				throw new ArgumentOutOfRangeException ("colorRange");

			this.pencil_size = pencilSize;
			this.color_range = colorRange;

			blurEffect = new GaussianBlurEffect (pencil_size);
			desaturateOp = new DesaturateOp ();
			//invertEffect = new InvertColorsEffect ();
			//bacAdjustment = new BrightnessContrastEffect ();
			colorDodgeOp = new ColorDodgeBlendOp ();
		}

		#region Algorithm Code Ported From PDN
		public unsafe override void Render (ISurface src, ISurface dest, Rectangle[] rois)
		{
			//bacAdjustment.Data.Brightness = -Data.ColorRange;
			//bacAdjustment.Data.Contrast = -Data.ColorRange;
			//bacAdjustment.Render (src, dest, rois);

			blurEffect.Render (src, dest, rois);

			//invertEffect.Render (dest, dest, rois);
			desaturateOp.Apply (dest, dest, rois);

			foreach (var roi in rois) {
				for (int y = roi.Top; y <= roi.Bottom; ++y) {
					var srcPtr = src.GetPointAddress (roi.X, y);
					var dstPtr = dest.GetPointAddress (roi.X, y);

					for (int x = roi.Left; x <= roi.Right; ++x) {
						var srcGrey = desaturateOp.Apply (*srcPtr);
						var sketched = colorDodgeOp.Apply (srcGrey, *dstPtr);
						*dstPtr = sketched;

						++srcPtr;
						++dstPtr;
					}
				}
			}
		}
		#endregion
	}
}
