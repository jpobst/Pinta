/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
//                                                                             //
// Ported to Pinta by: Jonathan Pobst <monkey@jpobst.com>                      //
/////////////////////////////////////////////////////////////////////////////////

using System;

namespace Pinta.ImageManipulation.Effects
{
	public class Effect : BaseEffect
	{
		private int radius;

		public Effect (int radius)
		{
			if (radius < 0 || radius > 200)
				throw new ArgumentOutOfRangeException ("radius");

			this.radius = radius;
		}

		#region Algorithm Code Ported From PDN
		#endregion
	}
}
