﻿/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2011 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace OpenHardwareMonitor.Hardware.Mainboard
{

    internal class GigabyteTAMG
    {
        private readonly byte[] _table;

        private readonly Sensor[] _sensors;

        private struct Sensor
        {
            public string Name;
            public SensorType Type;
            public int Channel;
            public float Value;
        }

        private enum SensorType
        {
            Voltage = 1,
            Temperature = 2,
            Fan = 4,
            Case = 8,
        }

        public GigabyteTAMG(byte[] table)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));

            int index = IndexOf(table, Encoding.ASCII.GetBytes("$HEALTH$"), 0);

            if (index >= 0)
            {
                index += 8;
                using (MemoryStream m =
                  new MemoryStream(table, index, table.Length - index))
                using (BinaryReader r = new BinaryReader(m))
                {
                    try
                    {
                        r.ReadInt64();
                        int count = r.ReadInt32();
                        r.ReadInt64();
                        r.ReadInt32();
                        _sensors = new Sensor[count];
                        for (int i = 0; i < _sensors.Length; i++)
                        {
                            _sensors[i].Name = new string(r.ReadChars(32)).TrimEnd('\0');
                            _sensors[i].Type = (SensorType)r.ReadByte();
                            _sensors[i].Channel = r.ReadInt16();
                            _sensors[i].Channel |= r.ReadByte() << 24;
                            r.ReadInt64();
                            int value = r.ReadInt32();
                            switch (_sensors[i].Type)
                            {
                                case SensorType.Voltage:
                                    _sensors[i].Value = 1e-3f * value; break;
                                default:
                                    _sensors[i].Value = value; break;
                            }
                            r.ReadInt64();
                        }
                    }
                    catch (IOException) { _sensors = Array.Empty<Sensor>(); }
                }
            }
            else
            {
                _sensors = Array.Empty<Sensor>();
            }
        }

        public static int IndexOf(byte[] array, byte[] pattern, int startIndex)
        {
            if (array == null || pattern == null || pattern.Length > array.Length)
                return -1;

            for (int i = startIndex; i < array.Length - pattern.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (array[i + j] != pattern[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found)
                    return i;
            }
            return -1;
        }

        private string GetCompressedAndEncodedTable()
        {
            string base64;
            using (MemoryStream m = new MemoryStream())
            {
                using (GZipStream c = new GZipStream(m, CompressionMode.Compress))
                {
                    c.Write(_table, 0, _table.Length);
                }
                base64 = Convert.ToBase64String(m.ToArray());
            }

            StringBuilder r = new StringBuilder();
            for (int i = 0; i < Math.Ceiling(base64.Length / 64.0); i++)
            {
                r.Append(' ');
                for (int j = 0; j < 0x40; j++)
                {
                    int index = (i << 6) | j;
                    if (index < base64.Length)
                    {
                        r.Append(base64[index]);
                    }
                }
                r.AppendLine();
            }

            return r.ToString();
        }

        public string GetReport()
        {
            StringBuilder r = new StringBuilder();

            if (_sensors.Length > 0)
            {
                r.AppendLine("Gigabyte TAMG Sensors");
                r.AppendLine();

                foreach (Sensor sensor in _sensors)
                {
                    r.AppendFormat(" {0,-10}: {1,8:G6} ({2})", sensor.Name, sensor.Value,
                      sensor.Type);
                    r.AppendLine();
                }
                r.AppendLine();
            }

            if (_table.Length > 0)
            {
                r.AppendLine("Gigabyte TAMG Table");
                r.AppendLine();
                r.Append(GetCompressedAndEncodedTable());
                r.AppendLine();
            }

            return r.ToString();
        }
    }
}
