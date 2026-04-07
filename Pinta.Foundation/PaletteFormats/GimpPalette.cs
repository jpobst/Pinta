using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Pinta.Foundation;

/// <summary>
/// Loads and saves GIMP palette format files.
/// Uses streams instead of GIO for toolkit independence.
/// </summary>
public sealed class GimpPalette : IPaletteLoader, IPaletteSaver
{
	public List<ColorBgra> Load (Stream stream)
	{
		List<ColorBgra> colors = [];
		using StreamReader reader = new (stream, leaveOpen: true);
		string? line = reader.ReadLine ();

		if (line is null || !line.StartsWith ("GIMP"))
			throw new InvalidDataException ("Not a valid GIMP palette file.");

		// skip everything until the first color
		while (line != null && (line.Length == 0 || !char.IsDigit (line.TrimStart ()[0])))
			line = reader.ReadLine ();

		if (line == null)
			return colors;

		// then read the palette
		do {
			if (line.Length == 0 || line.StartsWith ('#'))
				continue;

			string trimmed = line.TrimStart ();
			if (trimmed.Length > 0 && char.IsDigit (trimmed[0])) {
				var finalColor = ReadColor (trimmed);
				colors.Add (finalColor);
			}
		} while ((line = reader.ReadLine ()) != null);

		return colors;
	}

	private static ColorBgra ReadColor (string line)
	{
		string[] split = line.Split ((char[]?) null, StringSplitOptions.RemoveEmptyEntries);
		byte r = byte.Parse (split[0]);
		byte g = byte.Parse (split[1]);
		byte b = byte.Parse (split[2]);
		return ColorBgra.FromBgra (b, g, r, 255);
	}

	public void Save (IReadOnlyList<ColorBgra> colors, Stream stream)
	{
		using StreamWriter writer = new (stream, leaveOpen: true);

		writer.WriteLine ("GIMP Palette");
		writer.WriteLine ("Name: Pinta Created {0}", DateTime.Now.ToString (DateTimeFormatInfo.InvariantInfo.RFC1123Pattern));
		writer.WriteLine ("#");

		for (var i = 0; i < colors.Count; i++)
			writer.WriteLine (RepresentColor (colors[i], $"Untitled_{i}"));
	}

	private static string RepresentColor (ColorBgra color, string colorName)
		=> string.Format (
			"{0,3} {1,3} {2,3} {3}",
			color.R,
			color.G,
			color.B,
			colorName
		);
}
