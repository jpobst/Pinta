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
	public class EdgeDetectEffect : ColorDifferenceEffect
	{
		private double[][] weights;
		private double angle;

		public EdgeDetectEffect (double angle)
		{
			this.angle = angle;

			SetWeights ();
		}

		#region Algorithm Code Ported From PDN
		protected override unsafe void RenderLine (ISurface src, ISurface dst, Rectangle roi)
		{
			base.RenderColorDifferenceEffect (weights, src, dst, roi);
		}

		private void SetWeights ()
		{
			weights = new double[3][];

			for (int i = 0; i < this.weights.Length; ++i) {
				this.weights[i] = new double[3];
			}

			// adjust and convert angle to radians
			double r = (double)angle * 2.0 * Math.PI / 360.0;

			// angle delta for each weight
			double dr = Math.PI / 4.0;

			// for r = 0 this builds an edge detect filter pointing straight left

			this.weights[0][0] = Math.Cos (r + dr);
			this.weights[0][1] = Math.Cos (r + 2.0 * dr);
			this.weights[0][2] = Math.Cos (r + 3.0 * dr);

			this.weights[1][0] = Math.Cos (r);
			this.weights[1][1] = 0;
			this.weights[1][2] = Math.Cos (r + 4.0 * dr);

			this.weights[2][0] = Math.Cos (r - dr);
			this.weights[2][1] = Math.Cos (r - 2.0 * dr);
			this.weights[2][2] = Math.Cos (r - 3.0 * dr);
		}
		#endregion
	}
}
