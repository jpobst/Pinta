using NUnit.Framework;
using Pinta.Foundation;

namespace Pinta.Foundation.Tests;

[TestFixture]
public class BlendOpTests
{
	[Test]
	public void NormalBlend_OpaqueTop_ReturnsTop ()
	{
		var bottom = ColorBgra.FromBgra (100, 150, 200, 255);
		var top = ColorBgra.FromBgra (50, 75, 25, 255);
		var result = UserBlendOps.NormalBlendOp.ApplyStatic (bottom, top);
		Assert.Multiple (() => {
			Assert.That (result.B, Is.EqualTo (50));
			Assert.That (result.G, Is.EqualTo (75));
			Assert.That (result.R, Is.EqualTo (25));
			Assert.That (result.A, Is.EqualTo (255));
		});
	}

	[Test]
	public void NormalBlend_TransparentTop_ReturnsBottom ()
	{
		var bottom = ColorBgra.FromBgra (100, 150, 200, 255);
		var top = ColorBgra.FromBgra (50, 75, 25, 0);
		var result = UserBlendOps.NormalBlendOp.ApplyStatic (bottom, top);
		Assert.Multiple (() => {
			Assert.That (result.B, Is.EqualTo (100));
			Assert.That (result.G, Is.EqualTo (150));
			Assert.That (result.R, Is.EqualTo (200));
			Assert.That (result.A, Is.EqualTo (255));
		});
	}

	[Test]
	public void MultiplyBlend_ProducesCorrectResult ()
	{
		var bottom = ColorBgra.FromBgra (128, 128, 128, 255);
		var top = ColorBgra.FromBgra (128, 128, 128, 255);
		var result = UserBlendOps.MultiplyBlendOp.ApplyStatic (bottom, top);
		// With premultiplied multiply: (128*128)/255 ≈ 64
		Assert.Multiple (() => {
			Assert.That (result.B, Is.InRange (63, 65));
			Assert.That (result.G, Is.InRange (63, 65));
			Assert.That (result.R, Is.InRange (63, 65));
			Assert.That (result.A, Is.EqualTo (255));
		});
	}

	[Test]
	public void ScreenBlend_WhiteOnBlack_ReturnsWhite ()
	{
		var black = ColorBgra.FromBgra (0, 0, 0, 255);
		var white = ColorBgra.FromBgra (255, 255, 255, 255);
		var result = UserBlendOps.ScreenBlendOp.ApplyStatic (black, white);
		Assert.Multiple (() => {
			Assert.That (result.R, Is.EqualTo (255));
			Assert.That (result.G, Is.EqualTo (255));
			Assert.That (result.B, Is.EqualTo (255));
		});
	}

	[Test]
	public void DarkenBlend_ProducesDarkerColor ()
	{
		var color1 = ColorBgra.FromBgra (200, 100, 150, 255);
		var color2 = ColorBgra.FromBgra (100, 200, 150, 255);
		var result = UserBlendOps.DarkenBlendOp.ApplyStatic (color1, color2);
		// Darken selects min of each channel
		Assert.Multiple (() => {
			Assert.That (result.B, Is.EqualTo (100));
			Assert.That (result.G, Is.EqualTo (100));
			Assert.That (result.R, Is.EqualTo (150));
		});
	}

	[Test]
	public void LightenBlend_ProducesLighterColor ()
	{
		var color1 = ColorBgra.FromBgra (200, 100, 150, 255);
		var color2 = ColorBgra.FromBgra (100, 200, 150, 255);
		var result = UserBlendOps.LightenBlendOp.ApplyStatic (color1, color2);
		// Lighten selects max of each channel
		Assert.Multiple (() => {
			Assert.That (result.B, Is.EqualTo (200));
			Assert.That (result.G, Is.EqualTo (200));
			Assert.That (result.R, Is.EqualTo (150));
		});
	}

	[Test]
	public void DifferenceBlend_SameColor_ReturnsBlack ()
	{
		var color = ColorBgra.FromBgra (128, 128, 128, 255);
		var result = UserBlendOps.DifferenceBlendOp.ApplyStatic (color, color);
		Assert.Multiple (() => {
			Assert.That (result.B, Is.EqualTo (0));
			Assert.That (result.G, Is.EqualTo (0));
			Assert.That (result.R, Is.EqualTo (0));
		});
	}

	[Test]
	public void CreateBlendOp_ReturnsCorrectTypes ()
	{
		Assert.Multiple (() => {
			Assert.That (UserBlendOps.CreateBlendOp (BlendMode.Normal), Is.InstanceOf<UserBlendOps.NormalBlendOp> ());
			Assert.That (UserBlendOps.CreateBlendOp (BlendMode.Multiply), Is.InstanceOf<UserBlendOps.MultiplyBlendOp> ());
			Assert.That (UserBlendOps.CreateBlendOp (BlendMode.Screen), Is.InstanceOf<UserBlendOps.ScreenBlendOp> ());
			Assert.That (UserBlendOps.CreateBlendOp (BlendMode.Overlay), Is.InstanceOf<UserBlendOps.OverlayBlendOp> ());
		});
	}
}
