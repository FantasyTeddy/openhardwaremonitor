﻿/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2010 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Globalization;

namespace OpenHardwareMonitor.Hardware
{

    internal readonly struct ParameterDescription
    {
        public ParameterDescription(string name, string description,
          float defaultValue)
        {
            Name = name;
            Description = description;
            DefaultValue = defaultValue;
        }

        public string Name { get; }

        public string Description { get; }

        public float DefaultValue { get; }
    }

    internal class Parameter : IParameter
    {
        private readonly ParameterDescription _description;
        private float _value;
        private bool _isDefault;
        private readonly ISettings _settings;

        public Parameter(ParameterDescription description, ISensor sensor,
          ISettings settings)
        {
            Sensor = sensor;
            _description = description;
            _settings = settings;
            _isDefault = !settings.Contains(Identifier.ToString());
            _value = description.DefaultValue;
            if (!_isDefault)
            {
                if (!float.TryParse(settings.GetValue(Identifier.ToString(), "0"),
                  NumberStyles.Float,
                  CultureInfo.InvariantCulture,
                  out _value))
                {
                    _value = description.DefaultValue;
                }
            }
        }

        public ISensor Sensor { get; }

        public Identifier Identifier => new Identifier(Sensor.Identifier, "parameter",
                  Name.Replace(" ", string.Empty).ToLowerInvariant());

        public string Name => _description.Name;

        public string Description => _description.Description;

        public float Value
        {
            get => _value;
            set
            {
                _isDefault = false;
                _value = value;
                _settings.SetValue(Identifier.ToString(), value.ToString(
                  CultureInfo.InvariantCulture));
            }
        }

        public float DefaultValue => _description.DefaultValue;

        public bool IsDefault
        {
            get => _isDefault;
            set
            {
                _isDefault = value;
                if (value)
                {
                    _value = _description.DefaultValue;
                    _settings.Remove(Identifier.ToString());
                }
            }
        }

        public void Accept(IVisitor visitor)
        {
            if (visitor == null)
                throw new ArgumentNullException(nameof(visitor));
            visitor.VisitParameter(this);
        }

        public void Traverse(IVisitor visitor) { }
    }
}
