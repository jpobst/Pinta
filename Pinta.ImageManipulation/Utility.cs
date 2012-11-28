/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) Rick Brewster, Tom Jackson, and past contributors.            //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
/////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Pinta.ImageManipulation
{
	class Utility
	{
		/// <summary>
		/// Allows you to find the bounding box for a "region" that is described as an
		/// array of bounding boxes.
		/// </summary>
		/// <param name="rectsF">The "region" you want to find a bounding box for.</param>
		/// <returns>A RectangleF structure that surrounds the Region.</returns>
		public static Rectangle GetRegionBounds (Rectangle[] rects, int startIndex, int length)
		{
			if (rects.Length == 0) {
				return Rectangle.Empty;
			}

			int left = rects[startIndex].Left;
			int top = rects[startIndex].Top;
			int right = rects[startIndex].Right;
			int bottom = rects[startIndex].Bottom;

			for (int i = startIndex + 1; i < startIndex + length; ++i) {
				Rectangle rect = rects[i];

				if (rect.Left < left) {
					left = rect.Left;
				}

				if (rect.Top < top) {
					top = rect.Top;
				}

				if (rect.Right > right) {
					right = rect.Right;
				}

				if (rect.Bottom > bottom) {
					bottom = rect.Bottom;
				}
			}

			return Rectangle.FromLTRB (left, top, right, bottom);
		}

		public static double Clamp (double x, double min, double max)
		{
			if (x < min) {
				return min;
			} else if (x > max) {
				return max;
			} else {
				return x;
			}
		}

		public static float Clamp (float x, float min, float max)
		{
			if (x < min) {
				return min;
			} else if (x > max) {
				return max;
			} else {
				return x;
			}
		}

		public static int Clamp (int x, int min, int max)
		{
			if (x < min) {
				return min;
			} else if (x > max) {
				return max;
			} else {
				return x;
			}
		}

		public static byte ClampToByte (double x)
		{
			if (x > 255) {
				return 255;
			} else if (x < 0) {
				return 0;
			} else {
				return (byte)x;
			}
		}

		public static byte ClampToByte (float x)
		{
			if (x > 255) {
				return 255;
			} else if (x < 0) {
				return 0;
			} else {
				return (byte)x;
			}
		}

		public static byte ClampToByte (int x)
		{
			if (x > 255) {
				return 255;
			} else if (x < 0) {
				return 0;
			} else {
				return (byte)x;
			}
		}
	}
}
