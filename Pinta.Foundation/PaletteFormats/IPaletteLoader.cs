using System.Collections.Generic;
using System.IO;

namespace Pinta.Foundation;

/// <summary>
/// Interface for palette loaders that operate on streams.
/// This is the Foundation-layer replacement for the Cairo/GTK-based IPaletteLoader.
/// </summary>
public interface IPaletteLoader
{
	List<ColorBgra> Load (Stream stream);
}
