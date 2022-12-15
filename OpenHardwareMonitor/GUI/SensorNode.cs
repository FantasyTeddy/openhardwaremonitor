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

namespace OpenHardwareMonitor.GUI
{
    public class SensorNode : Node
    {
        private readonly PersistentSettings _settings;
        private readonly UnitManager _unitManager;
        private readonly string _fixedFormat;
        private bool _plot;
        private Color? _penColor;

        public string ValueToString(float? value)
        {
            if (value.HasValue)
            {
                switch (Sensor.SensorType)
                {
                    case SensorType.Temperature:
                        if (_unitManager.TemperatureUnit == TemperatureUnit.Fahrenheit)
                            return string.Format("{0:F1} °F", value * 1.8 + 32);
                        else
                            return string.Format("{0:F1} °C", value);
                    case SensorType.Throughput:
                        if (value < 1)
                            return string.Format("{0:F1} KB/s", value * 0x400);
                        else
                            return string.Format("{0:F1} MB/s", value);
                    default:
                        return string.Format(_fixedFormat, value);
                }
            }
            else
            {
                return "-";
            }
        }

        public SensorNode(ISensor sensor, PersistentSettings settings,
          UnitManager unitManager)
            : base()
        {
            Sensor = sensor;
            _settings = settings;
            _unitManager = unitManager;
            switch (sensor.SensorType)
            {
                case SensorType.Voltage: _fixedFormat = "{0:F3} V"; break;
                case SensorType.Clock: _fixedFormat = "{0:F1} MHz"; break;
                case SensorType.Load: _fixedFormat = "{0:F1} %"; break;
                case SensorType.Fan: _fixedFormat = "{0:F0} RPM"; break;
                case SensorType.Flow: _fixedFormat = "{0:F0} L/h"; break;
                case SensorType.Control: _fixedFormat = "{0:F1} %"; break;
                case SensorType.Level: _fixedFormat = "{0:F1} %"; break;
                case SensorType.Power: _fixedFormat = "{0:F1} W"; break;
                case SensorType.Data: _fixedFormat = "{0:F1} GB"; break;
                case SensorType.SmallData: _fixedFormat = "{0:F1} MB"; break;
                case SensorType.Factor: _fixedFormat = "{0:F3}"; break;
                default: _fixedFormat = string.Empty; break;
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

        public override string Text
        {
            get => Sensor.Name;
            set => Sensor.Name = value;
        }

        public override bool IsVisible
        {
            get => base.IsVisible;
            set
            {
                base.IsVisible = value;
                _settings.SetValue(new Identifier(Sensor.Identifier,
                  "hidden").ToString(), !value);
            }
        }

        public Color? PenColor
        {
            get => _penColor;
            set
            {
                _penColor = value;

                string id = new Identifier(Sensor.Identifier, "penColor").ToString();
                if (value.HasValue)
                    _settings.SetValue(id, value.Value);
                else
                    _settings.Remove(id);

                PlotSelectionChanged?.Invoke(this, null);
            }
        }

        public bool Plot
        {
            get => _plot;
            set
            {
                _plot = value;
                _settings.SetValue(new Identifier(Sensor.Identifier, "plot").ToString(),
                  value);
                PlotSelectionChanged?.Invoke(this, null);
            }
        }

        public event EventHandler PlotSelectionChanged;

        public ISensor Sensor { get; }

        public string Value => ValueToString(Sensor.Value);

        public string Min => ValueToString(Sensor.Min);

        public string Max => ValueToString(Sensor.Max);

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (!(obj is SensorNode s))
                return false;

            return Sensor == s.Sensor;
        }

        public override int GetHashCode()
        {
            return Sensor.GetHashCode();
        }

    }
}
