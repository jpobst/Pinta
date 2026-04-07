using System;

/// Replacements for Cairo / GDK points that GtkSharp provided in the GTK3 build.
namespace Pinta.Foundation;

public readonly record struct PointF (float X, float Y)
{
	public static PointF Zero { get; } = new (0, 0);
	public override readonly string ToString () => $"{X}, {Y}";
	public readonly PointD ToDouble () => new (X, Y);
	public readonly PointI ToInt () => new ((int) X, (int) Y);

	/// <summary>
	/// Returns a new point, rounded to the nearest integer coordinates.
	/// </summary>
	public readonly PointF Rounded () => new (MathF.Round (X), MathF.Round (Y));

	public readonly PointF Scaled (float factor) => new (X * factor, Y * factor);
	public static explicit operator PointF (PointI p) => new (p.X, p.Y);
	public static PointF operator + (PointF left, PointF right)
		=> new (
			X: left.X + right.X,
			Y: left.Y + right.Y
		);

	public static PointF operator - (PointF left, PointF right)
		=> new (
			X: left.X - right.X,
			Y: left.Y - right.Y
		);
}

public readonly record struct PointI (int X, int Y)
{
	public static PointI Zero { get; } = new (0, 0);
	public override readonly string ToString () => $"{X}, {Y}";

	public readonly PointD ToDouble () => new (X, Y);
	public readonly PointF ToFloat () => new (X, Y);

	public PointI Rotated90CCW () // Counterclockwise
		=> new (-Y, X);

	public static PointI operator + (PointI left, PointI right)
		=> new (
			X: left.X + right.X,
			Y: left.Y + right.Y
		);

	public static PointI operator - (PointI left, PointI right)
		=> new (
			X: left.X - right.X,
			Y: left.Y - right.Y
		);
}

public readonly record struct PointD (double X, double Y)
{
	public static PointD Zero { get; } = new (0, 0);

	public override readonly string ToString () => $"{X}, {Y}";

	public readonly PointI ToInt () => new ((int) X, (int) Y);
	public readonly PointF ToFloat () => new ((float) X, (float) Y);

	/// <summary>
	/// Returns a new point, rounded to the nearest integer coordinates.
	/// </summary>
	public readonly PointD Rounded () => new (Math.Round (X), Math.Round (Y));

	public readonly PointD Scaled (double factor) => new (X * factor, Y * factor);

	public static explicit operator PointD (PointI p) => new (p.X, p.Y);

	public static PointD operator + (PointD left, PointD right)
		=> new (
			X: left.X + right.X,
			Y: left.Y + right.Y
		);

	public static PointD operator - (PointD left, PointD right)
		=> new (
			X: left.X - right.X,
			Y: left.Y - right.Y
		);
}

public readonly record struct Size (int Width, int Height)
{
	public static Size Empty { get; } = new (0, 0);

	public override readonly string ToString () => $"{Width}, {Height}";

	public readonly bool IsEmpty => (Width == 0 && Height == 0);
}
