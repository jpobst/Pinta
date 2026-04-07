using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Pinta.Foundation;

/// <summary>
/// Loads and saves Paint.NET palette format files (hexadecimal aarrggbb format).
/// Uses streams instead of GIO for toolkit independence.
/// </summary>
public sealed class PaintDotNetPalette : IPaletteLoader, IPaletteSaver
{
	public List<ColorBgra> Load (Stream stream)
	{
		List<ColorBgra> colors = [];
		using var reader = new StreamReader (stream);

		string? line = reader.ReadLine ();
		do {
			if (line is null || line.StartsWith (';'))
				continue;

			colors.Add (ReadColor (line));

		} while ((line = reader.ReadLine ()) != null);

		return colors;
	}

	private static ColorBgra ReadColor (string line)
	{
		uint color = uint.Parse (line[..8], NumberStyles.HexNumber);
		byte b = (byte) (color & 0xff);
		byte g = (byte) ((color >> 8) & 0xff);
		byte r = (byte) ((color >> 16) & 0xff);
		byte a = (byte) (color >> 24);
		return ColorBgra.FromBgra (b, g, r, a);
	}

	public void Save (IReadOnlyList<ColorBgra> colors, Stream stream)
	{
		using StreamWriter writer = new (stream);

		writer.WriteLine ("; Hexadecimal format: aarrggbb");

		foreach (ColorBgra color in colors)
			writer.WriteLine (RepresentColor (color));
	}

	private static string RepresentColor (ColorBgra color)
		=> string.Format ("{0:X}", (color.A << 24) | (color.R << 16) | (color.G << 8) | color.B);
}
