/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2010-2020 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using OpenHardwareMonitor.Hardware;
using OpenHardwareMonitor.Utilities;

namespace OpenHardwareMonitor.GUI
{
    public class SensorGadget : Gadget
    {

        private readonly UnitManager _unitManager;

        private Image _back = Utilities.EmbeddedResources.GetImage("gadget.png");
        private Image _image;
        private Image _fore;
        private Image _barBack = Utilities.EmbeddedResources.GetImage("barback.png");
        private Image _barFore = Utilities.EmbeddedResources.GetImage("barblue.png");
        private const int topBorder = 6;
        private const int bottomBorder = 7;
        private const int leftBorder = 6;
        private const int rightBorder = 7;
        private Image _background = new Bitmap(1, 1);

        private readonly float _scale;
        private float _fontSize;
        private int _iconSize;
        private int _hardwareLineHeight;
        private int _sensorLineHeight;
        private int _rightMargin;
        private int _leftMargin;
        private int _topMargin;
        private int _bottomMargin;
        private int _progressWidth;

        private readonly IDictionary<IHardware, IList<ISensor>> _sensors =
          new SortedDictionary<IHardware, IList<ISensor>>(new HardwareComparer());

        private readonly PersistentSettings _settings;
        private readonly UserOption _hardwareNames;
        private readonly UserOption _alwaysOnTop;
        private readonly UserOption _lockPositionAndSize;

        private Font _largeFont;
        private Font _smallFont;
        private Brush _darkWhite;
        private StringFormat _stringFormat;
        private StringFormat _trimStringFormat;
        private StringFormat _alignRightStringFormat;

        public SensorGadget(IComputer computer, PersistentSettings settings,
          UnitManager unitManager)
        {
            _unitManager = unitManager;
            _settings = settings;
            computer.HardwareAdded += (_, e) => HardwareAdded(e.Hardware);
            computer.HardwareRemoved += (_, e) => HardwareRemoved(e.Hardware);

            _darkWhite = new SolidBrush(Color.FromArgb(0xF0, 0xF0, 0xF0));

            _stringFormat = new StringFormat
            {
                FormatFlags = StringFormatFlags.NoWrap
            };

            _trimStringFormat = new StringFormat
            {
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            };

            _alignRightStringFormat = new StringFormat
            {
                Alignment = StringAlignment.Far,
                FormatFlags = StringFormatFlags.NoWrap
            };

            if (File.Exists("gadget_background.png"))
            {
                try
                {
                    Image newBack = new Bitmap("gadget_background.png");
                    _back.Dispose();
                    _back = newBack;
                }
                catch { }
            }

            if (File.Exists("gadget_image.png"))
            {
                try
                {
                    _image = new Bitmap("gadget_image.png");
                }
                catch { }
            }

            if (File.Exists("gadget_foreground.png"))
            {
                try
                {
                    _fore = new Bitmap("gadget_foreground.png");
                }
                catch { }
            }

            if (File.Exists("gadget_bar_background.png"))
            {
                try
                {
                    Image newBarBack = new Bitmap("gadget_bar_background.png");
                    _barBack.Dispose();
                    _barBack = newBarBack;
                }
                catch { }
            }

            if (File.Exists("gadget_bar_foreground.png"))
            {
                try
                {
                    Image newBarColor = new Bitmap("gadget_bar_foreground.png");
                    _barFore.Dispose();
                    _barFore = newBarColor;
                }
                catch { }
            }

            Location = new Point(
              settings.GetValue("sensorGadget.Location.X", 100),
              settings.GetValue("sensorGadget.Location.Y", 100));
            LocationChanged += (sender, e) =>
            {
                settings.SetValue("sensorGadget.Location.X", Location.X);
                settings.SetValue("sensorGadget.Location.Y", Location.Y);
            };

            // get the custom to default dpi ratio
            using (Bitmap b = new Bitmap(1, 1))
            {
                _scale = b.HorizontalResolution / 96.0f;
            }

            SetFontSize(settings.GetValue("sensorGadget.FontSize", 7.5f));
            Resize(settings.GetValue("sensorGadget.Width", Size.Width));

            ContextMenu contextMenu = new ContextMenu();
            MenuItem hardwareNamesItem = new MenuItem("Hardware Names");
            contextMenu.MenuItems.Add(hardwareNamesItem);
            MenuItem fontSizeMenu = new MenuItem("Font Size");
            for (int i = 0; i < 4; i++)
            {
                float size;
                string name;
                switch (i)
                {
                    case 0: size = 6.5f; name = "Small"; break;
                    case 1: size = 7.5f; name = "Medium"; break;
                    case 2: size = 9f; name = "Large"; break;
                    case 3: size = 11f; name = "Very Large"; break;
                    default: throw new NotImplementedException();
                }
                MenuItem item = new MenuItem(name)
                {
                    Checked = _fontSize == size
                };
                item.Click += (sender, e) =>
                {
                    SetFontSize(size);
                    settings.SetValue("sensorGadget.FontSize", size);
                    foreach (MenuItem mi in fontSizeMenu.MenuItems)
                        mi.Checked = mi == item;
                };
                fontSizeMenu.MenuItems.Add(item);
            }
            contextMenu.MenuItems.Add(fontSizeMenu);
            contextMenu.MenuItems.Add(new MenuItem("-"));
            MenuItem lockItem = new MenuItem("Lock Position and Size");
            contextMenu.MenuItems.Add(lockItem);
            contextMenu.MenuItems.Add(new MenuItem("-"));
            MenuItem alwaysOnTopItem = new MenuItem("Always on Top");
            contextMenu.MenuItems.Add(alwaysOnTopItem);
            MenuItem opacityMenu = new MenuItem("Opacity");
            contextMenu.MenuItems.Add(opacityMenu);
            Opacity = (byte)settings.GetValue("sensorGadget.Opacity", 255);
            for (int i = 0; i < 5; i++)
            {
                MenuItem item = new MenuItem((20 * (i + 1)).ToString() + " %");
                byte o = (byte)(51 * (i + 1));
                item.Checked = Opacity == o;
                item.Click += (sender, e) =>
                {
                    Opacity = o;
                    settings.SetValue("sensorGadget.Opacity", Opacity);
                    foreach (MenuItem mi in opacityMenu.MenuItems)
                        mi.Checked = mi == item;
                };
                opacityMenu.MenuItems.Add(item);
            }
            ContextMenu = contextMenu;

            _hardwareNames = new UserOption("sensorGadget.Hardwarenames", true,
              hardwareNamesItem, settings);
            _hardwareNames.Changed += (sender, e) =>
            {
                Resize();
            };

            _alwaysOnTop = new UserOption("sensorGadget.AlwaysOnTop", false,
              alwaysOnTopItem, settings);
            _alwaysOnTop.Changed += (sender, e) =>
            {
                AlwaysOnTop = _alwaysOnTop.Value;
            };
            _lockPositionAndSize = new UserOption("sensorGadget.LockPositionAndSize",
              false, lockItem, settings);
            _lockPositionAndSize.Changed += (sender, e) =>
            {
                LockPositionAndSize = _lockPositionAndSize.Value;
            };

            HitTest += (sender, e) =>
            {
                if (_lockPositionAndSize.Value)
                    return;

                if (e.Location.X < leftBorder)
                {
                    e.HitResult = HitResult.Left;
                    return;
                }
                if (e.Location.X > Size.Width - 1 - rightBorder)
                {
                    e.HitResult = HitResult.Right;
                    return;
                }
            };

            SizeChanged += (sender, e) =>
            {
                settings.SetValue("sensorGadget.Width", Size.Width);
                Redraw();
            };

            VisibleChanged += (sender, e) =>
            {
                Rectangle bounds = new Rectangle(Location, Size);
                Screen screen = Screen.FromRectangle(bounds);
                Rectangle intersection =
                  Rectangle.Intersect(screen.WorkingArea, bounds);
                if (intersection.Width < Math.Min(16, bounds.Width) ||
                    intersection.Height < Math.Min(16, bounds.Height))
                {
                    Location = new Point(
                      screen.WorkingArea.Width / 2 - bounds.Width / 2,
                      screen.WorkingArea.Height / 2 - bounds.Height / 2);
                }
            };

            MouseDoubleClick += (obj, args) =>
            {
                SendHideShowCommand();
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _largeFont.Dispose();
                _largeFont = null;

                _smallFont.Dispose();
                _smallFont = null;

                _darkWhite.Dispose();
                _darkWhite = null;

                _stringFormat.Dispose();
                _stringFormat = null;

                _trimStringFormat.Dispose();
                _trimStringFormat = null;

                _alignRightStringFormat.Dispose();
                _alignRightStringFormat = null;

                _back.Dispose();
                _back = null;

                _barFore.Dispose();
                _barFore = null;

                _barBack.Dispose();
                _barBack = null;

                _background.Dispose();
                _background = null;

                if (_image != null)
                {
                    _image.Dispose();
                    _image = null;
                }

                if (_fore != null)
                {
                    _fore.Dispose();
                    _fore = null;
                }
            }

            base.Dispose(disposing);
        }

        private void HardwareRemoved(IHardware hardware)
        {
            hardware.SensorAdded -= SensorAdded;
            hardware.SensorRemoved -= SensorRemoved;
            foreach (ISensor sensor in hardware.Sensors)
                RemoveSensor(sensor);
            foreach (IHardware subHardware in hardware.SubHardware)
                HardwareRemoved(subHardware);
        }

        private void HardwareAdded(IHardware hardware)
        {
            foreach (ISensor sensor in hardware.Sensors)
                AddSensor(sensor);
            hardware.SensorAdded += SensorAdded;
            hardware.SensorRemoved += SensorRemoved;
            foreach (IHardware subHardware in hardware.SubHardware)
                HardwareAdded(subHardware);
        }

        private void SensorAdded(object sender, SensorEventArgs e)
        {
            AddSensor(e.Sensor);
        }

        private void AddSensor(ISensor sensor)
        {
            if (_settings.GetValue(new Identifier(sensor.Identifier,
              "gadget").ToString(), false))
            {
                Add(sensor);
            }
        }

        private void SensorRemoved(object sender, SensorEventArgs e)
        {
            RemoveSensor(e.Sensor);
        }

        private void RemoveSensor(ISensor sensor)
        {
            if (Contains(sensor))
                Remove(sensor, false);
        }

        public bool Contains(ISensor sensor)
        {
            foreach (IList<ISensor> list in _sensors.Values)
            {
                if (list.Contains(sensor))
                    return true;
            }

            return false;
        }

        public void Add(ISensor sensor)
        {
            if (Contains(sensor))
            {
                return;
            }
            else
            {
                // get the right hardware
                IHardware hardware = sensor.Hardware;
                while (hardware.Parent != null)
                    hardware = hardware.Parent;

                // get the sensor list associated with the hardware
                if (!_sensors.TryGetValue(hardware, out IList<ISensor> list))
                {
                    list = new List<ISensor>();
                    _sensors.Add(hardware, list);
                }

                // insert the sensor at the right position
                int i = 0;
                while (i < list.Count && (list[i].SensorType < sensor.SensorType ||
                  (list[i].SensorType == sensor.SensorType &&
                   list[i].Index < sensor.Index)))
                {
                    i++;
                }

                list.Insert(i, sensor);

                _settings.SetValue(
                  new Identifier(sensor.Identifier, "gadget").ToString(), true);

                Resize();
            }
        }

        public void Remove(ISensor sensor)
        {
            Remove(sensor, true);
        }

        private void Remove(ISensor sensor, bool deleteConfig)
        {
            if (deleteConfig)
                _settings.Remove(new Identifier(sensor.Identifier, "gadget").ToString());

            foreach (KeyValuePair<IHardware, IList<ISensor>> keyValue in _sensors)
            {
                if (keyValue.Value.Contains(sensor))
                {
                    keyValue.Value.Remove(sensor);
                    if (keyValue.Value.Count == 0)
                    {
                        _sensors.Remove(keyValue.Key);
                        break;
                    }
                }
            }

            Resize();
        }

        public event EventHandler HideShowCommand;

        public void SendHideShowCommand()
        {
            HideShowCommand?.Invoke(this, null);
        }

        private static Font CreateFont(float size, FontStyle style)
        {
            try
            {
                return new Font(SystemFonts.MessageBoxFont.FontFamily, size, style);
            }
            catch (ArgumentException)
            {
                // if the style is not supported, fall back to the original one
                return new Font(SystemFonts.MessageBoxFont.FontFamily, size,
                  SystemFonts.MessageBoxFont.Style);
            }
        }

        private void SetFontSize(float size)
        {
            _fontSize = size;
            _largeFont = CreateFont(_fontSize, FontStyle.Bold);
            _smallFont = CreateFont(_fontSize, FontStyle.Regular);

            double scaledFontSize = _fontSize * _scale;
            _iconSize = (int)Math.Round(1.5 * scaledFontSize);
            _hardwareLineHeight = (int)Math.Round(1.66 * scaledFontSize);
            _sensorLineHeight = (int)Math.Round(1.33 * scaledFontSize);
            _leftMargin = leftBorder + (int)Math.Round(0.3 * scaledFontSize);
            _rightMargin = rightBorder + (int)Math.Round(0.3 * scaledFontSize);
            _topMargin = topBorder;
            _bottomMargin = bottomBorder + (int)Math.Round(0.3 * scaledFontSize);
            _progressWidth = (int)Math.Round(5.3 * scaledFontSize);

            Resize((int)Math.Round(17.3 * scaledFontSize));
        }

        private void Resize()
        {
            Resize(Size.Width);
        }

        private void Resize(int width)
        {
            int y = _topMargin;
            foreach (KeyValuePair<IHardware, IList<ISensor>> pair in _sensors)
            {
                if (_hardwareNames.Value)
                {
                    if (y > _topMargin)
                        y += _hardwareLineHeight - _sensorLineHeight;
                    y += _hardwareLineHeight;
                }
                y += pair.Value.Count * _sensorLineHeight;
            }
            if (_sensors.Count == 0)
                y += 4 * _sensorLineHeight + _hardwareLineHeight;
            y += _bottomMargin;
            Size = new Size(width, y);
        }

        private static void DrawImageWidthBorder(Graphics g, int width, int height,
          Image back, int t, int b, int l, int r)
        {
            GraphicsUnit u = GraphicsUnit.Pixel;

            g.DrawImage(back, new Rectangle(0, 0, l, t),
                  new Rectangle(0, 0, l, t), u);
            g.DrawImage(back, new Rectangle(l, 0, width - l - r, t),
              new Rectangle(l, 0, back.Width - l - r, t), u);
            g.DrawImage(back, new Rectangle(width - r, 0, r, t),
              new Rectangle(back.Width - r, 0, r, t), u);

            g.DrawImage(back, new Rectangle(0, t, l, height - t - b),
              new Rectangle(0, t, l, back.Height - t - b), u);
            g.DrawImage(back, new Rectangle(l, t, width - l - r, height - t - b),
              new Rectangle(l, t, back.Width - l - r, back.Height - t - b), u);
            g.DrawImage(back, new Rectangle(width - r, t, r, height - t - b),
              new Rectangle(back.Width - r, t, r, back.Height - t - b), u);

            g.DrawImage(back, new Rectangle(0, height - b, l, b),
              new Rectangle(0, back.Height - b, l, b), u);
            g.DrawImage(back, new Rectangle(l, height - b, width - l - r, b),
              new Rectangle(l, back.Height - b, back.Width - l - r, b), u);
            g.DrawImage(back, new Rectangle(width - r, height - b, r, b),
              new Rectangle(back.Width - r, back.Height - b, r, b), u);
        }

        private void DrawBackground(Graphics g)
        {
            int w = Size.Width;
            int h = Size.Height;

            if (w != _background.Width || h != _background.Height)
            {

                _background.Dispose();
                _background = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                using (Graphics graphics = Graphics.FromImage(_background))
                {

                    DrawImageWidthBorder(graphics, w, h, _back, topBorder, bottomBorder,
                      leftBorder, rightBorder);

                    if (_fore != null)
                    {
                        DrawImageWidthBorder(graphics, w, h, _fore, topBorder, bottomBorder,
                        leftBorder, rightBorder);
                    }

                    if (_image != null)
                    {
                        int width = w - leftBorder - rightBorder;
                        int height = h - topBorder - bottomBorder;
                        float xRatio = width / (float)_image.Width;
                        float yRatio = height / (float)_image.Height;
                        float destWidth, destHeight;
                        float xOffset, yOffset;
                        if (xRatio < yRatio)
                        {
                            destWidth = width;
                            destHeight = _image.Height * xRatio;
                            xOffset = 0;
                            yOffset = 0.5f * (height - destHeight);
                        }
                        else
                        {
                            destWidth = _image.Width * yRatio;
                            destHeight = height;
                            xOffset = 0.5f * (width - destWidth);
                            yOffset = 0;
                        }

                        graphics.DrawImage(_image,
                          new RectangleF(leftBorder + xOffset, topBorder + yOffset,
                            destWidth, destHeight));
                    }
                }
            }

            g.DrawImageUnscaled(_background, 0, 0);
        }

        private void DrawProgress(Graphics g, float x, float y,
          float width, float height, float progress)
        {
            g.DrawImage(_barBack,
              new RectangleF(x + width * progress, y, width * (1 - progress), height),
              new RectangleF(_barBack.Width * progress, 0,
                (1 - progress) * _barBack.Width, _barBack.Height),
              GraphicsUnit.Pixel);
            g.DrawImage(_barFore,
              new RectangleF(x, y, width * progress, height),
              new RectangleF(0, 0, progress * _barFore.Width, _barFore.Height),
              GraphicsUnit.Pixel);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            int w = Size.Width;

            g.Clear(Color.Transparent);

            DrawBackground(g);

            int x;
            int y = _topMargin;

            if (_sensors.Count == 0)
            {
                x = leftBorder + 1;
                g.DrawString("Right-click on a sensor in the main window and select " +
                  "\"Show in Gadget\" to show the sensor here.",
                  _smallFont, Brushes.White,
                  new Rectangle(x, y - 1, w - rightBorder - x, 0));
            }

            foreach (KeyValuePair<IHardware, IList<ISensor>> pair in _sensors)
            {
                if (_hardwareNames.Value)
                {
                    if (y > _topMargin)
                        y += _hardwareLineHeight - _sensorLineHeight;
                    x = leftBorder + 1;
                    g.DrawImage(HardwareTypeImage.Instance.GetImage(pair.Key.HardwareType),
                      new Rectangle(x, y + 1, _iconSize, _iconSize));
                    x += _iconSize + 1;
                    g.DrawString(pair.Key.Name, _largeFont, Brushes.White,
                      new Rectangle(x, y - 1, w - rightBorder - x, 0),
                      _stringFormat);
                    y += _hardwareLineHeight;
                }

                foreach (ISensor sensor in pair.Value)
                {
                    int remainingWidth;


                    if ((sensor.SensorType != SensorType.Load &&
                         sensor.SensorType != SensorType.Control &&
                         sensor.SensorType != SensorType.Level) || !sensor.Value.HasValue)
                    {
                        string formatted;

                        if (sensor.Value.HasValue)
                        {
                            string format = string.Empty;
                            switch (sensor.SensorType)
                            {
                                case SensorType.Voltage:
                                    format = "{0:F3} V";
                                    break;
                                case SensorType.Clock:
                                    format = "{0:F0} MHz";
                                    break;
                                case SensorType.Fan:
                                    format = "{0:F0} RPM";
                                    break;
                                case SensorType.Flow:
                                    format = "{0:F0} L/h";
                                    break;
                                case SensorType.Power:
                                    format = "{0:F1} W";
                                    break;
                                case SensorType.Data:
                                    format = "{0:F1} GB";
                                    break;
                                case SensorType.Factor:
                                    format = "{0:F3}";
                                    break;
                                case SensorType.SmallData:
                                    format = "{0:F1} MB";
                                    break;
                            }

                            switch (sensor.SensorType)
                            {
                                case SensorType.Temperature:
                                    if (_unitManager.TemperatureUnit == TemperatureUnit.Fahrenheit)
                                    {
                                        formatted = string.Format("{0:F1} °F",
                                          UnitManager.CelsiusToFahrenheit(sensor.Value));
                                    }
                                    else
                                    {
                                        formatted = string.Format("{0:F1} °C", sensor.Value);
                                    }
                                    break;
                                case SensorType.Throughput:
                                    if (sensor.Value < 1)
                                    {
                                        formatted =
                                          string.Format("{0:F1} KB/s", sensor.Value * 0x400);
                                    }
                                    else
                                    {
                                        formatted =
                                          string.Format("{0:F1} MB/s", sensor.Value);
                                    }
                                    break;
                                default:
                                    formatted = string.Format(format, sensor.Value);
                                    break;
                            }
                        }
                        else
                        {
                            formatted = "-";
                        }

                        g.DrawString(formatted, _smallFont, _darkWhite,
                          new RectangleF(-1, y - 1, w - _rightMargin + 3, 0),
                          _alignRightStringFormat);

                        remainingWidth = w - (int)Math.Floor(g.MeasureString(formatted,
                          _smallFont, w, StringFormat.GenericTypographic).Width) -
                          _rightMargin;
                    }
                    else
                    {
                        DrawProgress(g, w - _progressWidth - _rightMargin,
                          y + 0.35f * _sensorLineHeight, _progressWidth,
                          0.6f * _sensorLineHeight, 0.01f * sensor.Value.Value);

                        remainingWidth = w - _progressWidth - _rightMargin;
                    }

                    remainingWidth -= _leftMargin + 2;
                    if (remainingWidth > 0)
                    {
                        g.DrawString(sensor.Name, _smallFont, _darkWhite,
                          new RectangleF(_leftMargin - 1, y - 1, remainingWidth, 0),
                          _trimStringFormat);
                    }

                    y += _sensorLineHeight;
                }
            }
        }

        private class HardwareComparer : IComparer<IHardware>
        {
            public int Compare(IHardware x, IHardware y)
            {
                if (x == null && y == null)
                    return 0;
                if (x == null)
                    return -1;
                if (y == null)
                    return 1;

                if (x.HardwareType != y.HardwareType)
                    return x.HardwareType.CompareTo(y.HardwareType);

                return x.Identifier.CompareTo(y.Identifier);
            }
        }
    }
}

