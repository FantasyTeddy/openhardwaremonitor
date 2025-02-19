﻿/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2013 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using OpenHardwareMonitor.Hardware;

namespace OpenHardwareMonitor.Utilities
{
    public class Logger
    {

        private const string fileNameFormat =
          "OpenHardwareMonitorLog-{0:yyyy-MM-dd}.csv";

        private readonly IComputer _computer;

        private DateTime _day = DateTime.MinValue;
        private string _fileName;
        private string[] _identifiers;
        private ISensor[] _sensors;

        private DateTime _lastLoggedTime = DateTime.MinValue;

        public Logger(IComputer computer)
        {
            _computer = computer;
            _computer.HardwareAdded += (_, e) => HardwareAdded(e.Hardware);
            _computer.HardwareRemoved += (_, e) => HardwareRemoved(e.Hardware);
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
            if (_sensors == null)
                return;

            for (int i = 0; i < _sensors.Length; i++)
            {
                if (sensor.Identifier.ToString() == _identifiers[i])
                    _sensors[i] = sensor;
            }
        }

        private void SensorRemoved(object sender, SensorEventArgs e)
        {
            RemoveSensor(e.Sensor);
        }

        private void RemoveSensor(ISensor sensor)
        {
            if (_sensors == null)
                return;

            for (int i = 0; i < _sensors.Length; i++)
            {
                if (sensor == _sensors[i])
                    _sensors[i] = null;
            }
        }

        private static string GetFileName(DateTime date)
        {
            return AppDomain.CurrentDomain.BaseDirectory +
              Path.DirectorySeparatorChar + string.Format(fileNameFormat, date);
        }

        private bool OpenExistingLogFile()
        {
            if (!File.Exists(_fileName))
                return false;

            try
            {
                string line;
                using (StreamReader reader = new StreamReader(_fileName))
                    line = reader.ReadLine();

                if (string.IsNullOrEmpty(line))
                    return false;

                _identifiers = line.Split(',').Skip(1).ToArray();
            }
            catch
            {
                _identifiers = null;
                return false;
            }

            if (_identifiers.Length == 0)
            {
                _identifiers = null;
                return false;
            }

            _sensors = new ISensor[_identifiers.Length];
            SensorVisitor visitor = new SensorVisitor((_, e) =>
            {
                for (int i = 0; i < _identifiers.Length; i++)
                {
                    if (e.Sensor.Identifier.ToString() == _identifiers[i])
                        _sensors[i] = e.Sensor;
                }
            });
            visitor.VisitComputer(_computer);
            return true;
        }

        private void CreateNewLogFile()
        {
            IList<ISensor> list = new List<ISensor>();
            SensorVisitor visitor = new SensorVisitor((_, e) =>
            {
                list.Add(e.Sensor);
            });
            visitor.VisitComputer(_computer);
            _sensors = list.ToArray();
            _identifiers = _sensors.Select(s => s.Identifier.ToString()).ToArray();

            using (StreamWriter writer = new StreamWriter(_fileName, false))
            {
                writer.Write(",");
                for (int i = 0; i < _sensors.Length; i++)
                {
                    writer.Write(_sensors[i].Identifier);
                    if (i < _sensors.Length - 1)
                        writer.Write(",");
                    else
                        writer.WriteLine();
                }

                writer.Write("Time,");
                for (int i = 0; i < _sensors.Length; i++)
                {
                    writer.Write('"');
                    writer.Write(_sensors[i].Name);
                    writer.Write('"');
                    if (i < _sensors.Length - 1)
                        writer.Write(",");
                    else
                        writer.WriteLine();
                }
            }
        }

        public TimeSpan LoggingInterval { get; set; }

        public void Log()
        {
            DateTime now = DateTime.Now;

            if (_lastLoggedTime + LoggingInterval - new TimeSpan(5000000) > now)
                return;

            if (_day != now.Date || !File.Exists(_fileName))
            {
                _day = now.Date;
                _fileName = GetFileName(_day);

                if (!OpenExistingLogFile())
                    CreateNewLogFile();
            }

            try
            {
                using (StreamWriter writer = new StreamWriter(new FileStream(_fileName,
                  FileMode.Append, FileAccess.Write, FileShare.ReadWrite)))
                {
                    writer.Write(now.ToString("G", CultureInfo.InvariantCulture));
                    writer.Write(",");
                    for (int i = 0; i < _sensors.Length; i++)
                    {
                        if (_sensors[i] != null)
                        {
                            float? value = _sensors[i].Value;
                            if (value.HasValue)
                            {
                                writer.Write(
                                  value.Value.ToString("R", CultureInfo.InvariantCulture));
                            }
                        }
                        if (i < _sensors.Length - 1)
                            writer.Write(",");
                        else
                            writer.WriteLine();
                    }
                }
            }
            catch (IOException) { }

            _lastLoggedTime = now;
        }
    }
}
