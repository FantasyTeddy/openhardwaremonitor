/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

using System;
using System.Drawing;
using OpenHardwareMonitor.Hardware;
using OpenHardwareMonitor.Utilities;

namespace OpenHardwareMonitor.GUI {
    public class SensorNode : Node {
        private readonly PersistentSettings settings;
        private readonly UnitManager unitManager;
        private readonly string fixedFormat;
        private bool plot = false;
        private Color? penColor = null;

        public string ValueToString(float? value) {
            if (value.HasValue) {
                switch (Sensor.SensorType) {
                    case SensorType.Temperature:
                        if (unitManager.TemperatureUnit == TemperatureUnit.Fahrenheit)
                            return string.Format("{0:F1} °F", value * 1.8 + 32);
                        else
                            return string.Format("{0:F1} °C", value);
                    case SensorType.Throughput:
                        if (value < 1)
                            return string.Format("{0:F1} KB/s", value * 0x400);
                        else
                            return string.Format("{0:F1} MB/s", value);
                    default:
                        return string.Format(fixedFormat, value);
                }
            } else {
                return "-";
            }
        }

        public SensorNode(ISensor sensor, PersistentSettings settings,
          UnitManager unitManager) : base() {
            Sensor = sensor;
            this.settings = settings;
            this.unitManager = unitManager;
            switch (sensor.SensorType) {
                case SensorType.Voltage: fixedFormat = "{0:F3} V"; break;
                case SensorType.Clock: fixedFormat = "{0:F1} MHz"; break;
                case SensorType.Load: fixedFormat = "{0:F1} %"; break;
                case SensorType.Fan: fixedFormat = "{0:F0} RPM"; break;
                case SensorType.Flow: fixedFormat = "{0:F0} L/h"; break;
                case SensorType.Control: fixedFormat = "{0:F1} %"; break;
                case SensorType.Level: fixedFormat = "{0:F1} %"; break;
                case SensorType.Power: fixedFormat = "{0:F1} W"; break;
                case SensorType.Data: fixedFormat = "{0:F1} GB"; break;
                case SensorType.SmallData: fixedFormat = "{0:F1} MB"; break;
                case SensorType.Factor: fixedFormat = "{0:F3}"; break;
                default: fixedFormat = ""; break;
            }

            bool hidden = settings.GetValue(new Identifier(sensor.Identifier,
              "hidden").ToString(), sensor.IsDefaultHidden);
            base.IsVisible = !hidden;

            Plot = settings.GetValue(new Identifier(sensor.Identifier,
              "plot").ToString(), false);

            string id = new Identifier(sensor.Identifier, "penColor").ToString();
            if (settings.Contains(id))
                PenColor = settings.GetValue(id, Color.Black);
        }

        public override string Text {
            get => Sensor.Name;
            set => Sensor.Name = value;
        }

        public override bool IsVisible {
            get => base.IsVisible;
            set {
                base.IsVisible = value;
                settings.SetValue(new Identifier(Sensor.Identifier,
                  "hidden").ToString(), !value);
            }
        }

        public Color? PenColor {
            get => penColor;
            set {
                penColor = value;

                string id = new Identifier(Sensor.Identifier, "penColor").ToString();
                if (value.HasValue)
                    settings.SetValue(id, value.Value);
                else
                    settings.Remove(id);

                PlotSelectionChanged?.Invoke(this, null);
            }
        }

        public bool Plot {
            get => plot;
            set {
                plot = value;
                settings.SetValue(new Identifier(Sensor.Identifier, "plot").ToString(),
                  value);
                PlotSelectionChanged?.Invoke(this, null);
            }
        }

        public event EventHandler PlotSelectionChanged;

        public ISensor Sensor { get; }

        public string Value => ValueToString(Sensor.Value);

        public string Min => ValueToString(Sensor.Min);

        public string Max => ValueToString(Sensor.Max);

        public override bool Equals(object obj) {
            if (obj == null)
                return false;

            if (!(obj is SensorNode s))
                return false;

            return Sensor == s.Sensor;
        }

        public override int GetHashCode() {
            return Sensor.GetHashCode();
        }

    }
}
