/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
//                                                                             //
// Ported to Pinta by: Olivier Dufour <olivier.duff@gmail.com>                 //
/////////////////////////////////////////////////////////////////////////////////

using System;

namespace Pinta.ImageManipulation.Effects
{
	public class BulgeEffect : BaseEffect
	{
		private int amount;
		private PointD offset;

		public BulgeEffect (int amount, PointD offset)
		{
			if (amount < -200 || amount > 100)
				throw new ArgumentOutOfRangeException ("amount");

			this.amount = amount;
			this.offset = offset;
		}

		#region Algorithm Code Ported From PDN
		unsafe public override void Render (ISurface src, ISurface dst, Rectangle[] rois)
		{
			float bulge = (float)amount;

			float hw = dst.Width / 2f;
			float hh = dst.Height / 2f;
			float maxrad = Math.Min (hw, hh);
			float amt = bulge / 100f;

			hh = hh + (float)offset.Y * hh;
			hw = hw + (float)offset.X * hw;

			foreach (var rect in rois) {

				for (int y = rect.Top; y <= rect.Bottom; y++) {

					ColorBgra* dstPtr = dst.GetPointAddress (rect.Left, y);
					ColorBgra* srcPtr = src.GetPointAddress (rect.Left, y);
					float v = y - hh;

					for (int x = rect.Left; x <= rect.Right; x++) {
						float u = x - hw;
						float r = (float)Math.Sqrt (u * u + v * v);
						float rscale1 = (1f - (r / maxrad));

						if (rscale1 > 0) {
							float rscale2 = 1 - amt * rscale1 * rscale1;

							float xp = u * rscale2;
							float yp = v * rscale2;

							*dstPtr = Utility.GetBilinearSampleClamped (src, xp + hw, yp + hh);
						} else {
							*dstPtr = *srcPtr;
						}

						++dstPtr;
						++srcPtr;
					}
				}
			}
		}
		#endregion
	}
}
