using System.Collections.Generic;
using System.IO;

namespace Pinta.Foundation;

/// <summary>
/// Interface for palette savers that operate on streams.
/// This is the Foundation-layer replacement for the Cairo/GTK-based IPaletteSaver.
/// </summary>
public interface IPaletteSaver
{
	void Save (IReadOnlyList<ColorBgra> colors, Stream stream);
}
