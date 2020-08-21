//
// AutoHideBox.cs
//
// Author:
//   Lluis Sanchez Gual
//

//
// Copyright (C) 2007 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

//#define ANIMATE_DOCKING

using Gtk;
using Gdk;
using Pinta.Core;
using Pinta.Docking.AtkCocoaHelper;

namespace Pinta.Docking
{
	class AutoHideBox: DockFrameTopLevel
	{
		const bool ANIMATE = false;
		
		static Gdk.Cursor resizeCursorW = new Gdk.Cursor (Gdk.CursorType.SbHDoubleArrow);
		static Gdk.Cursor resizeCursorH = new Gdk.Cursor (Gdk.CursorType.SbVDoubleArrow);
		
		bool resizing;
		int resizePos;
		int origSize;
		int origPos;
		bool horiz;
		bool startPos;
		DockFrame frame;
		bool animating;
		int targetSize;
		Gtk.PositionType position;
		bool disposed;
		bool insideGrip;
		
		int gripSize = 8;
		
		public AutoHideBox (DockFrame frame, DockItem item, Gtk.PositionType pos, int size): base (frame)
		{
			this.position = pos;
			this.frame = frame;
			this.targetSize = size;
			horiz = pos == PositionType.Left || pos == PositionType.Right;
			startPos = pos == PositionType.Top || pos == PositionType.Left;
			Events = Events | Gdk.EventMask.EnterNotifyMask | Gdk.EventMask.LeaveNotifyMask;
			
			Box fr;
			CustomFrame cframe = new CustomFrame ();
			cframe.Accessible.SetShouldIgnore (true);

			switch (pos) {
			case PositionType.Left: cframe.SetMargins (0, 0, 1, 1); break;
			case PositionType.Right: cframe.SetMargins (0, 0, 1, 1); break;
			case PositionType.Top: cframe.SetMargins (1, 1, 0, 0); break;
			case PositionType.Bottom: cframe.SetMargins (1, 1, 0, 0); break;
			}

			if (frame.UseWindowsForTopLevelFrames) {
				// When using a top level window on mac, clicks on the first 4 pixels next to the border
				// are not detected. To avoid confusing the user (since the resize cursor is shown), 
				// we make the resize drag area smaller.
				switch (pos) {
				case PositionType.Left: cframe.SetPadding (0, 0, 0, 4); gripSize = 4; break;
				case PositionType.Right: cframe.SetPadding (0, 0, 4, 0); gripSize = 4; break;
				}
			}

			EventBox sepBox = new EventBox ();

			// FIXME How to actually resize this?
			sepBox.Accessible.SetRole (AtkCocoa.Roles.AXSplitter, Translations.GetString ("Pad resize handle"));
			sepBox.Accessible.SetLabel (Translations.GetString ("Pad resize handle"));

			cframe.Add (sepBox);
			
			if (horiz) {
				fr = new HBox ();
				sepBox.Realized += delegate { sepBox.Window.Cursor = resizeCursorW; };
				sepBox.WidthRequest = gripSize;
			} else {
				fr = new VBox ();
				sepBox.Realized += delegate { sepBox.Window.Cursor = resizeCursorH; };
				sepBox.HeightRequest = gripSize;
			}
			fr.Accessible.SetShouldIgnore (true);

			sepBox.Events = EventMask.AllEventsMask;
			
			if (pos == PositionType.Left || pos == PositionType.Top)
				fr.PackEnd (cframe, false, false, 0);
			else
				fr.PackStart (cframe, false, false, 0);

			Add (fr);
			ShowAll ();
			Hide ();
			
#if ANIMATE_DOCKING
			scrollable = new ScrollableContainer ();
			scrollable.ScrollMode = false;
			scrollable.Show ();
#endif
			VBox itemBox = new VBox ();
			itemBox.Accessible.SetShouldIgnore (true);

			itemBox.Show ();
			item.TitleTab.Active = true;
			itemBox.PackStart (item.TitleTab, false, false, 0);

			item.Widget.Accessible.SetShouldIgnore (true);
			itemBox.PackStart (item.Widget, true, true, 0);

			item.Widget.Show ();
#if ANIMATE_DOCKING
			scrollable.Add (itemBox);
			fr.PackStart (scrollable, true, true, 0);
#else
			fr.PackStart (itemBox, true, true, 0);
#endif
			
			sepBox.ButtonPressEvent += OnSizeButtonPress;
			sepBox.ButtonReleaseEvent += OnSizeButtonRelease;
			sepBox.MotionNotifyEvent += OnSizeMotion;
			sepBox.Drawn += OnGripExpose;
			sepBox.EnterNotifyEvent += delegate { insideGrip = true; sepBox.QueueDraw (); };
			sepBox.LeaveNotifyEvent += delegate { insideGrip = false; sepBox.QueueDraw (); };
		}

		public bool Disposed {
			get { return disposed; }
			set { disposed = value; }
		}

		public void AnimateShow ()
		{
#if ANIMATE_DOCKING
			animating = true;
			scrollable.ScrollMode = true;
			scrollable.SetSize (position, targetSize);
			
			switch (position) {
			case PositionType.Left:
				Width = 0;
				break;
			case PositionType.Right:
				targetPos = X = X + Width;
				Width = 0;
				break;
			case PositionType.Top:
				Height = 0;
				break;
			case PositionType.Bottom:
				targetPos = Y = Y + Height;
				Height = 0;
				break;
			}
			Show ();
			GLib.Timeout.Add (10, RunAnimateShow);
#else
			Show ();
#endif
		}
		
		public void AnimateHide ()
		{
#if ANIMATE_DOCKING
			animating = true;
			scrollable.ScrollMode = true;
			scrollable.SetSize (position, targetSize);
			GLib.Timeout.Add (10, RunAnimateHide);
#else
			Hide ();
#endif
		}
		
		bool RunAnimateShow ()
		{
			if (!animating)
				return false;

			switch (position) {
			case PositionType.Left:
				Width += 1 + (targetSize - Width) / 3;
				if (Width < targetSize)
					return true;
				break;
			case PositionType.Right:
				Width += 1 + (targetSize - Width) / 3;
				X = - Width;
				if (Width < targetSize)
					return true;
				break;
			case PositionType.Top:
				Height += 1 + (targetSize - Height) / 3;
				if (Height < targetSize)
					return true;
				break;
			case PositionType.Bottom:
				Height += 1 + (targetSize - Height) / 3;
				Y = - Height;
				if (Height < targetSize)
					return true;
				break;
			}

			if (horiz)
				Width = targetSize;
			else
				Height = targetSize;
			animating = false;
			return false;
		}
		
		bool RunAnimateHide ()
		{
			if (!animating)
				return false;

			switch (position) {
			case PositionType.Left: {
				int ns = Width - 1 - Width / 3;
				if (ns > 0) {
					Width = ns;
					return true;
				}
				break;
			}
			case PositionType.Right: {
				int ns = Width - 1 - Width / 3;
				if (ns > 0) {
					Width = ns;
					X = - ns;
					return true;
				}
				break;
			}
			case PositionType.Top: {
				int ns = Height - 1 - Height / 3;
				if (ns > 0) {
					Height = ns;
					return true;
				}
				break;
			}
			case PositionType.Bottom: {
				int ns = Height - 1 - Height / 3;
				if (ns > 0) {
					Height = ns;
					Y = - ns;
					return true;
				}
				break;
			}
			}

			Hide ();
			animating = false;
			return false;
		}
		
		protected override void OnHidden ()
		{
			base.OnHidden ();
			animating = false;
		}

		protected override bool OnButtonPressEvent (EventButton evnt)
		{
			// Don't propagate the button press event to the parent frame,
			// since it has a handler that hides all visible autohide pads
			return true;
		}
		
		public int PadSize {
			get {
				return horiz ? Width : Height;
			}
		}

		[GLib.ConnectBefore]
		void OnSizeButtonPress (object ob, Gtk.ButtonPressEventArgs args)
		{
			if (!animating && args.Event.Button == 1 && !args.Event.TriggersContextMenu ()) {
				int n;
				if (horiz) {
					frame.Toplevel.GetPointer (out resizePos, out n);
					origSize = Width;
					if (!startPos) {
						origPos = X + origSize;
					}
				} else {
					frame.Toplevel.GetPointer (out n, out resizePos);
					origSize = Height;
					if (!startPos) {
						origPos = Y + origSize;
					}
				}
				resizing = true;
			}
		}
		
		void OnSizeButtonRelease (object ob, Gtk.ButtonReleaseEventArgs args)
		{
			resizing = false;
		}
		
		void OnSizeMotion (object ob, Gtk.MotionNotifyEventArgs args)
		{
			if (resizing) {
				int newPos, n;
				if (horiz) {
					frame.Toplevel.GetPointer (out newPos, out n);
					int diff = startPos ? (newPos - resizePos) : (resizePos - newPos);
					int newSize = origSize + diff;
					if (newSize < Child.SizeRequest ().Width)
						newSize = Child.SizeRequest ().Width;
					if (!startPos) {
						X = origPos - newSize;
					}
					Width = newSize;
				} else {
					frame.Toplevel.GetPointer (out n, out newPos);
					int diff = startPos ? (newPos - resizePos) : (resizePos - newPos);
					int newSize = origSize + diff;
					if (newSize < Child.SizeRequest ().Height)
						newSize = Child.SizeRequest ().Height;
					if (!startPos) {
						Y = origPos - newSize;
					}
					Height = newSize;
				}
				frame.QueueResize ();
			}
		}

		void OnGripExpose (object sender, Gtk.DrawnArgs args)
		{
			var w = (EventBox) sender;
			StateFlags s = insideGrip ? StateFlags.Prelight : StateFlags.Normal;

			var ctx = args.Cr;
			ctx.SetSourceColor (w.StyleContext.GetBackgroundColor(s).ToCairoColor ());
			ctx.Paint ();
		}
	}
	
	class ScrollableContainer: EventBox
	{
		PositionType expandPos;
		bool scrollMode;
		int targetSize;
		
		public bool ScrollMode {
			get {
				return scrollMode;
			}
			set {
				scrollMode = value;
				QueueResize ();
			}
		}
		
		public void SetSize (PositionType expandPosition, int targetSize)
		{
			this.expandPos = expandPosition;
			this.targetSize = targetSize;
			QueueResize ();
		}

        protected override void OnGetPreferredHeight(out int minimum_height, out int natural_height)
        {
			if (scrollMode || Child == null)
				minimum_height = natural_height = 0;
			else
				Child.GetPreferredHeight(out minimum_height, out natural_height);
        }

        protected override void OnGetPreferredWidth(out int minimum_width, out int natural_width)
        {
			if (scrollMode || Child == null)
				minimum_width = natural_width = 0;
			else
				Child.GetPreferredWidth(out minimum_width, out natural_width);
        }

        protected override void OnSizeAllocated (Rectangle alloc)
		{
			if (scrollMode && Child != null) {
				switch (expandPos) {
				case PositionType.Bottom:
					alloc = new Rectangle (alloc.X, alloc.Y, alloc.Width, targetSize);
					break;
				case PositionType.Top:
					alloc = new Rectangle (alloc.X, alloc.Y - targetSize + alloc.Height, alloc.Width, targetSize);
					break;
				case PositionType.Right:
					alloc = new Rectangle (alloc.X, alloc.Y, targetSize, alloc.Height);
					break;
				case PositionType.Left:
					alloc = new Rectangle (alloc.X - targetSize + alloc.Width, alloc.Y, targetSize, alloc.Height);
					break;
				}
			}
			base.OnSizeAllocated (alloc);
		}
	}

}
