/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2012 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using OpenHardwareMonitor.Hardware;
using OpenHardwareMonitor.Utilities;

namespace OpenHardwareMonitor.GUI
{
    public class SystemTray : IDisposable
    {
        private readonly IComputer _computer;
        private readonly PersistentSettings _settings;
        private readonly UnitManager _unitManager;
        private readonly List<SensorNotifyIcon> _list = new List<SensorNotifyIcon>();
        private bool _mainIconEnabled;
        private readonly NotifyIconAdv _mainIcon;

        public SystemTray(IComputer computer, PersistentSettings settings,
          UnitManager unitManager)
        {
            _computer = computer;
            _settings = settings;
            _unitManager = unitManager;
            computer.HardwareAdded += (_, e) => HardwareAdded(e.Hardware);
            computer.HardwareRemoved += (_, e) => HardwareRemoved(e.Hardware);

            _mainIcon = new NotifyIconAdv();

            ContextMenu contextMenu = new ContextMenu();
            MenuItem hideShowItem = new MenuItem("Hide/Show");
            hideShowItem.Click += (obj, args) =>
            {
                SendHideShowCommand();
            };
            contextMenu.MenuItems.Add(hideShowItem);
            contextMenu.MenuItems.Add(new MenuItem("-"));
            MenuItem exitItem = new MenuItem("Exit");
            exitItem.Click += (obj, args) =>
            {
                SendExitCommand();
            };
            contextMenu.MenuItems.Add(exitItem);
            _mainIcon.ContextMenu = contextMenu;
            _mainIcon.DoubleClick += (obj, args) =>
            {
                SendHideShowCommand();
            };
            _mainIcon.Icon = EmbeddedResources.GetIcon("smallicon.ico");
            _mainIcon.Text = "Open Hardware Monitor";
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
              "tray").ToString(), false))
            {
                Add(sensor, false);
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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (SensorNotifyIcon icon in _list)
                    icon.Dispose();
                _mainIcon.Dispose();
            }
        }

        public void Redraw()
        {
            foreach (SensorNotifyIcon icon in _list)
                icon.Update();
        }

        public bool Contains(ISensor sensor)
        {
            foreach (SensorNotifyIcon icon in _list)
            {
                if (icon.Sensor == sensor)
                    return true;
            }

            return false;
        }

        public void Add(ISensor sensor, bool balloonTip)
        {
            if (Contains(sensor))
            {
                return;
            }
            else
            {
                _list.Add(new SensorNotifyIcon(this, sensor, balloonTip, _settings, _unitManager));
                UpdateMainIconVisibilty();
                _settings.SetValue(new Identifier(sensor.Identifier, "tray").ToString(), true);
            }
        }

        public void Remove(ISensor sensor)
        {
            Remove(sensor, true);
        }

        private void Remove(ISensor sensor, bool deleteConfig)
        {
            if (deleteConfig)
            {
                _settings.Remove(
                  new Identifier(sensor.Identifier, "tray").ToString());
                _settings.Remove(
                  new Identifier(sensor.Identifier, "traycolor").ToString());
            }
            SensorNotifyIcon instance = null;
            foreach (SensorNotifyIcon icon in _list)
            {
                if (icon.Sensor == sensor)
                    instance = icon;
            }

            if (instance != null)
            {
                _list.Remove(instance);
                UpdateMainIconVisibilty();
                instance.Dispose();
            }
        }

        public event EventHandler HideShowCommand;

        public void SendHideShowCommand()
        {
            HideShowCommand?.Invoke(this, null);
        }

        public event EventHandler ExitCommand;

        public void SendExitCommand()
        {
            ExitCommand?.Invoke(this, null);
        }

        private void UpdateMainIconVisibilty()
        {
            if (_mainIconEnabled)
            {
                _mainIcon.Visible = _list.Count == 0;
            }
            else
            {
                _mainIcon.Visible = false;
            }
        }

        public bool IsMainIconEnabled
        {
            get => _mainIconEnabled;
            set
            {
                if (_mainIconEnabled != value)
                {
                    _mainIconEnabled = value;
                    UpdateMainIconVisibilty();
                }
            }
        }
    }
}
