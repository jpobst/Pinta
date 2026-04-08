using System;

namespace Pinta.Foundation;

public readonly struct TextPosition (int line, int offset) : IComparable<TextPosition>
{
	public int Line { get; } = line;
	public int Offset { get; } = offset;

	public TextPosition WithLine (int line)
		=> new (line, Offset);

	public TextPosition WithOffset (int offset)
		=> new (Line, offset);

	public override readonly bool Equals (object? obj)
		=> obj is TextPosition position && this == position;

	public override readonly int GetHashCode ()
		=> HashCode.Combine (Line, Offset);

	public override readonly string ToString ()
		=> $"({Line}, {Offset})";

	public static bool operator == (TextPosition x, TextPosition y)
		=> x.CompareTo (y) == 0;

	public static bool operator != (TextPosition x, TextPosition y)
		=> x.CompareTo (y) != 0;

	public readonly int CompareTo (TextPosition other)
	{
		if (Line.CompareTo (other.Line) != 0)
			return Line.CompareTo (other.Line);
		else
			return Offset.CompareTo (other.Offset);
	}

	public static TextPosition Max (TextPosition p1, TextPosition p2)
		=> (p1.CompareTo (p2) > 0) ? p1 : p2;

	public static TextPosition Min (TextPosition p1, TextPosition p2)
		=> (p1.CompareTo (p2) < 0) ? p1 : p2;
}
