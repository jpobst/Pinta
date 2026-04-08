using System;
using NUnit.Framework;
using Pinta.Foundation;

namespace Pinta.Foundation.Tests;

[TestFixture]
public class GeometryTests
{
	[Test]
	public void PointI_Equality ()
	{
		var a = new PointI (10, 20);
		var b = new PointI (10, 20);
		Assert.That (a, Is.EqualTo (b));
	}

	[Test]
	public void PointI_Arithmetic ()
	{
		var a = new PointI (10, 20);
		var b = new PointI (3, 5);
		Assert.That (a + b, Is.EqualTo (new PointI (13, 25)));
		Assert.That (a - b, Is.EqualTo (new PointI (7, 15)));
	}

	[Test]
	public void PointD_Scaled ()
	{
		var p = new PointD (3.0, 4.0);
		var scaled = p.Scaled (2.0);
		Assert.Multiple (() => {
			Assert.That (scaled.X, Is.EqualTo (6.0));
			Assert.That (scaled.Y, Is.EqualTo (8.0));
		});
	}

	[Test]
	public void RectangleI_Properties ()
	{
		var rect = new RectangleI (10, 20, 100, 50);
		Assert.Multiple (() => {
			Assert.That (rect.X, Is.EqualTo (10));
			Assert.That (rect.Y, Is.EqualTo (20));
			Assert.That (rect.Width, Is.EqualTo (100));
			Assert.That (rect.Height, Is.EqualTo (50));
			Assert.That (rect.Right, Is.EqualTo (109));
			Assert.That (rect.Bottom, Is.EqualTo (69));
		});
	}

	[Test]
	public void Size_Properties ()
	{
		var size = new Size (100, 200);
		Assert.Multiple (() => {
			Assert.That (size.Width, Is.EqualTo (100));
			Assert.That (size.Height, Is.EqualTo (200));
		});
	}

	[Test]
	public void DegreesAngle_ToRadians ()
	{
		var angle = new DegreesAngle (180);
		var radians = angle.ToRadians ();
		Assert.That (radians.Radians, Is.EqualTo (Math.PI).Within (0.0001));
	}

	[Test]
	public void RadiansAngle_ToDegrees ()
	{
		var angle = new RadiansAngle (Math.PI);
		var degrees = angle.ToDegrees ();
		Assert.That (degrees.Degrees, Is.EqualTo (180).Within (0.0001));
	}
}
