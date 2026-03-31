/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
//                                                                             //
// Ported to Pinta by: Jonathan Pobst <monkey@jpobst.com>                      //
/////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Immutable;
using System.Runtime.Intrinsics;
using System.Threading.Tasks;
using Cairo;
using Pinta.Core;

namespace Pinta.Effects;

public sealed class GaussianBlurEffect : BaseEffect
{
	public override string Icon => Resources.Icons.EffectsBlursGaussianBlur;

	public sealed override bool IsTileable => true;

	public override string Name => Translations.GetString ("Gaussian Blur");

	public override bool IsConfigurable => true;

	public override string EffectMenuCategory => Translations.GetString ("Blurs");

	public GaussianBlurData Data => (GaussianBlurData) EffectData!;  // NRT - Set in constructor

	private readonly IChromeService chrome;
	private readonly IWorkspaceService workspace;
	public GaussianBlurEffect (IServiceProvider services)
	{
		chrome = services.GetService<IChromeService> ();
		workspace = services.GetService<IWorkspaceService> ();
		EffectData = new GaussianBlurData ();
	}

	public override Task<bool> LaunchConfiguration ()
		=> chrome.LaunchSimpleEffectDialog (this, workspace);

	#region Algorithm Code Ported From PDN

	public static ImmutableArray<int> CreateGaussianBlurRow (int amount)
	{
		int size = 1 + (amount * 2);
		var weights = ImmutableArray.CreateBuilder<int> (size);
		weights.Count = size;

		for (int i = 0; i <= amount; ++i) {
			// 1 + aa - aa + 2ai - ii
			weights[i] = 16 * (i + 1);
			weights[size - i - 1] = weights[i];
		}

		return weights.MoveToImmutable ();
	}

	public override void Render (ImageSurface src, ImageSurface dest, ReadOnlySpan<RectangleI> rois)
	{
		if (Data.Radius == 0)
			return; // Copy src to dest

		int r = Data.Radius;
		ImmutableArray<int> w = CreateGaussianBlurRow (r);
		int wlen = w.Length;

		// Use Vector256<long> to process all 4 color channels (B, G, R, A) in parallel
		Span<long> waSums = stackalloc long[wlen];
		Span<long> wcSums = stackalloc long[wlen];
		Span<Vector256<long>> channelSums = stackalloc Vector256<long>[wlen];

		// Cache these for a massive performance boost
		int src_width = src.Width;
		int src_height = src.Height;
		ReadOnlySpan<ColorBgra> src_data = src.GetReadOnlyPixelData ();
		Span<ColorBgra> dst_data = dest.GetPixelData ();

		foreach (var rect in rois) {

			if (rect.Height < 1 || rect.Width < 1)
				continue;

			for (int y = rect.Top; y <= rect.Bottom; ++y) {
				long waSum = 0;
				long wcSum = 0;
				Vector256<long> channelSum = Vector256<long>.Zero;

				var dst_row = dst_data.Slice (y * src_width, src_width);

				for (int wx = 0; wx < wlen; ++wx) {
					int srcX = rect.Left + wx - r;
					waSums[wx] = 0;
					wcSums[wx] = 0;
					channelSums[wx] = Vector256<long>.Zero;

					if (srcX < 0 || srcX >= src_width)
						continue;

					for (int wy = 0; wy < wlen; ++wy) {
						int srcY = y + wy - r;

						if (srcY < 0 || srcY >= src_height)
							continue;

						PointI pixelPosition = new (srcX, srcY);

						ColorBgra c = src.GetColorBgra (src_data, src_width, pixelPosition).ToStraightAlpha ();
						int wp = w[wy];

						waSums[wx] += wp;
						wp *= c.A + (c.A >> 7);
						wcSums[wx] += wp;
						wp >>= 8;

						Vector256<long> pixel = Vector256.Create ((long) c.B, (long) c.G, (long) c.R, (long) c.A);
						channelSums[wx] += Vector256.Create ((long) wp) * pixel;
					}

					long wwx = w[wx];
					waSum += wwx * waSums[wx];
					wcSum += wwx * wcSums[wx];
					channelSum += Vector256.Create (wwx) * channelSums[wx];
				}

				wcSum >>= 8;

				dst_row[rect.Left] = ComputeBlurredPixel (channelSum, waSum, wcSum);

				for (int x = rect.Left + 1; x <= rect.Right; ++x) {
					for (int i = 0; i < wlen - 1; ++i) {
						waSums[i] = waSums[i + 1];
						wcSums[i] = wcSums[i + 1];
						channelSums[i] = channelSums[i + 1];
					}

					waSum = 0;
					wcSum = 0;
					channelSum = Vector256<long>.Zero;

					int wx;
					for (wx = 0; wx < wlen - 1; ++wx) {
						long wwx = w[wx];
						waSum += wwx * waSums[wx];
						wcSum += wwx * wcSums[wx];
						channelSum += Vector256.Create (wwx) * channelSums[wx];
					}

					wx = wlen - 1;

					waSums[wx] = 0;
					wcSums[wx] = 0;
					channelSums[wx] = Vector256<long>.Zero;

					int srcX = x + wx - r;

					if (srcX >= 0 && srcX < src_width) {
						for (int wy = 0; wy < wlen; ++wy) {
							int srcY = y + wy - r;

							if (srcY < 0 || srcY >= src_height)
								continue;

							ColorBgra c = src.GetColorBgra (src_data, src_width, new (srcX, srcY)).ToStraightAlpha ();
							int wp = w[wy];

							waSums[wx] += wp;
							wp *= c.A + (c.A >> 7);
							wcSums[wx] += wp;
							wp >>= 8;

							Vector256<long> pixel = Vector256.Create ((long) c.B, (long) c.G, (long) c.R, (long) c.A);
							channelSums[wx] += Vector256.Create ((long) wp) * pixel;
						}

						long wr = w[wx];
						waSum += wr * waSums[wx];
						wcSum += wr * wcSums[wx];
						channelSum += Vector256.Create (wr) * channelSums[wx];
					}

					wcSum >>= 8;

					dst_row[x] = ComputeBlurredPixel (channelSum, waSum, wcSum);
				}
			}
		}
	}

	/// <summary>
	/// Converts the accumulated SIMD channel sums into a final blurred pixel color.
	/// The channelSum vector holds {B, G, R, A} accumulated weighted sums.
	/// </summary>
	private static ColorBgra ComputeBlurredPixel (Vector256<long> channelSum, long waSum, long wcSum)
	{
		if (waSum == 0 || wcSum == 0)
			return ColorBgra.Zero;

		byte blue = (byte) (channelSum.GetElement (0) / wcSum);
		byte green = (byte) (channelSum.GetElement (1) / wcSum);
		byte red = (byte) (channelSum.GetElement (2) / wcSum);
		byte alpha = (byte) (channelSum.GetElement (3) / waSum);

		return ColorBgra.FromBgra (blue, green, red, alpha).ToPremultipliedAlpha ();
	}
	#endregion

	public sealed class GaussianBlurData : EffectData
	{
		[Caption ("Radius")]
		[MinimumValue (0), MaximumValue (200)]
		public int Radius { get; set; } = 2;

		[Skip]
		public override bool IsDefault => Radius == 0;
	}
}
