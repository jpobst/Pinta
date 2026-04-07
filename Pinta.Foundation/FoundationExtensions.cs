using System;
using System.Collections.Generic;

namespace Pinta.Foundation;

/// <summary>
/// Extension methods for Foundation types that are not GUI-related.
/// </summary>
public static class FoundationExtensions
{
	public static IEnumerable<RectangleI> ToRows (this RectangleI original)
	{
		if (original.Height < 0) throw new ArgumentException ("Height cannot be negative", nameof (original));
		if (original.Height == 0) yield break;
		for (int i = 0; i < original.Height; i++)
			yield return new (
				original.X,
				original.Y + i,
				original.Width,
				1);
	}

	public static ColorBgra RandomColorBgra (this Random random, bool includeAlpha = false)
	{
		Span<byte> colorBytes = stackalloc byte[4];
		random.NextBytes (colorBytes);
		ColorBgra baseColor = ColorBgra.FromBgr (colorBytes[0], colorBytes[1], colorBytes[2]);
		return
			includeAlpha
			? baseColor.NewAlpha (colorBytes[3])
			: baseColor;
	}
}
