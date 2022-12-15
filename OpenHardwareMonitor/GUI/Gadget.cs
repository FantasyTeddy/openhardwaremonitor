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

        private readonly GadgetWindow _window;

        public Gadget()
        {
            _window = new GadgetWindow();
            _window.Paint += (sender, e) =>
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
                _window.Dispose();
            }
        }

        public Point Location
        {
            get => _window.Location;
            set => _window.Location = value;
        }

        public event EventHandler LocationChanged
        {
            add => _window.LocationChanged += value;
            remove => _window.LocationChanged -= value;
        }

        public virtual Size Size
        {
            get => _window.Size;
            set => _window.Size = value;
        }

        public event EventHandler SizeChanged
        {
            add => _window.SizeChanged += value;
            remove => _window.SizeChanged -= value;
        }

        public byte Opacity
        {
            get => _window.Opacity;
            set => _window.Opacity = value;
        }

        public bool LockPositionAndSize
        {
            get => _window.LockPositionAndSize;
            set => _window.LockPositionAndSize = value;
        }

        public bool AlwaysOnTop
        {
            get => _window.AlwaysOnTop;
            set => _window.AlwaysOnTop = value;
        }

        public ContextMenu ContextMenu
        {
            get => _window.ContextMenu;
            set => _window.ContextMenu = value;
        }

        public event EventHandler<HitTestEventArgs> HitTest
        {
            add => _window.HitTest += value;
            remove => _window.HitTest -= value;
        }

        public event MouseEventHandler MouseDoubleClick
        {
            add => _window.MouseDoubleClick += value;
            remove => _window.MouseDoubleClick -= value;
        }

        public bool Visible
        {
            get => _window.Visible;
            set
            {
                if (value != _window.Visible)
                {
                    _window.Visible = value;
                    VisibleChanged?.Invoke(this, EventArgs.Empty);
                    if (value)
                        Redraw();
                }
            }
        }

        public event EventHandler VisibleChanged;

        public void Redraw()
        {
            _window.Redraw();
        }

        protected abstract void OnPaint(PaintEventArgs e);

    }
}
