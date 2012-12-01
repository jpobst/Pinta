/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
//                                                                             //
// Ported to Pinta by: Hanh Pham <hanh.pham@gmx.com>                           //
/////////////////////////////////////////////////////////////////////////////////

using System;

namespace Pinta.ImageManipulation.Effects
{
	public class AddNoiseEffect : BaseEffect
	{
		[ThreadStatic]
		private static Random threadRand = new Random ();
		private const int tableSize = 16384;
		private static int[] lookup;

		private int intensity;
		private int color_saturation;
		private double coverage;

		public AddNoiseEffect (int intensity, int colorSaturation, double coverage)
		{
			if (intensity < 0 || intensity > 100)
				throw new ArgumentOutOfRangeException ("intensity");
			if (colorSaturation < 0 || colorSaturation > 400)
				throw new ArgumentOutOfRangeException ("colorSaturation");
			if (coverage < 0 || coverage > 100)
				throw new ArgumentOutOfRangeException ("coverage");

			this.intensity = intensity;
			this.color_saturation = colorSaturation;
			this.coverage = coverage * 0.01;
		}

		static AddNoiseEffect ()
		{
			InitLookup ();
		}

		#region Algorithm Code Ported From PDN
		public unsafe override void Render (ISurface src, ISurface dst, Rectangle[] rois)
		{
			int dev = this.intensity * this.intensity / 4;
			int sat = this.color_saturation * 4096 / 100;

			if (threadRand == null) {
				threadRand = new Random (unchecked (System.Threading.Thread.CurrentThread.GetHashCode () ^
				    unchecked ((int)DateTime.Now.Ticks)));
			}

			Random localRand = threadRand;
			int[] localLookup = lookup;

			foreach (var rect in rois) {
				for (int y = rect.Top; y <= rect.Bottom; ++y) {
					ColorBgra* srcPtr = src.GetPointAddress (rect.Left, y);
					ColorBgra* dstPtr = dst.GetPointAddress (rect.Left, y);

					for (int x = 0; x < rect.Width; ++x) {
						if (localRand.NextDouble () > this.coverage) {
							*dstPtr = *srcPtr;
						} else {
							int r;
							int g;
							int b;
							int i;

							r = localLookup[localRand.Next (tableSize)];
							g = localLookup[localRand.Next (tableSize)];
							b = localLookup[localRand.Next (tableSize)];

							i = (4899 * r + 9618 * g + 1867 * b) >> 14;


							r = i + (((r - i) * sat) >> 12);
							g = i + (((g - i) * sat) >> 12);
							b = i + (((b - i) * sat) >> 12);

							dstPtr->R = Utility.ClampToByte (srcPtr->R + ((r * dev + 32768) >> 16));
							dstPtr->G = Utility.ClampToByte (srcPtr->G + ((g * dev + 32768) >> 16));
							dstPtr->B = Utility.ClampToByte (srcPtr->B + ((b * dev + 32768) >> 16));
							dstPtr->A = srcPtr->A;
						}

						++srcPtr;
						++dstPtr;
					}
				}
			}
		}

		private static double NormalCurve (double x, double scale)
		{
			return scale * Math.Exp (-x * x / 2);
		}

		private static void InitLookup ()
		{
			double l = 5;
			double r = 10;
			double scale = 50;
			double sum = 0;

			while (r - l > 0.0000001) {
				sum = 0;
				scale = (l + r) * 0.5;

				for (int i = 0; i < tableSize; ++i) {
					sum += NormalCurve (16.0 * ((double)i - tableSize / 2) / tableSize, scale);

					if (sum > 1000000) {
						break;
					}
				}

				if (sum > tableSize) {
					r = scale;
				} else if (sum < tableSize) {
					l = scale;
				} else {
					break;
				}
			}

			lookup = new int[tableSize];
			sum = 0;
			int roundedSum = 0, lastRoundedSum;

			for (int i = 0; i < tableSize; ++i) {
				sum += NormalCurve (16.0 * ((double)i - tableSize / 2) / tableSize, scale);
				lastRoundedSum = roundedSum;
				roundedSum = (int)sum;

				for (int j = lastRoundedSum; j < roundedSum; ++j) {
					lookup[j] = (i - tableSize / 2) * 65536 / tableSize;
				}
			}
		}
		#endregion
	}
}
