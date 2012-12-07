﻿/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
//                                                                             //
// Ported to Pinta by: Krzysztof Marecki <marecki.krzysztof@gmail.com>         //
/////////////////////////////////////////////////////////////////////////////////

using System;

namespace Pinta.ImageManipulation.Effects
{
	public class BrightnessContrastEffect : BaseEffect
	{
		private int brightness;
		private int contrast;

		private int divide;
		private byte[] rgbTable;

		public BrightnessContrastEffect (int brightness, int contrast)
		{
			if (brightness < -100 || brightness > 100)
				throw new ArgumentOutOfRangeException ("brightness");
			if (contrast < -100 || contrast > 100)
				throw new ArgumentOutOfRangeException ("contrast");

			this.brightness = brightness;
			this.contrast = contrast;

			Calculate ();
		}

		#region Algorithm Code Ported From PDN
		protected override unsafe void Render (ColorBgra* src, ColorBgra* dst, int length)
		{
			ColorBgra* srcRowPtr = src;
			ColorBgra* dstRowPtr = dst;
			ColorBgra* dstRowEndPtr = dstRowPtr + length;

			if (divide == 0) {
				while (dstRowPtr < dstRowEndPtr) {
					ColorBgra col = *srcRowPtr;
					int i = col.GetIntensityByte ();
					uint c = rgbTable[i];
					dstRowPtr->Bgra = (col.Bgra & 0xff000000) | c | (c << 8) | (c << 16);

					++dstRowPtr;
					++srcRowPtr;
				}
			} else {
				while (dstRowPtr < dstRowEndPtr) {
					ColorBgra col = *srcRowPtr;
					int i = col.GetIntensityByte ();
					int shiftIndex = i * 256;

					col.R = rgbTable[shiftIndex + col.R];
					col.G = rgbTable[shiftIndex + col.G];
					col.B = rgbTable[shiftIndex + col.B];

					*dstRowPtr = col;
					++dstRowPtr;
					++srcRowPtr;
				}
			}
		}

		//protected unsafe override void Render (ISurface src, ISurface dest, Rectangle rect)
		//{
		//        for (int y = rect.Top; y <= rect.Bottom; y++) {
		//                ColorBgra* srcRowPtr = src.GetPointAddress (rect.Left, y);
		//                ColorBgra* dstRowPtr = dest.GetPointAddress (rect.Left, y);
		//                ColorBgra* dstRowEndPtr = dstRowPtr + rect.Width;

		//                if (divide == 0) {
		//                        while (dstRowPtr < dstRowEndPtr) {
		//                                ColorBgra col = *srcRowPtr;
		//                                int i = col.GetIntensityByte ();
		//                                uint c = rgbTable[i];
		//                                dstRowPtr->Bgra = (col.Bgra & 0xff000000) | c | (c << 8) | (c << 16);

		//                                ++dstRowPtr;
		//                                ++srcRowPtr;
		//                        }
		//                } else {
		//                        while (dstRowPtr < dstRowEndPtr) {
		//                                ColorBgra col = *srcRowPtr;
		//                                int i = col.GetIntensityByte ();
		//                                int shiftIndex = i * 256;

		//                                col.R = rgbTable[shiftIndex + col.R];
		//                                col.G = rgbTable[shiftIndex + col.G];
		//                                col.B = rgbTable[shiftIndex + col.B];

		//                                *dstRowPtr = col;
		//                                ++dstRowPtr;
		//                                ++srcRowPtr;
		//                        }
		//                }
		//        }
		//}

		private void Calculate ()
		{
			int multiply;

			if (contrast < 0) {
				multiply = contrast + 100;
				divide = 100;
			} else if (contrast > 0) {
				multiply = 100;
				divide = 100 - contrast;
			} else {
				multiply = 1;
				divide = 1;
			}

			if (rgbTable == null)
				rgbTable = new byte[65536];

			if (divide == 0) {
				for (int intensity = 0; intensity < 256; intensity++) {
					if (intensity + brightness < 128)
						rgbTable[intensity] = 0;
					else
						rgbTable[intensity] = 255;
				}
			} else if (divide == 100) {
				for (int intensity = 0; intensity < 256; intensity++) {
					int shift = (intensity - 127) * multiply / divide + 127 - intensity + brightness;

					for (int col = 0; col < 256; ++col) {
						int index = (intensity * 256) + col;
						rgbTable[index] = Utility.ClampToByte (col + shift);
					}
				}
			} else {
				for (int intensity = 0; intensity < 256; ++intensity) {
					int shift = (intensity - 127 + brightness) * multiply / divide + 127 - intensity;

					for (int col = 0; col < 256; ++col) {
						int index = (intensity * 256) + col;
						rgbTable[index] = Utility.ClampToByte (col + shift);
					}
				}
			}
		}
		#endregion
	}
}
