/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>
  Copyright (C) 2011 Christian Vallières

*/

using System;
using System.Globalization;
using System.Text;

namespace OpenHardwareMonitor.Hardware.Nvidia
{
    internal class NvidiaGPU : Hardware
    {

        private readonly int _adapterIndex;
        private readonly NvPhysicalGpuHandle _handle;
        private readonly NvDisplayHandle? _displayHandle;
        private readonly NVML.NvmlDevice? _device;

        private readonly Sensor[] _temperatures;
        private readonly Sensor _fan;
        private readonly Sensor[] _clocks;
        private readonly Sensor[] _loads;
        private readonly Sensor _control;
        private readonly Sensor _memoryLoad;
        private readonly Sensor _memoryUsed;
        private readonly Sensor _memoryFree;
        private readonly Sensor _memoryAvail;
        private readonly Sensor _power;
        private readonly Sensor _pcieThroughputRx;
        private readonly Sensor _pcieThroughputTx;
        private readonly Control _fanControl;

        public NvidiaGPU(int adapterIndex, NvPhysicalGpuHandle handle,
          NvDisplayHandle? displayHandle, ISettings settings)
          : base(GetName(handle), new Identifier("nvidiagpu",
              adapterIndex.ToString(CultureInfo.InvariantCulture)), settings)
        {
            _adapterIndex = adapterIndex;
            _handle = handle;
            _displayHandle = displayHandle;

            NvGPUThermalSettings thermalSettings = GetThermalSettings();
            _temperatures = new Sensor[thermalSettings.Count];
            for (int i = 0; i < _temperatures.Length; i++)
            {
                NvSensor sensor = thermalSettings.Sensor[i];
                string name;
                switch (sensor.Target)
                {
                    case NvThermalTarget.BOARD: name = "GPU Board"; break;
                    case NvThermalTarget.GPU: name = "GPU Core"; break;
                    case NvThermalTarget.MEMORY: name = "GPU Memory"; break;
                    case NvThermalTarget.POWER_SUPPLY: name = "GPU Power Supply"; break;
                    case NvThermalTarget.UNKNOWN: name = "GPU Unknown"; break;
                    default: name = "GPU"; break;
                }
                _temperatures[i] = new Sensor(name, i, SensorType.Temperature, this,
                  Array.Empty<ParameterDescription>(), settings);
                ActivateSensor(_temperatures[i]);
            }

            _fan = new Sensor("GPU", 0, SensorType.Fan, this, settings);

            _clocks = new Sensor[3];
            _clocks[0] = new Sensor("GPU Core", 0, SensorType.Clock, this, settings);
            _clocks[1] = new Sensor("GPU Memory", 1, SensorType.Clock, this, settings);
            _clocks[2] = new Sensor("GPU Shader", 2, SensorType.Clock, this, settings);
            for (int i = 0; i < _clocks.Length; i++)
                ActivateSensor(_clocks[i]);

            _loads = new Sensor[4];
            _loads[0] = new Sensor("GPU Core", 0, SensorType.Load, this, settings);
            _loads[1] = new Sensor("GPU Frame Buffer", 1, SensorType.Load, this, settings);
            _loads[2] = new Sensor("GPU Video Engine", 2, SensorType.Load, this, settings);
            _loads[3] = new Sensor("GPU Bus Interface", 3, SensorType.Load, this, settings);
            _memoryLoad = new Sensor("GPU Memory", 4, SensorType.Load, this, settings);
            _memoryFree = new Sensor("GPU Memory Free", 1, SensorType.SmallData, this, settings);
            _memoryUsed = new Sensor("GPU Memory Used", 2, SensorType.SmallData, this, settings);
            _memoryAvail = new Sensor("GPU Memory Total", 3, SensorType.SmallData, this, settings);
            _control = new Sensor("GPU Fan", 0, SensorType.Control, this, settings);

            NvGPUCoolerSettings coolerSettings = GetCoolerSettings();
            if (coolerSettings.Count > 0)
            {
                _fanControl = new Control(_control, settings,
                  coolerSettings.Cooler[0].DefaultMin,
                  coolerSettings.Cooler[0].DefaultMax);
                _fanControl.ControlModeChanged += ControlModeChanged;
                _fanControl.SoftwareControlValueChanged += SoftwareControlValueChanged;
                ControlModeChanged(_fanControl);
                _control.Control = _fanControl;
            }

            if (NVML.IsInitialized)
            {
                if (NVAPI.NvAPI_GPU_GetBusId != null &&
                    NVAPI.NvAPI_GPU_GetBusId(handle, out uint busId) == NvStatus.OK)
                {
                    if (NVML.NvmlDeviceGetHandleByPciBusId != null &&
                      NVML.NvmlDeviceGetHandleByPciBusId(
                      "0000:" + busId.ToString("X2") + ":00.0", out NVML.NvmlDevice result)
                      == NVML.NvmlReturn.Success)
                    {
                        _device = result;
                        _power = new Sensor("GPU Power", 0, SensorType.Power, this, settings);
                        _pcieThroughputRx = new Sensor("GPU PCIE Rx", 0,
                          SensorType.Throughput, this, settings);
                        _pcieThroughputTx = new Sensor("GPU PCIE Tx", 1,
                          SensorType.Throughput, this, settings);
                    }
                }
            }

            Update();
        }

        private static string GetName(NvPhysicalGpuHandle handle)
        {
            if (NVAPI.NvAPI_GPU_GetFullName(handle, out string gpuName) == NvStatus.OK)
            {
                return "NVIDIA " + gpuName.Trim();
            }
            else
            {
                return "NVIDIA";
            }
        }

        public override HardwareType HardwareType => HardwareType.GpuNvidia;

        private NvGPUThermalSettings GetThermalSettings()
        {
            NvGPUThermalSettings settings = new NvGPUThermalSettings
            {
                Version = NVAPI.GPU_THERMAL_SETTINGS_VER,
                Count = NVAPI.MAX_THERMAL_SENSORS_PER_GPU,
                Sensor = new NvSensor[NVAPI.MAX_THERMAL_SENSORS_PER_GPU]
            };
            if (!(NVAPI.NvAPI_GPU_GetThermalSettings != null &&
              NVAPI.NvAPI_GPU_GetThermalSettings(_handle, (int)NvThermalTarget.ALL,
                ref settings) == NvStatus.OK))
            {
                settings.Count = 0;
            }
            return settings;
        }

        private NvGPUCoolerSettings GetCoolerSettings()
        {
            NvGPUCoolerSettings settings = new NvGPUCoolerSettings
            {
                Version = NVAPI.GPU_COOLER_SETTINGS_VER,
                Cooler = new NvCooler[NVAPI.MAX_COOLER_PER_GPU]
            };
            if (!(NVAPI.NvAPI_GPU_GetCoolerSettings != null &&
              NVAPI.NvAPI_GPU_GetCoolerSettings(_handle, 0,
                ref settings) == NvStatus.OK))
            {
                settings.Count = 0;
            }
            return settings;
        }

        private NvFanCoolersStatus GetFanCoolersStatus()
        {
            var coolers = new NvFanCoolersStatus
            {
                Version = NVAPI.GPU_FAN_COOLERS_STATUS_VER,
                Items =
              new NvFanCoolersStatusItem[NVAPI.MAX_FAN_COOLERS_STATUS_ITEMS]
            };

            if (!(NVAPI.NvAPI_GPU_ClientFanCoolersGetStatus != null &&
               NVAPI.NvAPI_GPU_ClientFanCoolersGetStatus(_handle, ref coolers)
               == NvStatus.OK))
            {
                coolers.Count = 0;
            }
            return coolers;
        }

        private uint[] GetClocks()
        {
            NvClocks allClocks = new NvClocks
            {
                Version = NVAPI.GPU_CLOCKS_VER,
                Clock = new uint[NVAPI.MAX_CLOCKS_PER_GPU]
            };
            if (NVAPI.NvAPI_GPU_GetAllClocks != null &&
              NVAPI.NvAPI_GPU_GetAllClocks(_handle, ref allClocks) == NvStatus.OK)
            {
                return allClocks.Clock;
            }
            return null;
        }

        public override void Update()
        {
            NvGPUThermalSettings settings = GetThermalSettings();
            foreach (Sensor sensor in _temperatures)
                sensor.Value = settings.Sensor[sensor.Index].CurrentTemp;

            bool tachReadingOk = false;
            if (NVAPI.NvAPI_GPU_GetTachReading != null &&
              NVAPI.NvAPI_GPU_GetTachReading(_handle, out int fanValue) == NvStatus.OK)
            {
                _fan.Value = fanValue;
                ActivateSensor(_fan);
                tachReadingOk = true;
            }

            uint[] values = GetClocks();
            if (values != null)
            {
                _clocks[1].Value = 0.001f * values[8];
                if (values[30] != 0)
                {
                    _clocks[0].Value = 0.0005f * values[30];
                    _clocks[2].Value = 0.001f * values[30];
                }
                else
                {
                    _clocks[0].Value = 0.001f * values[0];
                    _clocks[2].Value = 0.001f * values[14];
                }
            }

            var infoEx = new NvDynamicPstatesInfoEx
            {
                Version = NVAPI.GPU_DYNAMIC_PSTATES_INFO_EX_VER,
                UtilizationDomains =
              new NvUtilizationDomainEx[NVAPI.NVAPI_MAX_GPU_UTILIZATIONS]
            };
            if (NVAPI.NvAPI_GPU_GetDynamicPstatesInfoEx != null &&
              NVAPI.NvAPI_GPU_GetDynamicPstatesInfoEx(_handle, ref infoEx) == NvStatus.OK)
            {
                for (int i = 0; i < _loads.Length; i++)
                {
                    if (infoEx.UtilizationDomains[i].Present)
                    {
                        _loads[i].Value = infoEx.UtilizationDomains[i].Percentage;
                        ActivateSensor(_loads[i]);
                    }
                }
            }
            else
            {
                var info = new NvDynamicPstatesInfo
                {
                    Version = NVAPI.GPU_DYNAMIC_PSTATES_INFO_VER,
                    UtilizationDomains =
                  new NvUtilizationDomain[NVAPI.NVAPI_MAX_GPU_UTILIZATIONS]
                };
                if (NVAPI.NvAPI_GPU_GetDynamicPstatesInfo != null &&
                  NVAPI.NvAPI_GPU_GetDynamicPstatesInfo(_handle, ref info) == NvStatus.OK)
                {
                    for (int i = 0; i < _loads.Length; i++)
                    {
                        if (info.UtilizationDomains[i].Present)
                        {
                            _loads[i].Value = info.UtilizationDomains[i].Percentage;
                            ActivateSensor(_loads[i]);
                        }
                    }
                }
            }

            NvGPUCoolerSettings coolerSettings = GetCoolerSettings();
            bool coolerSettingsOk = false;
            if (coolerSettings.Count > 0)
            {
                _control.Value = coolerSettings.Cooler[0].CurrentLevel;
                ActivateSensor(_control);
                coolerSettingsOk = true;
            }

            if (!tachReadingOk || !coolerSettingsOk)
            {
                NvFanCoolersStatus coolersStatus = GetFanCoolersStatus();
                if (coolersStatus.Count > 0)
                {
                    if (!coolerSettingsOk)
                    {
                        _control.Value = coolersStatus.Items[0].CurrentLevel;
                        ActivateSensor(_control);
                        coolerSettingsOk = true;
                    }
                    if (!tachReadingOk)
                    {
                        _fan.Value = coolersStatus.Items[0].CurrentRpm;
                        ActivateSensor(_fan);
                        tachReadingOk = true;
                    }
                }
            }

            NvDisplayDriverMemoryInfo memoryInfo = new NvDisplayDriverMemoryInfo
            {
                Version = NVAPI.DISPLAY_DRIVER_MEMORY_INFO_VER,
                Values = new uint[NVAPI.MAX_MEMORY_VALUES_PER_GPU]
            };
            if (NVAPI.NvAPI_GetDisplayDriverMemoryInfo != null && _displayHandle.HasValue &&
              NVAPI.NvAPI_GetDisplayDriverMemoryInfo(_displayHandle.Value, ref memoryInfo) ==
              NvStatus.OK)
            {
                uint totalMemory = memoryInfo.Values[0];
                uint freeMemory = memoryInfo.Values[4];
                float usedMemory = Math.Max(totalMemory - freeMemory, 0);
                _memoryFree.Value = (float)freeMemory / 1024;
                _memoryAvail.Value = (float)totalMemory / 1024;
                _memoryUsed.Value = usedMemory / 1024;
                _memoryLoad.Value = 100f * usedMemory / totalMemory;
                ActivateSensor(_memoryAvail);
                ActivateSensor(_memoryUsed);
                ActivateSensor(_memoryFree);
                ActivateSensor(_memoryLoad);
            }

            if (_power != null)
            {
                if (NVML.NvmlDeviceGetPowerUsage(_device.Value, out int powerValue)
                  == NVML.NvmlReturn.Success)
                {
                    _power.Value = powerValue * 0.001f;
                    ActivateSensor(_power);
                }
            }

            if (_pcieThroughputRx != null)
            {
                if (NVML.NvmlDeviceGetPcieThroughput(_device.Value,
                  NVML.NvmlPcieUtilCounter.RxBytes, out uint value)
                  == NVML.NvmlReturn.Success)
                {
                    _pcieThroughputRx.Value = value * (1.0f / 0x400);
                    ActivateSensor(_pcieThroughputRx);
                }
            }

            if (_pcieThroughputTx != null)
            {
                if (NVML.NvmlDeviceGetPcieThroughput(_device.Value,
                  NVML.NvmlPcieUtilCounter.TxBytes, out uint value)
                  == NVML.NvmlReturn.Success)
                {
                    _pcieThroughputTx.Value = value * (1.0f / 0x400);
                    ActivateSensor(_pcieThroughputTx);
                }
            }
        }

        public override string GetReport()
        {
            StringBuilder r = new StringBuilder();

            r.AppendLine("Nvidia GPU");
            r.AppendLine();

            r.AppendFormat("Name: {0}{1}", name, Environment.NewLine);
            r.AppendFormat("Index: {0}{1}", _adapterIndex, Environment.NewLine);

            if (_displayHandle.HasValue && NVAPI.NvAPI_GetDisplayDriverVersion != null)
            {
                NvDisplayDriverVersion driverVersion = new NvDisplayDriverVersion
                {
                    Version = NVAPI.DISPLAY_DRIVER_VERSION_VER
                };
                if (NVAPI.NvAPI_GetDisplayDriverVersion(_displayHandle.Value,
                  ref driverVersion) == NvStatus.OK)
                {
                    r.Append("Driver Version: ");
                    r.Append(driverVersion.DriverVersion / 100);
                    r.Append('.');
                    r.Append((driverVersion.DriverVersion % 100).ToString("00",
                      CultureInfo.InvariantCulture));
                    r.AppendLine();
                    r.Append("Driver Branch: ");
                    r.AppendLine(driverVersion.BuildBranch);
                }
            }
            r.AppendLine();

            if (NVAPI.NvAPI_GPU_GetPCIIdentifiers != null)
            {

                NvStatus status = NVAPI.NvAPI_GPU_GetPCIIdentifiers(_handle,
                  out uint deviceId, out uint subSystemId, out uint revisionId, out uint extDeviceId);

                if (status == NvStatus.OK)
                {
                    r.Append("DeviceID: 0x");
                    r.AppendLine(deviceId.ToString("X", CultureInfo.InvariantCulture));
                    r.Append("SubSystemID: 0x");
                    r.AppendLine(subSystemId.ToString("X", CultureInfo.InvariantCulture));
                    r.Append("RevisionID: 0x");
                    r.AppendLine(revisionId.ToString("X", CultureInfo.InvariantCulture));
                    r.Append("ExtDeviceID: 0x");
                    r.AppendLine(extDeviceId.ToString("X", CultureInfo.InvariantCulture));
                    r.AppendLine();
                }
            }

            if (NVAPI.NvAPI_GPU_GetThermalSettings != null)
            {
                NvGPUThermalSettings settings = new NvGPUThermalSettings
                {
                    Version = NVAPI.GPU_THERMAL_SETTINGS_VER,
                    Count = NVAPI.MAX_THERMAL_SENSORS_PER_GPU,
                    Sensor = new NvSensor[NVAPI.MAX_THERMAL_SENSORS_PER_GPU]
                };

                NvStatus status = NVAPI.NvAPI_GPU_GetThermalSettings(_handle,
                  (int)NvThermalTarget.ALL, ref settings);

                r.AppendLine("Thermal Settings");
                r.AppendLine();
                if (status == NvStatus.OK)
                {
                    for (int i = 0; i < settings.Count; i++)
                    {
                        r.AppendFormat(" Sensor[{0}].Controller: {1}{2}", i,
                          settings.Sensor[i].Controller, Environment.NewLine);
                        r.AppendFormat(" Sensor[{0}].DefaultMinTemp: {1}{2}", i,
                          settings.Sensor[i].DefaultMinTemp, Environment.NewLine);
                        r.AppendFormat(" Sensor[{0}].DefaultMaxTemp: {1}{2}", i,
                          settings.Sensor[i].DefaultMaxTemp, Environment.NewLine);
                        r.AppendFormat(" Sensor[{0}].CurrentTemp: {1}{2}", i,
                          settings.Sensor[i].CurrentTemp, Environment.NewLine);
                        r.AppendFormat(" Sensor[{0}].Target: {1}{2}", i,
                          settings.Sensor[i].Target, Environment.NewLine);
                    }
                }
                else
                {
                    r.Append(" Status: ");
                    r.AppendLine(status.ToString());
                }
                r.AppendLine();
            }

            if (NVAPI.NvAPI_GPU_GetAllClocks != null)
            {
                NvClocks allClocks = new NvClocks
                {
                    Version = NVAPI.GPU_CLOCKS_VER,
                    Clock = new uint[NVAPI.MAX_CLOCKS_PER_GPU]
                };
                NvStatus status = NVAPI.NvAPI_GPU_GetAllClocks(_handle, ref allClocks);

                r.AppendLine("Clocks");
                r.AppendLine();
                if (status == NvStatus.OK)
                {
                    for (int i = 0; i < allClocks.Clock.Length; i++)
                    {
                        if (allClocks.Clock[i] > 0)
                        {
                            r.AppendFormat(" Clock[{0}]: {1}{2}", i, allClocks.Clock[i],
                              Environment.NewLine);
                        }
                    }
                }
                else
                {
                    r.Append(" Status: ");
                    r.AppendLine(status.ToString());
                }
                r.AppendLine();
            }

            if (NVAPI.NvAPI_GPU_GetTachReading != null)
            {
                NvStatus status = NVAPI.NvAPI_GPU_GetTachReading(_handle, out int tachValue);

                r.AppendLine("Tachometer");
                r.AppendLine();
                if (status == NvStatus.OK)
                {
                    r.AppendFormat(" Value: {0}{1}", tachValue, Environment.NewLine);
                }
                else
                {
                    r.Append(" Status: ");
                    r.AppendLine(status.ToString());
                }
                r.AppendLine();
            }

            if (NVAPI.NvAPI_GPU_GetDynamicPstatesInfoEx != null)
            {
                var info = new NvDynamicPstatesInfoEx
                {
                    Version = NVAPI.GPU_DYNAMIC_PSTATES_INFO_EX_VER,
                    UtilizationDomains =
                  new NvUtilizationDomainEx[NVAPI.NVAPI_MAX_GPU_UTILIZATIONS]
                };
                NvStatus status = NVAPI.NvAPI_GPU_GetDynamicPstatesInfoEx(_handle, ref info);

                r.AppendLine("Utilization Domains Ex");
                r.AppendLine();
                if (status == NvStatus.OK)
                {
                    for (int i = 0; i < info.UtilizationDomains.Length; i++)
                    {
                        if (info.UtilizationDomains[i].Present)
                        {
                            r.AppendFormat(" Percentage[{0}]: {1}{2}", i,
                              info.UtilizationDomains[i].Percentage, Environment.NewLine);
                        }
                    }
                }
                else
                {
                    r.Append(" Status: ");
                    r.AppendLine(status.ToString());
                }
                r.AppendLine();
            }

            if (NVAPI.NvAPI_GPU_GetDynamicPstatesInfo != null)
            {
                var info = new NvDynamicPstatesInfo
                {
                    Version = NVAPI.GPU_DYNAMIC_PSTATES_INFO_VER,
                    UtilizationDomains =
                  new NvUtilizationDomain[NVAPI.NVAPI_MAX_GPU_UTILIZATIONS]
                };
                NvStatus status = NVAPI.NvAPI_GPU_GetDynamicPstatesInfo(_handle, ref info);

                r.AppendLine("Utilization Domains");
                r.AppendLine();
                if (status == NvStatus.OK)
                {
                    for (int i = 0; i < info.UtilizationDomains.Length; i++)
                    {
                        if (info.UtilizationDomains[i].Present)
                        {
                            r.AppendFormat(" Percentage[{0}]: {1}{2}", i,
                              info.UtilizationDomains[i].Percentage, Environment.NewLine);
                        }
                    }
                }
                else
                {
                    r.Append(" Status: ");
                    r.AppendLine(status.ToString());
                }
                r.AppendLine();
            }

            if (NVAPI.NvAPI_GPU_GetCoolerSettings != null)
            {
                NvGPUCoolerSettings settings = new NvGPUCoolerSettings
                {
                    Version = NVAPI.GPU_COOLER_SETTINGS_VER,
                    Cooler = new NvCooler[NVAPI.MAX_COOLER_PER_GPU]
                };
                NvStatus status =
                  NVAPI.NvAPI_GPU_GetCoolerSettings(_handle, 0, ref settings);

                r.AppendLine("Cooler Settings");
                r.AppendLine();
                if (status == NvStatus.OK)
                {
                    for (int i = 0; i < settings.Count; i++)
                    {
                        r.AppendFormat(" Cooler[{0}].Type: {1}{2}", i,
                          settings.Cooler[i].Type, Environment.NewLine);
                        r.AppendFormat(" Cooler[{0}].Controller: {1}{2}", i,
                          settings.Cooler[i].Controller, Environment.NewLine);
                        r.AppendFormat(" Cooler[{0}].DefaultMin: {1}{2}", i,
                          settings.Cooler[i].DefaultMin, Environment.NewLine);
                        r.AppendFormat(" Cooler[{0}].DefaultMax: {1}{2}", i,
                          settings.Cooler[i].DefaultMax, Environment.NewLine);
                        r.AppendFormat(" Cooler[{0}].CurrentMin: {1}{2}", i,
                          settings.Cooler[i].CurrentMin, Environment.NewLine);
                        r.AppendFormat(" Cooler[{0}].CurrentMax: {1}{2}", i,
                          settings.Cooler[i].CurrentMax, Environment.NewLine);
                        r.AppendFormat(" Cooler[{0}].CurrentLevel: {1}{2}", i,
                          settings.Cooler[i].CurrentLevel, Environment.NewLine);
                        r.AppendFormat(" Cooler[{0}].DefaultPolicy: {1}{2}", i,
                          settings.Cooler[i].DefaultPolicy, Environment.NewLine);
                        r.AppendFormat(" Cooler[{0}].CurrentPolicy: {1}{2}", i,
                          settings.Cooler[i].CurrentPolicy, Environment.NewLine);
                        r.AppendFormat(" Cooler[{0}].Target: {1}{2}", i,
                          settings.Cooler[i].Target, Environment.NewLine);
                        r.AppendFormat(" Cooler[{0}].ControlType: {1}{2}", i,
                          settings.Cooler[i].ControlType, Environment.NewLine);
                        r.AppendFormat(" Cooler[{0}].Active: {1}{2}", i,
                          settings.Cooler[i].Active, Environment.NewLine);
                    }
                }
                else
                {
                    r.Append(" Status: ");
                    r.AppendLine(status.ToString());
                }
                r.AppendLine();
            }

            if (NVAPI.NvAPI_GetDisplayDriverMemoryInfo != null &&
              _displayHandle.HasValue)
            {
                NvDisplayDriverMemoryInfo memoryInfo = new NvDisplayDriverMemoryInfo
                {
                    Version = NVAPI.DISPLAY_DRIVER_MEMORY_INFO_VER,
                    Values = new uint[NVAPI.MAX_MEMORY_VALUES_PER_GPU]
                };
                NvStatus status = NVAPI.NvAPI_GetDisplayDriverMemoryInfo(
                  _displayHandle.Value, ref memoryInfo);

                r.AppendLine("Memory Info");
                r.AppendLine();
                if (status == NvStatus.OK)
                {
                    for (int i = 0; i < memoryInfo.Values.Length; i++)
                    {
                        r.AppendFormat(" Value[{0}]: {1}{2}", i,
                            memoryInfo.Values[i], Environment.NewLine);
                    }
                }
                else
                {
                    r.Append(" Status: ");
                    r.AppendLine(status.ToString());
                }
                r.AppendLine();
            }

            if (NVAPI.NvAPI_GPU_ClientFanCoolersGetStatus != null)
            {
                var coolers = new NvFanCoolersStatus
                {
                    Version = NVAPI.GPU_FAN_COOLERS_STATUS_VER,
                    Items =
                  new NvFanCoolersStatusItem[NVAPI.MAX_FAN_COOLERS_STATUS_ITEMS]
                };

                NvStatus status = NVAPI.NvAPI_GPU_ClientFanCoolersGetStatus(_handle, ref coolers);

                r.AppendLine("Fan Coolers Status");
                r.AppendLine();
                if (status == NvStatus.OK)
                {
                    for (int i = 0; i < coolers.Count; i++)
                    {
                        r.AppendFormat(" Items[{0}].Type: {1}{2}", i,
                          coolers.Items[i].Type, Environment.NewLine);
                        r.AppendFormat(" Items[{0}].CurrentRpm: {1}{2}", i,
                          coolers.Items[i].CurrentRpm, Environment.NewLine);
                        r.AppendFormat(" Items[{0}].CurrentMinLevel: {1}{2}", i,
                          coolers.Items[i].CurrentMinLevel, Environment.NewLine);
                        r.AppendFormat(" Items[{0}].CurrentMaxLevel: {1}{2}", i,
                          coolers.Items[i].CurrentMaxLevel, Environment.NewLine);
                        r.AppendFormat(" Items[{0}].CurrentLevel: {1}{2}", i,
                          coolers.Items[i].CurrentLevel, Environment.NewLine);
                    }
                }
                else
                {
                    r.Append(" Status: ");
                    r.AppendLine(status.ToString());
                }
                r.AppendLine();
            }

            return r.ToString();
        }

        private void SoftwareControlValueChanged(IControl control)
        {
            NvGPUCoolerLevels coolerLevels = new NvGPUCoolerLevels
            {
                Version = NVAPI.GPU_COOLER_LEVELS_VER,
                Levels = new NvLevel[NVAPI.MAX_COOLER_PER_GPU]
            };
            coolerLevels.Levels[0].Level = (int)control.SoftwareValue;
            coolerLevels.Levels[0].Policy = 1;
            NVAPI.NvAPI_GPU_SetCoolerLevels(_handle, 0, ref coolerLevels);
        }

        private void ControlModeChanged(IControl control)
        {
            switch (control.ControlMode)
            {
                case ControlMode.Undefined:
                    return;
                case ControlMode.Default:
                    SetDefaultFanSpeed();
                    break;
                case ControlMode.Software:
                    SoftwareControlValueChanged(control);
                    break;
                default:
                    return;
            }
        }

        private void SetDefaultFanSpeed()
        {
            NvGPUCoolerLevels coolerLevels = new NvGPUCoolerLevels
            {
                Version = NVAPI.GPU_COOLER_LEVELS_VER,
                Levels = new NvLevel[NVAPI.MAX_COOLER_PER_GPU]
            };
            coolerLevels.Levels[0].Policy = 0x20;
            NVAPI.NvAPI_GPU_SetCoolerLevels(_handle, 0, ref coolerLevels);
        }

        public override void Close()
        {
            if (_fanControl != null)
            {
                _fanControl.ControlModeChanged -= ControlModeChanged;
                _fanControl.SoftwareControlValueChanged -=
                  SoftwareControlValueChanged;

                if (_fanControl.ControlMode != ControlMode.Undefined)
                    SetDefaultFanSpeed();
            }
            base.Close();
        }
    }
}
