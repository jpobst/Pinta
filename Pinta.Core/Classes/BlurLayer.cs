using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cairo;

namespace Pinta.Core.Classes
{
	public class BlurLayer : Layer
	{
		private BaseEffect blur;
		private ImageSurface tmp_surface;

		public BlurLayer ()
		{
			blur = PintaCore.Effects.GetEffect ("GaussianBlurEffect");
		}

		public override void EnsureUpdated (Cairo.ImageSurface src, Gdk.Rectangle bounds)
		{
			// Create a temp surface to render to that
			// is the same size as the destination
			if (tmp_surface == null || tmp_surface.Width != Surface.Width || tmp_surface.Height != Surface.Height) {
				if (tmp_surface != null)
					(tmp_surface as IDisposable).Dispose ();

				tmp_surface = new ImageSurface (Format.Argb32, Surface.Width, Surface.Height);
			}

			// Copy the source to the correct place in the temp surface
			using (var g = new Context (tmp_surface)) {
				g.Translate (bounds.X, bounds.Y);
				g.SetSource (src);
				g.Paint ();
			}

			// Render from the temp layer to our Surface
			blur.Render (tmp_surface, Surface, new Gdk.Rectangle[] { bounds });

			
			//} else {
			//        blur.Render (src, Surface, new Gdk.Rectangle[] { bounds });
			//}

			//base.Render (src, bounds);
		}
	}
}
