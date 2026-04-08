using NUnit.Framework;
using Pinta.Foundation;

namespace Pinta.Foundation.Tests;

[TestFixture]
public class PixelBufferTests
{
	[Test]
	public void Create_InitializesToTransparent ()
	{
		using var buffer = new PixelBuffer (10, 10);
		var data = buffer.GetReadOnlyPixelData ();
		for (int i = 0; i < data.Length; i++)
			Assert.That (data[i].BGRA, Is.EqualTo (0u));
	}

	[Test]
	public void Width_Height_AreCorrect ()
	{
		using var buffer = new PixelBuffer (100, 200);
		Assert.Multiple (() => {
			Assert.That (buffer.Width, Is.EqualTo (100));
			Assert.That (buffer.Height, Is.EqualTo (200));
		});
	}

	[Test]
	public void GetPixelData_CanWriteAndRead ()
	{
		using var buffer = new PixelBuffer (10, 10);
		var data = buffer.GetPixelData ();
		data[0] = ColorBgra.FromBgra (10, 20, 30, 255);

		var readData = buffer.GetReadOnlyPixelData ();
		Assert.That (readData[0].B, Is.EqualTo (10));
		Assert.That (readData[0].G, Is.EqualTo (20));
		Assert.That (readData[0].R, Is.EqualTo (30));
		Assert.That (readData[0].A, Is.EqualTo (255));
	}

	[Test]
	public void GetBounds_ReturnsCorrectRect ()
	{
		using var buffer = new PixelBuffer (50, 30);
		var bounds = buffer.GetBounds ();
		Assert.Multiple (() => {
			Assert.That (bounds.X, Is.EqualTo (0));
			Assert.That (bounds.Y, Is.EqualTo (0));
			Assert.That (bounds.Width, Is.EqualTo (50));
			Assert.That (bounds.Height, Is.EqualTo (30));
		});
	}

	[Test]
	public void GetSize_ReturnsCorrect ()
	{
		using var buffer = new PixelBuffer (50, 30);
		var size = buffer.GetSize ();
		Assert.Multiple (() => {
			Assert.That (size.Width, Is.EqualTo (50));
			Assert.That (size.Height, Is.EqualTo (30));
		});
	}
}
