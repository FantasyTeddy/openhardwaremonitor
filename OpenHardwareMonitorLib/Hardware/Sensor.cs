/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2012 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using OpenHardwareMonitor.Collections;

namespace OpenHardwareMonitor.Hardware
{

    internal class Sensor : ISensor
    {

        private readonly string _defaultName;
        private string _name;
        private readonly Hardware _hardware;
        private readonly ReadOnlyCollection<IParameter> _parameters;
        private float? _currentValue;
        private readonly RingCollection<SensorValue>
          _values = new RingCollection<SensorValue>();
        private readonly ISettings _settings;
        private float _sum;
        private int _count;

        public Sensor(string name, int index, SensorType sensorType,
          Hardware hardware, ISettings settings)
          : this(name, index, sensorType, hardware, null, settings)
        { }

        public Sensor(string name, int index, SensorType sensorType,
          Hardware hardware, ParameterDescription[] parameterDescriptions,
          ISettings settings)
          : this(name, index, false, sensorType, hardware,
            parameterDescriptions, settings)
        { }

        public Sensor(string name, int index, bool defaultHidden,
          SensorType sensorType, Hardware hardware,
          ParameterDescription[] parameterDescriptions, ISettings settings)
        {
            Index = index;
            IsDefaultHidden = defaultHidden;
            SensorType = sensorType;
            _hardware = hardware;
            Parameter[] parameters = new Parameter[parameterDescriptions == null ?
              0 : parameterDescriptions.Length];
            for (int i = 0; i < parameters.Length; i++)
                parameters[i] = new Parameter(parameterDescriptions[i], this, settings);
            _parameters = new ReadOnlyCollection<IParameter>(parameters);

            _settings = settings;
            _defaultName = name;
            _name = settings.GetValue(
              new Identifier(Identifier, "name").ToString(), name);

            GetSensorValuesFromSettings();

            hardware.Closing += (object sender, HardwareEventArgs e) =>
            {
                SetSensorValuesToSettings();
            };
        }

        private void SetSensorValuesToSettings()
        {
            using (MemoryStream m = new MemoryStream())
            {
                using (GZipStream c = new GZipStream(m, CompressionMode.Compress))
                using (BufferedStream b = new BufferedStream(c, 65536))
                using (BinaryWriter writer = new BinaryWriter(b))
                {
                    long t = 0;
                    foreach (SensorValue sensorValue in _values)
                    {
                        long v = sensorValue.Time.ToBinary();
                        writer.Write(v - t);
                        t = v;
                        writer.Write(sensorValue.Value);
                    }
                    writer.Flush();
                }
                _settings.SetValue(new Identifier(Identifier, "values").ToString(),
                  Convert.ToBase64String(m.ToArray()));
            }
        }

        private void GetSensorValuesFromSettings()
        {
            string name = new Identifier(Identifier, "values").ToString();
            string s = _settings.GetValue(name, null);

            try
            {
                byte[] array = Convert.FromBase64String(s);
                s = null;
                DateTime now = DateTime.UtcNow;
                using (MemoryStream m = new MemoryStream(array))
                using (GZipStream c = new GZipStream(m, CompressionMode.Decompress))
                using (BinaryReader reader = new BinaryReader(c))
                {
                    try
                    {
                        long t = 0;
                        while (true)
                        {
                            t += reader.ReadInt64();
                            DateTime time = DateTime.FromBinary(t);
                            if (time > now)
                                break;
                            float value = reader.ReadSingle();
                            AppendValue(value, time);
                        }
                    }
                    catch (EndOfStreamException) { }
                }
            }
            catch { }
            if (_values.Count > 0)
                AppendValue(float.NaN, DateTime.UtcNow);

            // remove the value string from the settings to reduce memory usage
            _settings.Remove(name);
        }

        private void AppendValue(float value, DateTime time)
        {
            if (_values.Count >= 2 && _values.Last.Value == value &&
              _values[_values.Count - 2].Value == value)
            {
                _values.Last = new SensorValue(value, time);
                return;
            }

            _values.Append(new SensorValue(value, time));
        }

        public IHardware Hardware => _hardware;

        public SensorType SensorType { get; }

        public Identifier Identifier => new Identifier(_hardware.Identifier,
                  SensorType.ToString().ToLowerInvariant(),
                  Index.ToString(CultureInfo.InvariantCulture));

        public string Name
        {
            get => _name;
            set
            {
                if (!string.IsNullOrEmpty(value))
                    _name = value;
                else
                    _name = _defaultName;
                _settings.SetValue(new Identifier(Identifier, "name").ToString(), _name);
            }
        }

        public int Index { get; }

        public bool IsDefaultHidden { get; }

        public IReadOnlyList<IParameter> Parameters => _parameters;

        public float? Value
        {
            get => _currentValue;
            set
            {
                DateTime now = DateTime.UtcNow;
                while (_values.Count > 0 && (now - _values.First.Time).TotalDays > 1)
                    _values.Remove();

                if (value.HasValue)
                {
                    _sum += value.Value;
                    _count++;
                    if (_count == 4)
                    {
                        AppendValue(_sum / _count, now);
                        _sum = 0;
                        _count = 0;
                    }
                }

                _currentValue = value;
                if (Min > value || !Min.HasValue)
                    Min = value;
                if (Max < value || !Max.HasValue)
                    Max = value;
            }
        }

        public float? Min { get; private set; }
        public float? Max { get; private set; }

        public void ResetMin()
        {
            Min = null;
        }

        public void ResetMax()
        {
            Max = null;
        }

        public IEnumerable<SensorValue> Values => _values;

        public void Accept(IVisitor visitor)
        {
            if (visitor == null)
                throw new ArgumentNullException(nameof(visitor));
            visitor.VisitSensor(this);
        }

        public void Traverse(IVisitor visitor)
        {
            foreach (IParameter parameter in _parameters)
                parameter.Accept(visitor);
        }

        public IControl Control { get; internal set; }
    }
}
