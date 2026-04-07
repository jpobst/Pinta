using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Pinta.Foundation;

/// <summary>
/// A managed pixel buffer that stores BGRA pixel data in premultiplied alpha format.
/// This is the Foundation-layer replacement for Cairo.ImageSurface for pixel storage.
/// It provides Span-based access for high-performance pixel manipulation.
/// </summary>
public sealed class PixelBuffer : IDisposable
{
	private ColorBgra[] data;
	private bool disposed;

	/// <summary>
	/// Creates a new pixel buffer with the specified dimensions, initialized to transparent black.
	/// </summary>
	public PixelBuffer (int width, int height)
	{
		ArgumentOutOfRangeException.ThrowIfNegative (width);
		ArgumentOutOfRangeException.ThrowIfNegative (height);

		Width = width;
		Height = height;
		data = GC.AllocateUninitializedArray<ColorBgra> (width * height);
		data.AsSpan ().Clear (); // Initialize to transparent black (0,0,0,0)
	}

	/// <summary>
	/// Creates a new pixel buffer with the specified dimensions from existing data.
	/// The data is copied.
	/// </summary>
	public PixelBuffer (int width, int height, ReadOnlySpan<ColorBgra> sourceData)
	{
		ArgumentOutOfRangeException.ThrowIfNegative (width);
		ArgumentOutOfRangeException.ThrowIfNegative (height);

		if (sourceData.Length != width * height)
			throw new ArgumentException ($"Source data length ({sourceData.Length}) does not match dimensions ({width}x{height} = {width * height})");

		Width = width;
		Height = height;
		data = GC.AllocateUninitializedArray<ColorBgra> (width * height);
		sourceData.CopyTo (data);
	}

	/// <summary>Width of the pixel buffer in pixels.</summary>
	public int Width { get; }

	/// <summary>Height of the pixel buffer in pixels.</summary>
	public int Height { get; }

	/// <summary>Gets the size of the pixel buffer.</summary>
	public Size GetSize ()
		=> new (Width, Height);

	/// <summary>Gets the bounding rectangle of the pixel buffer.</summary>
	public RectangleI GetBounds ()
		=> new (0, 0, Width, Height);

	/// <summary>
	/// Access the pixel buffer data as a writable span of ColorBgra pixels.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public Span<ColorBgra> GetPixelData ()
	{
		ObjectDisposedException.ThrowIf (disposed, this);
		return data;
	}

	/// <summary>
	/// Access the pixel buffer data as a read-only span of ColorBgra pixels.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public ReadOnlySpan<ColorBgra> GetReadOnlyPixelData ()
	{
		ObjectDisposedException.ThrowIf (disposed, this);
		return data;
	}

	/// <summary>
	/// Gets the raw byte data of the pixel buffer.
	/// </summary>
	public Span<byte> GetByteData ()
	{
		ObjectDisposedException.ThrowIf (disposed, this);
		return MemoryMarshal.AsBytes (data.AsSpan ());
	}

	/// <summary>
	/// Gets the raw byte data of the pixel buffer as read-only.
	/// </summary>
	public ReadOnlySpan<byte> GetReadOnlyByteData ()
	{
		ObjectDisposedException.ThrowIf (disposed, this);
		return MemoryMarshal.AsBytes<ColorBgra> (data);
	}

	/// <summary>
	/// Gets a reference to the pixel at the specified position.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public ref readonly ColorBgra GetColorBgra (PointI position)
	{
		return ref data[Width * position.Y + position.X];
	}

	/// <summary>
	/// Gets a reference to the pixel at the specified position using cached data span.
	/// For better performance in tight loops, get the data span once and reuse it.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static ref readonly ColorBgra GetColorBgra (
		ReadOnlySpan<ColorBgra> data,
		int width,
		PointI position)
	{
		return ref data[width * position.Y + position.X];
	}

	/// <summary>
	/// Creates a deep copy of this pixel buffer.
	/// </summary>
	public PixelBuffer Clone ()
	{
		ObjectDisposedException.ThrowIf (disposed, this);
		return new PixelBuffer (Width, Height, data);
	}

	/// <summary>
	/// Copies the pixel data from another pixel buffer into this one.
	/// Both buffers must have the same dimensions.
	/// </summary>
	public void CopyFrom (PixelBuffer source)
	{
		ObjectDisposedException.ThrowIf (disposed, this);

		if (Width != source.Width || Height != source.Height)
			throw new ArgumentException ("Source and destination pixel buffers must have the same dimensions.");

		source.GetReadOnlyPixelData ().CopyTo (data);
	}

	/// <summary>
	/// Clears the buffer to transparent black.
	/// </summary>
	public void Clear ()
	{
		ObjectDisposedException.ThrowIf (disposed, this);
		data.AsSpan ().Clear ();
	}

	/// <summary>
	/// Fills the buffer with the specified color.
	/// </summary>
	public void Fill (ColorBgra color)
	{
		ObjectDisposedException.ThrowIf (disposed, this);
		data.AsSpan ().Fill (color);
	}

	/// <summary>
	/// Gets the row of pixel data at the specified y coordinate.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public Span<ColorBgra> GetRow (int y)
	{
		return data.AsSpan (y * Width, Width);
	}

	/// <summary>
	/// Gets the row of pixel data at the specified y coordinate as read-only.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public ReadOnlySpan<ColorBgra> GetReadOnlyRow (int y)
	{
		return data.AsSpan (y * Width, Width);
	}

	/// <summary>
	/// Enumerates pixel offsets for a given region of interest.
	/// </summary>
	public PixelOffsetEnumerable EnumeratePixelOffsets (RectangleI roi)
		=> new (roi, GetSize ());

	public void Dispose ()
	{
		if (!disposed) {
			disposed = true;
			data = [];
		}
	}
}
