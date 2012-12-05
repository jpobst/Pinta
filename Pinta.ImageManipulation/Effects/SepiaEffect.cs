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
	public class SepiaEffect : BaseEffect
	{
		private DesaturateOp desat_op = new DesaturateOp ();
		private LevelOp level_op = new LevelOp (
				ColorBgra.Black,
				ColorBgra.White,
				new float[] { 1.2f, 1.0f, 0.8f },
				ColorBgra.Black,
				ColorBgra.White);

		#region Algorithm Code Ported From PDN
		public override void Render (ISurface src, ISurface dest, Rectangle[] rois)
		{
			desat_op.Apply (dest, src, rois);
			level_op.Apply (dest, dest, rois);
		}
		#endregion
	}
}