/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) Rick Brewster, Tom Jackson, and past contributors.            //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
/////////////////////////////////////////////////////////////////////////////////

using System;

namespace Pinta.Foundation;

/// <summary>
/// Represents a color in Hue-Saturation-Value color space.
/// </summary>
public readonly struct HsvColor : IColor<HsvColor>, IEquatable<HsvColor>
{
	/// <summary>Hue in range [0, 360]</summary>
	public double Hue { get; init; }

	/// <summary>Saturation in range [0, 1]</summary>
	public double Saturation { get; init; }

	/// <summary>Value (brightness) in range [0, 1]</summary>
	public double Value { get; init; }

	public static HsvColor Black => new (0, 0, 0);
	public static HsvColor Red => new (0, 1, 1);
	public static HsvColor Green => new (120, 1, 1);
	public static HsvColor Blue => new (240, 1, 1);
	public static HsvColor Yellow => new (60, 1, 1);
	public static HsvColor Magenta => new (300, 1, 1);
	public static HsvColor Cyan => new (180, 1, 1);
	public static HsvColor White => new (0, 0, 1);

	public HsvColor (double hue, double saturation, double value)
	{
		if (hue < 0 || hue > 360)
			throw new ArgumentOutOfRangeException (nameof (hue), "must be in the range [0, 360]");

		if (saturation < 0 || saturation > 1)
			throw new ArgumentOutOfRangeException (nameof (saturation), "must be in the range [0, 1]");

		if (value < 0 || value > 1)
			throw new ArgumentOutOfRangeException (nameof (value), "must be in the range [0, 1]");

		Hue = hue;
		Saturation = saturation;
		Value = value;
	}

	/// <summary>
	/// Creates an HsvColor from a ColorBgra value.
	/// </summary>
	public static HsvColor FromBgra (in ColorBgra c)
	{
		double r = c.R / 255.0;
		double g = c.G / 255.0;
		double b = c.B / 255.0;

		double max = Math.Max (r, Math.Max (g, b));
		double min = Math.Min (r, Math.Min (g, b));
		double delta = max - min;

		double hue;
		double saturation;
		double value = max;

		if (delta == 0) {
			hue = 0;
			saturation = 0;
		} else {
			saturation = delta / max;

			if (max == r) {
				hue = 60.0 * ((g - b) / delta % 6);
			} else if (max == g) {
				hue = 60.0 * ((b - r) / delta + 2);
			} else {
				hue = 60.0 * ((r - g) / delta + 4);
			}

			if (hue < 0) hue += 360;
		}

		return new (hue, saturation, value);
	}

	/// <summary>
	/// Converts this HsvColor to a ColorBgra value.
	/// </summary>
	public ColorBgra ToColorBgra (byte alpha = 255)
	{
		double c = Value * Saturation;
		double h = Hue / 60.0;
		double x = c * (1 - Math.Abs (h % 2 - 1));
		double m = Value - c;

		double r, g, b;

		if (h < 1) { r = c; g = x; b = 0; }
		else if (h < 2) { r = x; g = c; b = 0; }
		else if (h < 3) { r = 0; g = c; b = x; }
		else if (h < 4) { r = 0; g = x; b = c; }
		else if (h < 5) { r = x; g = 0; b = c; }
		else { r = c; g = 0; b = x; }

		byte rb = (byte) Math.Round ((r + m) * 255);
		byte gb = (byte) Math.Round ((g + m) * 255);
		byte bb = (byte) Math.Round ((b + m) * 255);

		return ColorBgra.FromBgra (bb, gb, rb, alpha);
	}

	public override readonly string ToString ()
		=> $"({Hue:F2}, {Saturation:F2}, {Value:F2})";

	public override int GetHashCode ()
		=> HashCode.Combine (Hue, Saturation, Value);

	public override readonly bool Equals (object? obj)
		=> obj is HsvColor other && Equals (other);

	public readonly bool Equals (HsvColor other)
		=> Hue == other.Hue && Saturation == other.Saturation && Value == other.Value;

	public static bool operator == (HsvColor left, HsvColor right) => left.Equals (right);
	public static bool operator != (HsvColor left, HsvColor right) => !left.Equals (right);
}
