using NUnit.Framework;
using Pinta.Foundation;

namespace Pinta.Foundation.Tests;

[TestFixture]
public class MathematicsTests
{
	[Test]
	public void Lerp_ReturnsCorrectValues ()
	{
		Assert.That (Mathematics.Lerp (0.0, 10.0, 0.5), Is.EqualTo (5.0));
		Assert.That (Mathematics.Lerp (0.0, 10.0, 0.0), Is.EqualTo (0.0));
		Assert.That (Mathematics.Lerp (0.0, 10.0, 1.0), Is.EqualTo (10.0));
	}
}

[TestFixture]
public class HsvColorTests
{
	[Test]
	public void FromBgra_Red ()
	{
		var red = ColorBgra.FromBgra (0, 0, 255, 255);
		var hsv = HsvColor.FromBgra (red);
		Assert.Multiple (() => {
			Assert.That (hsv.Hue, Is.EqualTo (0).Within (1));
			Assert.That (hsv.Saturation, Is.EqualTo (1.0).Within (0.01));
			Assert.That (hsv.Value, Is.EqualTo (1.0).Within (0.01));
		});
	}

	[Test]
	public void ToColorBgra_RoundTrip ()
	{
		var original = ColorBgra.FromBgra (100, 150, 200, 255);
		var hsv = HsvColor.FromBgra (original);
		var converted = hsv.ToColorBgra ();
		Assert.Multiple (() => {
			Assert.That (converted.R, Is.EqualTo (original.R).Within (2));
			Assert.That (converted.G, Is.EqualTo (original.G).Within (2));
			Assert.That (converted.B, Is.EqualTo (original.B).Within (2));
		});
	}
}
