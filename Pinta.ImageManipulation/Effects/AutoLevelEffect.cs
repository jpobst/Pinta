﻿/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
//                                                                             //
// Ported to Pinta by: Jonathan Pobst <monkey@jpobst.com>                      //
/////////////////////////////////////////////////////////////////////////////////

using System;
using Pinta.ImageManipulation.UnaryPixelOperations;

namespace Pinta.ImageManipulation.Effects
{
	public class AutoLevelEffect : BaseEffect
	{
		private LevelOp op;

		#region Algorithm Code Ported From PDN
		protected override void Render (ISurface src, ISurface dest, Rectangle roi)
		{
			if (op == null) {
				HistogramRgb histogram = new HistogramRgb ();
				histogram.UpdateHistogram (src, src.Bounds);

				op = histogram.MakeLevelsAuto ();
			}

			if (op.isValid)
				op.Apply (src, dest, roi);
		}
		#endregion
	}
}
