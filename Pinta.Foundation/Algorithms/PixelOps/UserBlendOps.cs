namespace Pinta.Foundation;

/// <summary>
/// This class contains all the render ops that can be used by the user
/// to configure a layer's blending mode.
/// </summary>
public sealed partial class UserBlendOps
{
	private UserBlendOps ()
	{
	}

	/// <summary>
	/// Creates a blend op for the given blend mode.
	/// </summary>
	public static UserBlendOp CreateBlendOp (BlendMode mode)
	{
		return mode switch {
			BlendMode.Normal => new NormalBlendOp (),
			BlendMode.Multiply => new MultiplyBlendOp (),
			BlendMode.ColorBurn => new ColorBurnBlendOp (),
			BlendMode.ColorDodge => new ColorDodgeBlendOp (),
			BlendMode.Overlay => new OverlayBlendOp (),
			BlendMode.Difference => new DifferenceBlendOp (),
			BlendMode.Lighten => new LightenBlendOp (),
			BlendMode.Darken => new DarkenBlendOp (),
			BlendMode.Screen => new ScreenBlendOp (),
			BlendMode.Xor => new XorBlendOp (),
			BlendMode.HardLight => new HardLightBlendOp (),
			BlendMode.SoftLight => new SoftLightBlendOp (),
			BlendMode.Color => new ColorBlendOp (),
			BlendMode.Luminosity => new LuminosityBlendOp (),
			BlendMode.Hue => new HueBlendOp (),
			BlendMode.Saturation => new SaturationBlendOp (),
			_ => throw new System.ArgumentOutOfRangeException (nameof (mode)),
		};
	}
}
