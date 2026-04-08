using System.IO;
using NUnit.Framework;
using Pinta.Foundation;

namespace Pinta.Foundation.Tests;

[TestFixture]
public class PaletteFormatTests
{
	[Test]
	public void GimpPalette_SaveLoad_RoundTrips ()
	{
		var colors = new System.Collections.Generic.List<ColorBgra> {
			ColorBgra.FromBgra (0, 0, 255, 255),   // Red
			ColorBgra.FromBgra (0, 255, 0, 255),   // Green
			ColorBgra.FromBgra (255, 0, 0, 255),   // Blue
		};

		var palette = new GimpPalette ();

		using var ms = new MemoryStream ();
		palette.Save (colors, ms);

		ms.Position = 0;
		var loaded = palette.Load (ms);

		Assert.That (loaded, Has.Count.EqualTo (3));
		Assert.Multiple (() => {
			Assert.That (loaded[0].R, Is.EqualTo (255));
			Assert.That (loaded[1].G, Is.EqualTo (255));
			Assert.That (loaded[2].B, Is.EqualTo (255));
		});
	}

	[Test]
	public void PaintDotNetPalette_SaveLoad_RoundTrips ()
	{
		var colors = new System.Collections.Generic.List<ColorBgra> {
			ColorBgra.FromBgra (0, 0, 255, 255),   // Red
			ColorBgra.FromBgra (0, 255, 0, 255),   // Green
		};

		var palette = new PaintDotNetPalette ();

		using var ms = new MemoryStream ();
		palette.Save (colors, ms);

		ms.Position = 0;
		var loaded = palette.Load (ms);

		Assert.That (loaded, Has.Count.EqualTo (2));
		Assert.Multiple (() => {
			Assert.That (loaded[0].R, Is.EqualTo (255));
			Assert.That (loaded[0].G, Is.EqualTo (0));
			Assert.That (loaded[0].B, Is.EqualTo (0));
			Assert.That (loaded[0].A, Is.EqualTo (255));
			Assert.That (loaded[1].G, Is.EqualTo (255));
		});
	}
}
