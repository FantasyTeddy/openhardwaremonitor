﻿/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2010-2014 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System.Globalization;

namespace OpenHardwareMonitor.Hardware
{

    internal delegate void ControlEventHandler(Control control);

    internal class Control : IControl
    {
        private readonly ISettings _settings;
        private ControlMode _mode;
        private float _softwareValue;

        public Control(ISensor sensor, ISettings settings, float minSoftwareValue,
          float maxSoftwareValue)
        {
            Identifier = new Identifier(sensor.Identifier, "control");
            _settings = settings;
            MinSoftwareValue = minSoftwareValue;
            MaxSoftwareValue = maxSoftwareValue;

            if (!float.TryParse(settings.GetValue(
                new Identifier(Identifier, "value").ToString(), "0"),
              NumberStyles.Float, CultureInfo.InvariantCulture,
              out _softwareValue))
            {
                _softwareValue = 0;
            }
            if (!int.TryParse(settings.GetValue(
                new Identifier(Identifier, "mode").ToString(),
                ((int)ControlMode.Undefined).ToString(CultureInfo.InvariantCulture)),
              NumberStyles.Integer, CultureInfo.InvariantCulture,
              out int mode))
            {
                _mode = ControlMode.Undefined;
            }
            else
            {
                _mode = (ControlMode)mode;
            }
        }

        public Identifier Identifier { get; }

        public ControlMode ControlMode
        {
            get => _mode;
            private set
            {
                if (_mode != value)
                {
                    _mode = value;
                    ControlModeChanged?.Invoke(this);
                    _settings.SetValue(new Identifier(Identifier, "mode").ToString(),
                      ((int)_mode).ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        public float SoftwareValue
        {
            get => _softwareValue;
            private set
            {
                if (_softwareValue != value)
                {
                    _softwareValue = value;
                    SoftwareControlValueChanged?.Invoke(this);
                    _settings.SetValue(new Identifier(Identifier,
                      "value").ToString(),
                      value.ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        public void SetDefault()
        {
            ControlMode = ControlMode.Default;
        }

        public float MinSoftwareValue { get; }

        public float MaxSoftwareValue { get; }

        public void SetSoftware(float value)
        {
            ControlMode = ControlMode.Software;
            SoftwareValue = value;
        }

        internal event ControlEventHandler ControlModeChanged;
        internal event ControlEventHandler SoftwareControlValueChanged;
    }
}
