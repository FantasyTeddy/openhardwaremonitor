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
        private readonly IComputer computer;
        private readonly PersistentSettings settings;
        private readonly UnitManager unitManager;
        private readonly List<SensorNotifyIcon> list = new List<SensorNotifyIcon>();
        private bool mainIconEnabled;
        private readonly NotifyIconAdv mainIcon;

        public SystemTray(IComputer computer, PersistentSettings settings,
          UnitManager unitManager)
        {
            this.computer = computer;
            this.settings = settings;
            this.unitManager = unitManager;
            computer.HardwareAdded += (_, e) => HardwareAdded(e.Hardware);
            computer.HardwareRemoved += (_, e) => HardwareRemoved(e.Hardware);

            mainIcon = new NotifyIconAdv();

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
            mainIcon.ContextMenu = contextMenu;
            mainIcon.DoubleClick += (obj, args) =>
            {
                SendHideShowCommand();
            };
            mainIcon.Icon = EmbeddedResources.GetIcon("smallicon.ico");
            mainIcon.Text = "Open Hardware Monitor";
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
            if (settings.GetValue(new Identifier(sensor.Identifier,
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
                foreach (SensorNotifyIcon icon in list)
                    icon.Dispose();
                mainIcon.Dispose();
            }
        }

        public void Redraw()
        {
            foreach (SensorNotifyIcon icon in list)
                icon.Update();
        }

        public bool Contains(ISensor sensor)
        {
            foreach (SensorNotifyIcon icon in list)
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
                list.Add(new SensorNotifyIcon(this, sensor, balloonTip, settings, unitManager));
                UpdateMainIconVisibilty();
                settings.SetValue(new Identifier(sensor.Identifier, "tray").ToString(), true);
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
                settings.Remove(
                  new Identifier(sensor.Identifier, "tray").ToString());
                settings.Remove(
                  new Identifier(sensor.Identifier, "traycolor").ToString());
            }
            SensorNotifyIcon instance = null;
            foreach (SensorNotifyIcon icon in list)
            {
                if (icon.Sensor == sensor)
                    instance = icon;
            }

            if (instance != null)
            {
                list.Remove(instance);
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
            if (mainIconEnabled)
            {
                mainIcon.Visible = list.Count == 0;
            }
            else
            {
                mainIcon.Visible = false;
            }
        }

        public bool IsMainIconEnabled
        {
            get => mainIconEnabled;
            set
            {
                if (mainIconEnabled != value)
                {
                    mainIconEnabled = value;
                    UpdateMainIconVisibilty();
                }
            }
        }
    }
}
