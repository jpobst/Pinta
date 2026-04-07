namespace Pinta.Foundation;

partial struct ColorBgra
{
	/// <summary>
	/// Compares two ColorBgra instance to determine if they are equal.
	/// </summary>
	public static bool operator == (ColorBgra lhs, ColorBgra rhs)
		=> lhs.BGRA == rhs.BGRA;

	/// <summary>
	/// Compares two ColorBgra instance to determine if they are not equal.
	/// </summary>
	public static bool operator != (ColorBgra lhs, ColorBgra rhs)
		=> lhs.BGRA != rhs.BGRA;

	/// <summary>
	/// Compares two ColorBgra instance to determine if they are equal.
	/// </summary>
	public override readonly bool Equals (object? obj)
		=> obj is ColorBgra bgra && bgra.BGRA == BGRA;

	/// <summary>
	/// Returns a hash code for this color value.
	/// </summary>
	/// <returns></returns>
	public override readonly int GetHashCode () { unchecked { return (int) BGRA; } }

	public override readonly string ToString ()
		=> $"B: {B}, G: {G}, R: {R}, A: {A}";

	public static int ColorDifference (ColorBgra a, ColorBgra b)
	{
		int diffR = a.R - b.R;
		int diffG = a.G - b.G;
		int diffB = a.B - b.B;
		int diffA = a.A - b.A;

		int summandR = diffR * diffR;
		int summandG = diffG * diffG;
		int summandB = diffB * diffB;
		int summandA = diffA * diffA;

		int sum = summandR + summandG + summandB + summandA;
		return sum;
	}

	public static bool ColorsWithinTolerance (ColorBgra a, ColorBgra b, int tolerance)
		=> ColorDifference (a, b) <= tolerance * tolerance * 4;
}
