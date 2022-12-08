/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2010-2011 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Drawing;
using System.Windows.Forms;

namespace OpenHardwareMonitor.GUI
{
    public abstract class Gadget : IDisposable
    {

        private readonly GadgetWindow window;

        public Gadget()
        {
            window = new GadgetWindow();
            window.Paint += (sender, e) =>
            {
                OnPaint(e);
            };
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                window.Dispose();
            }
        }

        public Point Location
        {
            get => window.Location;
            set => window.Location = value;
        }

        public event EventHandler LocationChanged
        {
            add => window.LocationChanged += value;
            remove => window.LocationChanged -= value;
        }

        public virtual Size Size
        {
            get => window.Size;
            set => window.Size = value;
        }

        public event EventHandler SizeChanged
        {
            add => window.SizeChanged += value;
            remove => window.SizeChanged -= value;
        }

        public byte Opacity
        {
            get => window.Opacity;
            set => window.Opacity = value;
        }

        public bool LockPositionAndSize
        {
            get => window.LockPositionAndSize;
            set => window.LockPositionAndSize = value;
        }

        public bool AlwaysOnTop
        {
            get => window.AlwaysOnTop;
            set => window.AlwaysOnTop = value;
        }

        public ContextMenu ContextMenu
        {
            get => window.ContextMenu;
            set => window.ContextMenu = value;
        }

        public event EventHandler<HitTestEventArgs> HitTest
        {
            add => window.HitTest += value;
            remove => window.HitTest -= value;
        }

        public event MouseEventHandler MouseDoubleClick
        {
            add => window.MouseDoubleClick += value;
            remove => window.MouseDoubleClick -= value;
        }

        public bool Visible
        {
            get => window.Visible;
            set
            {
                if (value != window.Visible)
                {
                    window.Visible = value;
                    VisibleChanged?.Invoke(this, EventArgs.Empty);
                    if (value)
                        Redraw();
                }
            }
        }

        public event EventHandler VisibleChanged;

        public void Redraw()
        {
            window.Redraw();
        }

        protected abstract void OnPaint(PaintEventArgs e);

    }
}
