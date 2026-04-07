using NUnit.Framework;
using Pinta.Foundation;

namespace Pinta.Foundation.Tests;

[TestFixture]
public class ColorBgraTests
{
	[Test]
	public void FromBgra_CreatesCorrectColor ()
	{
		var color = ColorBgra.FromBgra (10, 20, 30, 255);
		Assert.Multiple (() => {
			Assert.That (color.B, Is.EqualTo (10));
			Assert.That (color.G, Is.EqualTo (20));
			Assert.That (color.R, Is.EqualTo (30));
			Assert.That (color.A, Is.EqualTo (255));
		});
	}

	[Test]
	public void FromBgr_SetsAlphaTo255 ()
	{
		var color = ColorBgra.FromBgr (50, 100, 150);
		Assert.That (color.A, Is.EqualTo (255));
	}

	[Test]
	public void FromUInt32_RoundTrips ()
	{
		uint value = 0xFF112233; // A=FF, R=11, G=22, B=33
		var color = ColorBgra.FromUInt32 (value);
		Assert.That (color.BGRA, Is.EqualTo (value));
	}

	[Test]
	public void NewAlpha_ChangesAlpha ()
	{
		var color = ColorBgra.FromBgra (10, 20, 30, 255);
		var newColor = color.NewAlpha (128);
		Assert.That (newColor.A, Is.EqualTo (128));
	}

	[Test]
	public void Indexer_AccessesChannels ()
	{
		var color = ColorBgra.FromBgra (10, 20, 30, 40);
		Assert.Multiple (() => {
			Assert.That (color[0], Is.EqualTo (10)); // B
			Assert.That (color[1], Is.EqualTo (20)); // G
			Assert.That (color[2], Is.EqualTo (30)); // R
			Assert.That (color[3], Is.EqualTo (40)); // A
		});
	}

	[Test]
	public void Predefined_Colors_AreCorrect ()
	{
		Assert.Multiple (() => {
			Assert.That (ColorBgra.Black.R, Is.EqualTo (0));
			Assert.That (ColorBgra.Black.G, Is.EqualTo (0));
			Assert.That (ColorBgra.Black.B, Is.EqualTo (0));
			Assert.That (ColorBgra.Black.A, Is.EqualTo (255));

			Assert.That (ColorBgra.White.R, Is.EqualTo (255));
			Assert.That (ColorBgra.White.G, Is.EqualTo (255));
			Assert.That (ColorBgra.White.B, Is.EqualTo (255));
			Assert.That (ColorBgra.White.A, Is.EqualTo (255));

			Assert.That (ColorBgra.Transparent.A, Is.EqualTo (0));
		});
	}

	[Test]
	public void GetIntensityByte_ReturnsWeightedAverage ()
	{
		var white = ColorBgra.White;
		Assert.That (white.GetIntensityByte (), Is.EqualTo (255));

		var black = ColorBgra.Black;
		Assert.That (black.GetIntensityByte (), Is.EqualTo (0));
	}
}
