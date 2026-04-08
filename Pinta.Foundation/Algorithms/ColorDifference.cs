/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) Rick Brewster, Tom Jackson, and past contributors.            //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
/////////////////////////////////////////////////////////////////////////////////

using System;

namespace Pinta.Foundation;

/// <summary>
/// ColorDifference is a utility class for difference effects
/// that have floating point (double) convolution filters.
/// Limited to 3x3 kernels.
/// </summary>
public static class ColorDifference
{
	public static void RenderColorDifferenceEffect (
		double[,] weights,
		PixelBuffer source,
		PixelBuffer destination,
		ReadOnlySpan<RectangleI> rois)
	{
		if (weights.GetLength (0) != 3 || weights.GetLength (1) != 3) throw new ArgumentException ("Must be a 3x3 array", nameof (weights));

		RectangleI surfaceBounds = source.GetBounds ();

		ReadOnlySpan<ColorBgra> sourceData = source.GetReadOnlyPixelData ();
		Span<ColorBgra> destinationData = destination.GetPixelData ();

		foreach (RectangleI rect in rois) {

			foreach (var pixel in Tiling.GeneratePixelOffsets (rect, source.GetSize ())) {

				destinationData[pixel.memoryOffset] = GetFinalPixelColor (
					weights,
					sourceData,
					surfaceBounds,
					pixel.coordinates);
			}
		}
	}

	private static ColorBgra GetFinalPixelColor (
		double[,] weights,
		ReadOnlySpan<ColorBgra> sourceData,
		RectangleI surfaceBounds,
		PointI coordinates)
	{
		PointI fStart = new (
			X: (coordinates.X == surfaceBounds.X) ? 1 : 0,
			Y: (coordinates.Y == surfaceBounds.Y) ? 1 : 0);

		PointI fEnd = new (
			X: (coordinates.X == surfaceBounds.X + surfaceBounds.Width - 1) ? 2 : 3,
			Y: (coordinates.Y == surfaceBounds.Y + surfaceBounds.Height - 1) ? 2 : 3);

		double rSum = 0.0;
		double gSum = 0.0;
		double bSum = 0.0;

		for (int fy = fStart.Y; fy < fEnd.Y; ++fy) {
			for (int fx = fStart.X; fx < fEnd.X; ++fx) {
				double weight = weights[fy, fx];
				ColorBgra c = sourceData[(coordinates.Y - 1 + fy) * surfaceBounds.Width + (coordinates.X - 1 + fx)];
				rSum += weight * c.R;
				gSum += weight * c.G;
				bSum += weight * c.B;
			}
		}

		byte iRsum = FoundationUtility.ClampToByte (rSum);
		byte iGsum = FoundationUtility.ClampToByte (gSum);
		byte iBsum = FoundationUtility.ClampToByte (bSum);

		return ColorBgra.FromBgra (iBsum, iGsum, iRsum, 255);
	}
}
