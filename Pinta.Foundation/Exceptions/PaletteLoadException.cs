using System;

namespace Pinta.Foundation;

public sealed class PaletteLoadException : Exception
{
	public string FileName { get; }
	public PaletteLoadException (string fileName, string message)
		: base (message)
	{
		FileName = fileName;
	}
}
