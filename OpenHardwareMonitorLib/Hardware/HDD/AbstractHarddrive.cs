/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2015 Michael Möller <mmoeller@openhardwaremonitor.org>
  Copyright (C) 2010 Paul Werelds
  Copyright (C) 2011 Roland Reinl <roland-reinl@gmx.de>

*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OpenHardwareMonitor.Collections;

namespace OpenHardwareMonitor.Hardware.HDD
{
    internal abstract class AbstractHarddrive : Hardware
    {

        private const int UPDATE_DIVIDER = 30; // update only every 30s

        // array of all harddrive types, matching type is searched in this order
        private static readonly Type[] hddTypes = {
      typeof(SSDPlextor),
      typeof(SSDIntel),
      typeof(SSDSandforce),
      typeof(SSDIndilinx),
      typeof(SSDSamsung),
      typeof(SSDMicron),
      typeof(GenericHarddisk)
    };

        private readonly string _firmwareRevision;
        private readonly ISmart _smart;

        private readonly IntPtr _handle;
        private readonly int _index;
        private int _count;

        private readonly IList<SmartAttribute> _smartAttributes;
        private IDictionary<SmartAttribute, Sensor> _sensors;

        private readonly DriveInfo[] _driveInfos;
        private Sensor _usageSensor;

        protected AbstractHarddrive(ISmart smart, string name,
          string firmwareRevision, int index,
          IEnumerable<SmartAttribute> smartAttributes, ISettings settings)
          : base(name, new Identifier("hdd",
            index.ToString(CultureInfo.InvariantCulture)), settings)
        {
            _firmwareRevision = firmwareRevision;
            _smart = smart;
            _handle = smart.OpenDrive(index);

            if (_handle != smart.InvalidHandle)
                smart.EnableSmart(_handle, index);

            _index = index;
            _count = 0;

            _smartAttributes = new List<SmartAttribute>(smartAttributes);

            string[] logicalDrives = smart.GetLogicalDrives(index);
            List<DriveInfo> driveInfoList = new List<DriveInfo>(logicalDrives.Length);
            foreach (string logicalDrive in logicalDrives)
            {
                try
                {
                    DriveInfo di = new DriveInfo(logicalDrive);
                    if (di.TotalSize > 0)
                        driveInfoList.Add(new DriveInfo(logicalDrive));
                }
                catch (ArgumentException)
                {
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
            _driveInfos = driveInfoList.ToArray();

            CreateSensors();
        }

        public static AbstractHarddrive CreateInstance(ISmart smart,
          int driveIndex, ISettings settings)
        {
            IntPtr deviceHandle = smart.OpenDrive(driveIndex);

            string name = null;
            string firmwareRevision = null;
            DriveAttributeValue[] values = Array.Empty<DriveAttributeValue>();

            if (deviceHandle != smart.InvalidHandle)
            {
                bool nameValid = smart.ReadNameAndFirmwareRevision(deviceHandle,
                driveIndex, out name, out firmwareRevision);
                bool smartEnabled = smart.EnableSmart(deviceHandle, driveIndex);

                if (smartEnabled)
                    values = smart.ReadSmartData(deviceHandle, driveIndex);

                smart.CloseHandle(deviceHandle);

                if (!nameValid)
                {
                    name = null;
                    firmwareRevision = null;
                }
            }
            else
            {
                string[] logicalDrives = smart.GetLogicalDrives(driveIndex);
                if (logicalDrives == null || logicalDrives.Length == 0)
                    return null;

                bool hasNonZeroSizeDrive = false;
                foreach (string logicalDrive in logicalDrives)
                {
                    try
                    {
                        DriveInfo di = new DriveInfo(logicalDrive);
                        if (di.TotalSize > 0)
                        {
                            hasNonZeroSizeDrive = true;
                            break;
                        }
                    }
                    catch (ArgumentException)
                    {
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }

                if (!hasNonZeroSizeDrive)
                    return null;
            }

            if (string.IsNullOrEmpty(name))
                name = "Generic Hard Disk";

            if (string.IsNullOrEmpty(firmwareRevision))
                firmwareRevision = "Unknown";

            foreach (Type type in hddTypes)
            {
                // get the array of name prefixes for the current type
                NamePrefixAttribute[] namePrefixes = type.GetCustomAttributes(
                  typeof(NamePrefixAttribute), true) as NamePrefixAttribute[];

                // get the array of the required SMART attributes for the current type
                RequireSmartAttribute[] requiredAttributes = type.GetCustomAttributes(
                  typeof(RequireSmartAttribute), true) as RequireSmartAttribute[];

                // check if all required attributes are present
                bool allRequiredAttributesFound = true;
                foreach (RequireSmartAttribute requireAttribute in requiredAttributes)
                {
                    bool adttributeFound = false;
                    foreach (DriveAttributeValue value in values)
                    {
                        if (value.Identifier == requireAttribute.AttributeId)
                        {
                            adttributeFound = true;
                            break;
                        }
                    }
                    if (!adttributeFound)
                    {
                        allRequiredAttributesFound = false;
                        break;
                    }
                }

                // if an attribute is missing, then try the next type
                if (!allRequiredAttributesFound)
                    continue;

                // check if there is a matching name prefix for this type
                foreach (NamePrefixAttribute prefix in namePrefixes)
                {
                    if (name.StartsWith(prefix.Prefix, StringComparison.InvariantCulture))
                    {
                        return Activator.CreateInstance(type, smart, name, firmwareRevision,
                          driveIndex, settings) as AbstractHarddrive;
                    }
                }
            }

            // no matching type has been found
            return null;
        }

        private void CreateSensors()
        {
            _sensors = new Dictionary<SmartAttribute, Sensor>();

            if (_handle != _smart.InvalidHandle)
            {
                IList<Tuple<SensorType, int>> sensorTypeAndChannels =
                  new List<Tuple<SensorType, int>>();

                DriveAttributeValue[] values = _smart.ReadSmartData(_handle, _index);

                foreach (SmartAttribute attribute in _smartAttributes)
                {
                    if (!attribute.SensorType.HasValue)
                        continue;

                    bool found = false;
                    foreach (DriveAttributeValue value in values)
                    {
                        if (value.Identifier == attribute.Identifier)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                        continue;

                    Tuple<SensorType, int> pair = new Tuple<SensorType, int>(
                      attribute.SensorType.Value, attribute.SensorChannel);

                    if (!sensorTypeAndChannels.Contains(pair))
                    {
                        Sensor sensor = new Sensor(attribute.SensorName,
                          attribute.SensorChannel, attribute.DefaultHiddenSensor,
                          attribute.SensorType.Value, this, attribute.ParameterDescriptions,
                          settings);

                        _sensors.Add(attribute, sensor);
                        ActivateSensor(sensor);
                        sensorTypeAndChannels.Add(pair);
                    }
                }
            }

            if (_driveInfos.Length > 0)
            {
                _usageSensor =
                  new Sensor("Used Space", 0, SensorType.Load, this, settings);
                ActivateSensor(_usageSensor);
            }
        }

        public override HardwareType HardwareType => HardwareType.HDD;

        public virtual void UpdateAdditionalSensors(DriveAttributeValue[] values) { }

        public override void Update()
        {
            if (_count == 0)
            {
                if (_handle != _smart.InvalidHandle)
                {
                    DriveAttributeValue[] values = _smart.ReadSmartData(_handle, _index);

                    foreach (KeyValuePair<SmartAttribute, Sensor> keyValuePair in _sensors)
                    {
                        SmartAttribute attribute = keyValuePair.Key;
                        foreach (DriveAttributeValue value in values)
                        {
                            if (value.Identifier == attribute.Identifier)
                            {
                                Sensor sensor = keyValuePair.Value;
                                sensor.Value = attribute.ConvertValue(value, sensor.Parameters);
                            }
                        }
                    }

                    UpdateAdditionalSensors(values);
                }

                if (_usageSensor != null)
                {
                    long totalSize = 0;
                    long totalFreeSpace = 0;

                    for (int i = 0; i < _driveInfos.Length; i++)
                    {
                        if (!_driveInfos[i].IsReady)
                            continue;
                        try
                        {
                            totalSize += _driveInfos[i].TotalSize;
                            totalFreeSpace += _driveInfos[i].TotalFreeSpace;
                        }
                        catch (IOException) { }
                        catch (UnauthorizedAccessException) { }
                    }
                    if (totalSize > 0)
                    {
                        _usageSensor.Value = 100.0f - 100.0f * totalFreeSpace / totalSize;
                    }
                    else
                    {
                        _usageSensor.Value = null;
                    }
                }
            }

            _count++;
            _count %= UPDATE_DIVIDER;
        }

        public override string GetReport()
        {
            StringBuilder r = new StringBuilder();

            r.AppendLine(GetType().Name);
            r.AppendLine();
            r.AppendLine("Drive name: " + name);
            r.AppendLine("Firmware version: " + _firmwareRevision);
            r.AppendLine();

            if (_handle != _smart.InvalidHandle)
            {
                DriveAttributeValue[] values = _smart.ReadSmartData(_handle, _index);
                DriveThresholdValue[] thresholds =
                  _smart.ReadSmartThresholds(_handle, _index);

                if (values.Length > 0)
                {
                    r.AppendFormat(CultureInfo.InvariantCulture,
                      " {0}{1}{2}{3}{4}{5}{6}{7}",
                      "ID".PadRight(3),
                      "Description".PadRight(35),
                      "Raw Value".PadRight(13),
                      "Worst".PadRight(6),
                      "Value".PadRight(6),
                      "Thres".PadRight(6),
                      "Physical".PadRight(8),
                      Environment.NewLine);

                    foreach (DriveAttributeValue value in values)
                    {
                        if (value.Identifier == 0x00)
                            break;

                        byte? threshold = null;
                        foreach (DriveThresholdValue t in thresholds)
                        {
                            if (t.Identifier == value.Identifier)
                            {
                                threshold = t.Threshold;
                            }
                        }

                        string description = "Unknown";
                        float? physical = null;
                        foreach (SmartAttribute a in _smartAttributes)
                        {
                            if (a.Identifier == value.Identifier)
                            {
                                description = a.Name;
                                if (a.HasRawValueConversion | a.SensorType.HasValue)
                                    physical = a.ConvertValue(value, null);
                                else
                                    physical = null;
                            }
                        }

                        string raw = BitConverter.ToString(value.RawValue);
                        r.AppendFormat(CultureInfo.InvariantCulture,
                          " {0}{1}{2}{3}{4}{5}{6}{7}",
                          value.Identifier.ToString("X2").PadRight(3),
                          description.PadRight(35),
                          raw.Replace("-", string.Empty).PadRight(13),
                          value.WorstValue.ToString(CultureInfo.InvariantCulture).PadRight(6),
                          value.AttrValue.ToString(CultureInfo.InvariantCulture).PadRight(6),
                          (threshold.HasValue ? threshold.Value.ToString(
                            CultureInfo.InvariantCulture) : "-").PadRight(6),
                          (physical.HasValue ? physical.Value.ToString(
                            CultureInfo.InvariantCulture) : "-").PadRight(8),
                          Environment.NewLine);
                    }
                    r.AppendLine();
                }
            }

            foreach (DriveInfo di in _driveInfos)
            {
                if (!di.IsReady)
                    continue;
                try
                {
                    r.AppendLine("Logical drive name: " + di.Name);
                    r.AppendLine("Format: " + di.DriveFormat);
                    r.AppendLine("Total size: " + di.TotalSize);
                    r.AppendLine("Total free space: " + di.TotalFreeSpace);
                    r.AppendLine();
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }

            return r.ToString();
        }

        protected static float RawToInt(byte[] raw, byte value,
          IReadOnlyList<IParameter> parameters)
        {
            return (raw[3] << 24) | (raw[2] << 16) | (raw[1] << 8) | raw[0];
        }

        public override void Close()
        {
            if (_handle != _smart.InvalidHandle)
                _smart.CloseHandle(_handle);

            base.Close();
        }

        public override void Traverse(IVisitor visitor)
        {
            foreach (ISensor sensor in Sensors)
                sensor.Accept(visitor);
        }
    }
}
