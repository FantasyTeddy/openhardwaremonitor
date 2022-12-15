/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Globalization;
using System.Text;

namespace OpenHardwareMonitor.Hardware.ATI
{
    internal sealed class ATIGPU : Hardware
    {

        private readonly int _adapterIndex;
        private readonly Sensor _temperatureCore;
        private readonly Sensor _temperatureMemory;
        private readonly Sensor _temperatureVrmCore;
        private readonly Sensor _temperatureVrmMemory;
        private readonly Sensor _temperatureVrmMemory0;
        private readonly Sensor _temperatureVrmMemory1;
        private readonly Sensor _temperatureLiquid;
        private readonly Sensor _temperaturePlx;
        private readonly Sensor _temperatureHotSpot;
        private readonly Sensor _temperatureVrmSoc;
        private readonly Sensor _powerCore;
        private readonly Sensor _powerPpt;
        private readonly Sensor _powerSocket;
        private readonly Sensor _powerTotal;
        private readonly Sensor _powerSoc;
        private readonly Sensor _fan;
        private readonly Sensor _coreClock;
        private readonly Sensor _memoryClock;
        private readonly Sensor _socClock;
        private readonly Sensor _coreVoltage;
        private readonly Sensor _memoryVoltage;
        private readonly Sensor _socVoltage;
        private readonly Sensor _coreLoad;
        private readonly Sensor _memoryLoad;
        private readonly Sensor _controlSensor;
        private readonly Control _fanControl;

        private readonly IntPtr _context;
        private readonly int _overdriveVersion;

        public ATIGPU(string name, int adapterIndex, int busNumber,
          int deviceNumber, IntPtr context, ISettings settings)
          : base(name, new Identifier("atigpu",
            adapterIndex.ToString(CultureInfo.InvariantCulture)), settings)
        {
            _adapterIndex = adapterIndex;
            BusNumber = busNumber;
            DeviceNumber = deviceNumber;

            _context = context;

            if (ADL.ADL_Overdrive_Caps(adapterIndex, out _, out _,
              out _overdriveVersion) != ADLStatus.OK)
            {
                _overdriveVersion = -1;
            }

            _temperatureCore =
              new Sensor("GPU Core", 0, SensorType.Temperature, this, settings);
            _temperatureMemory =
              new Sensor("GPU Memory", 1, SensorType.Temperature, this, settings);
            _temperatureVrmCore =
              new Sensor("GPU VRM Core", 2, SensorType.Temperature, this, settings);
            _temperatureVrmMemory =
              new Sensor("GPU VRM Memory", 3, SensorType.Temperature, this, settings);
            _temperatureVrmMemory0 =
              new Sensor("GPU VRM Memory #1", 4, SensorType.Temperature, this, settings);
            _temperatureVrmMemory1 =
              new Sensor("GPU VRM Memory #2", 5, SensorType.Temperature, this, settings);
            _temperatureVrmSoc =
              new Sensor("GPU VRM SOC", 6, SensorType.Temperature, this, settings);
            _temperatureLiquid =
              new Sensor("GPU Liquid", 7, SensorType.Temperature, this, settings);
            _temperaturePlx =
              new Sensor("GPU PLX", 8, SensorType.Temperature, this, settings);
            _temperatureHotSpot =
              new Sensor("GPU Hot Spot", 9, SensorType.Temperature, this, settings);

            _powerTotal = new Sensor("GPU Total", 0, SensorType.Power, this, settings);
            _powerCore = new Sensor("GPU Core", 1, SensorType.Power, this, settings);
            _powerPpt = new Sensor("GPU PPT", 2, SensorType.Power, this, settings);
            _powerSocket = new Sensor("GPU Socket", 3, SensorType.Power, this, settings);
            _powerSoc = new Sensor("GPU SOC", 4, SensorType.Power, this, settings);

            _fan = new Sensor("GPU Fan", 0, SensorType.Fan, this, settings);

            _coreClock = new Sensor("GPU Core", 0, SensorType.Clock, this, settings);
            _memoryClock = new Sensor("GPU Memory", 1, SensorType.Clock, this, settings);
            _socClock = new Sensor("GPU SOC", 2, SensorType.Clock, this, settings);

            _coreVoltage = new Sensor("GPU Core", 0, SensorType.Voltage, this, settings);
            _memoryVoltage = new Sensor("GPU Memory", 1, SensorType.Voltage, this, settings);
            _socVoltage = new Sensor("GPU SOC", 2, SensorType.Voltage, this, settings);

            _coreLoad = new Sensor("GPU Core", 0, SensorType.Load, this, settings);
            _memoryLoad = new Sensor("GPU Memory", 1, SensorType.Load, this, settings);

            _controlSensor = new Sensor("GPU Fan", 0, SensorType.Control, this, settings);

            ADLFanSpeedInfo afsi = default(ADLFanSpeedInfo);
            if (ADL.ADL_Overdrive5_FanSpeedInfo_Get(adapterIndex, 0, ref afsi)
              != ADLStatus.OK)
            {
                afsi.MaxPercent = 100;
                afsi.MinPercent = 0;
            }

            _fanControl = new Control(_controlSensor, settings, afsi.MinPercent,
              afsi.MaxPercent);
            _fanControl.ControlModeChanged += ControlModeChanged;
            _fanControl.SoftwareControlValueChanged +=
              SoftwareControlValueChanged;
            ControlModeChanged(_fanControl);
            _controlSensor.Control = _fanControl;
            Update();
        }

        private void SoftwareControlValueChanged(IControl control)
        {
            if (control.ControlMode == ControlMode.Software)
            {
                ADLFanSpeedValue adlf = new ADLFanSpeedValue
                {
                    SpeedType = ADL.ADL_DL_FANCTRL_SPEED_TYPE_PERCENT,
                    Flags = ADL.ADL_DL_FANCTRL_FLAG_USER_DEFINED_SPEED,
                    FanSpeed = (int)control.SoftwareValue
                };
                ADL.ADL_Overdrive5_FanSpeed_Set(_adapterIndex, 0, ref adlf);
            }
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
            ADL.ADL_Overdrive5_FanSpeedToDefault_Set(_adapterIndex, 0);
        }

        public int BusNumber { get; }

        public int DeviceNumber { get; }


        public override HardwareType HardwareType => HardwareType.GpuAti;

        private void GetODNTemperature(ADLODNTemperatureType type,
          Sensor sensor)
        {
            if (ADL.ADL2_OverdriveN_Temperature_Get(_context, _adapterIndex,
              type, out int temperature) == ADLStatus.OK)
            {
                sensor.Value = 0.001f * temperature;
                ActivateSensor(sensor);
            }
            else
            {
                sensor.Value = null;
            }
        }

        private void GetOD6Power(ADLODNCurrentPowerType type, Sensor sensor)
        {
            if (ADL.ADL2_Overdrive6_CurrentPower_Get(_context, _adapterIndex, type,
              out int power) == ADLStatus.OK)
            {
                sensor.Value = power * (1.0f / 0xFF);
                ActivateSensor(sensor);
            }
            else
            {
                sensor.Value = null;
            }

        }

        public override string GetReport()
        {
            var r = new StringBuilder();

            r.AppendLine("AMD GPU");
            r.AppendLine();

            r.Append("AdapterIndex: ");
            r.AppendLine(_adapterIndex.ToString(CultureInfo.InvariantCulture));
            r.AppendLine();

            r.AppendLine("Overdrive Caps");
            r.AppendLine();
            try
            {
                ADLStatus status = ADL.ADL_Overdrive_Caps(_adapterIndex,
                  out int supported, out int enabled, out int version);
                r.Append(" Status: ");
                r.AppendLine(status.ToString());
                r.Append(" Supported: ");
                r.AppendLine(supported.ToString(CultureInfo.InvariantCulture));
                r.Append(" Enabled: ");
                r.AppendLine(enabled.ToString(CultureInfo.InvariantCulture));
                r.Append(" Version: ");
                r.AppendLine(version.ToString(CultureInfo.InvariantCulture));
            }
            catch (Exception e)
            {
                r.AppendLine(" Status: " + e.Message);
            }
            r.AppendLine();

            r.AppendLine("Overdrive5 Parameters");
            r.AppendLine();
            try
            {
                ADLStatus status = ADL.ADL_Overdrive5_ODParameters_Get(
                  _adapterIndex, out ADLODParameters p);
                r.Append(" Status: ");
                r.AppendLine(status.ToString());
                r.AppendFormat(" NumberOfPerformanceLevels: {0}{1}",
                  p.NumberOfPerformanceLevels, Environment.NewLine);
                r.AppendFormat(" ActivityReportingSupported: {0}{1}",
                  p.ActivityReportingSupported, Environment.NewLine);
                r.AppendFormat(" DiscretePerformanceLevels: {0}{1}",
                  p.DiscretePerformanceLevels, Environment.NewLine);
                r.AppendFormat(" EngineClock.Min: {0}{1}",
                  p.EngineClock.Min, Environment.NewLine);
                r.AppendFormat(" EngineClock.Max: {0}{1}",
                  p.EngineClock.Max, Environment.NewLine);
                r.AppendFormat(" EngineClock.Step: {0}{1}",
                  p.EngineClock.Step, Environment.NewLine);
                r.AppendFormat(" MemoryClock.Min: {0}{1}",
                  p.MemoryClock.Min, Environment.NewLine);
                r.AppendFormat(" MemoryClock.Max: {0}{1}",
                  p.MemoryClock.Max, Environment.NewLine);
                r.AppendFormat(" MemoryClock.Step: {0}{1}",
                  p.MemoryClock.Step, Environment.NewLine);
                r.AppendFormat(" Vddc.Min: {0}{1}",
                  p.Vddc.Min, Environment.NewLine);
                r.AppendFormat(" Vddc.Max: {0}{1}",
                  p.Vddc.Max, Environment.NewLine);
                r.AppendFormat(" Vddc.Step: {0}{1}",
                  p.Vddc.Step, Environment.NewLine);
            }
            catch (Exception e)
            {
                r.AppendLine(" Status: " + e.Message);
            }
            r.AppendLine();

            r.AppendLine("Overdrive5 Temperature");
            r.AppendLine();
            try
            {
                var adlt = default(ADLTemperature);
                ADLStatus status = ADL.ADL_Overdrive5_Temperature_Get(_adapterIndex, 0,
                  ref adlt);
                r.Append(" Status: ");
                r.AppendLine(status.ToString());
                r.AppendFormat(" Value: {0}{1}",
                  0.001f * adlt.Temperature, Environment.NewLine);
            }
            catch (Exception e)
            {
                r.AppendLine(" Status: " + e.Message);
            }
            r.AppendLine();

            r.AppendLine("Overdrive5 FanSpeed");
            r.AppendLine();
            try
            {
                var adlf = new ADLFanSpeedValue
                {
                    SpeedType = ADL.ADL_DL_FANCTRL_SPEED_TYPE_RPM
                };
                ADLStatus status = ADL.ADL_Overdrive5_FanSpeed_Get(_adapterIndex, 0, ref adlf);
                r.Append(" Status RPM: ");
                r.AppendLine(status.ToString());
                r.AppendFormat(" Value RPM: {0}{1}",
                  adlf.FanSpeed, Environment.NewLine);
                adlf.SpeedType = ADL.ADL_DL_FANCTRL_SPEED_TYPE_PERCENT;
                status = ADL.ADL_Overdrive5_FanSpeed_Get(_adapterIndex, 0, ref adlf);
                r.Append(" Status Percent: ");
                r.AppendLine(status.ToString());
                r.AppendFormat(" Value Percent: {0}{1}",
                  adlf.FanSpeed, Environment.NewLine);
            }
            catch (Exception e)
            {
                r.AppendLine(" Status: " + e.Message);
            }
            r.AppendLine();

            r.AppendLine("Overdrive5 CurrentActivity");
            r.AppendLine();
            try
            {
                var adlp = default(ADLPMActivity);
                ADLStatus status = ADL.ADL_Overdrive5_CurrentActivity_Get(_adapterIndex,
                  ref adlp);
                r.Append(" Status: ");
                r.AppendLine(status.ToString());
                r.AppendFormat(" EngineClock: {0}{1}",
                  0.01f * adlp.EngineClock, Environment.NewLine);
                r.AppendFormat(" MemoryClock: {0}{1}",
                  0.01f * adlp.MemoryClock, Environment.NewLine);
                r.AppendFormat(" Vddc: {0}{1}",
                  0.001f * adlp.Vddc, Environment.NewLine);
                r.AppendFormat(" ActivityPercent: {0}{1}",
                  adlp.ActivityPercent, Environment.NewLine);
                r.AppendFormat(" CurrentPerformanceLevel: {0}{1}",
                  adlp.CurrentPerformanceLevel, Environment.NewLine);
                r.AppendFormat(" CurrentBusSpeed: {0}{1}",
                  adlp.CurrentBusSpeed, Environment.NewLine);
                r.AppendFormat(" CurrentBusLanes: {0}{1}",
                  adlp.CurrentBusLanes, Environment.NewLine);
                r.AppendFormat(" MaximumBusLanes: {0}{1}",
                  adlp.MaximumBusLanes, Environment.NewLine);
            }
            catch (Exception e)
            {
                r.AppendLine(" Status: " + e.Message);
            }
            r.AppendLine();

            if (_context != IntPtr.Zero)
            {
                r.AppendLine("Overdrive6 CurrentPower");
                r.AppendLine();
                try
                {
                    for (int i = 0; i < 4; i++)
                    {
                        string pt = ((ADLODNCurrentPowerType)i).ToString();
                        ADLStatus status = ADL.ADL2_Overdrive6_CurrentPower_Get(
                          _context, _adapterIndex, (ADLODNCurrentPowerType)i,
                          out int power);
                        if (status == ADLStatus.OK)
                        {
                            r.AppendFormat(" Power[{0}].Value: {1}{2}", pt,
                              power * (1.0f / 0xFF), Environment.NewLine);
                        }
                        else
                        {
                            r.AppendFormat(" Power[{0}].Status: {1}{2}", pt,
                              status.ToString(), Environment.NewLine);
                        }
                    }
                }
                catch (EntryPointNotFoundException)
                {
                    r.AppendLine(" Status: Entry point not found");
                }
                catch (Exception e)
                {
                    r.AppendLine(" Status: " + e.Message);
                }
                r.AppendLine();
            }

            if (_context != IntPtr.Zero)
            {
                r.AppendLine("OverdriveN Temperature");
                r.AppendLine();
                try
                {
                    for (int i = 1; i < 8; i++)
                    {
                        string tt = ((ADLODNTemperatureType)i).ToString();
                        ADLStatus status = ADL.ADL2_OverdriveN_Temperature_Get(
                          _context, _adapterIndex, (ADLODNTemperatureType)i,
                          out int temperature);
                        if (status == ADLStatus.OK)
                        {
                            r.AppendFormat(" Temperature[{0}].Value: {1}{2}", tt,
                              0.001f * temperature, Environment.NewLine);
                        }
                        else
                        {
                            r.AppendFormat(" Temperature[{0}].Status: {1}{2}", tt,
                              status.ToString(), Environment.NewLine);
                        }
                    }
                }
                catch (EntryPointNotFoundException)
                {
                    r.AppendLine(" Status: Entry point not found");
                }
                catch (Exception e)
                {
                    r.AppendLine(" Status: " + e.Message);
                }
                r.AppendLine();
            }

            if (_context != IntPtr.Zero)
            {
                r.AppendLine("OverdriveN Performance Status");
                r.AppendLine();
                try
                {
                    ADLStatus status = ADL.ADL2_OverdriveN_PerformanceStatus_Get(_context,
                      _adapterIndex, out ADLODNPerformanceStatus ps);
                    r.Append(" Status: ");
                    r.AppendLine(status.ToString());
                    r.AppendFormat(" CoreClock: {0}{1}",
                      ps.CoreClock, Environment.NewLine);
                    r.AppendFormat(" MemoryClock: {0}{1}",
                      ps.MemoryClock, Environment.NewLine);
                    r.AppendFormat(" DCEFClock: {0}{1}",
                      ps.DCEFClock, Environment.NewLine);
                    r.AppendFormat(" GFXClock: {0}{1}",
                      ps.GFXClock, Environment.NewLine);
                    r.AppendFormat(" UVDClock: {0}{1}",
                      ps.UVDClock, Environment.NewLine);
                    r.AppendFormat(" VCEClock: {0}{1}",
                      ps.VCEClock, Environment.NewLine);
                    r.AppendFormat(" GPUActivityPercent: {0}{1}",
                      ps.GPUActivityPercent, Environment.NewLine);
                    r.AppendFormat(" CurrentCorePerformanceLevel: {0}{1}",
                      ps.CurrentCorePerformanceLevel, Environment.NewLine);
                    r.AppendFormat(" CurrentMemoryPerformanceLevel: {0}{1}",
                      ps.CurrentMemoryPerformanceLevel, Environment.NewLine);
                    r.AppendFormat(" CurrentDCEFPerformanceLevel: {0}{1}",
                      ps.CurrentDCEFPerformanceLevel, Environment.NewLine);
                    r.AppendFormat(" CurrentGFXPerformanceLevel: {0}{1}",
                      ps.CurrentGFXPerformanceLevel, Environment.NewLine);
                    r.AppendFormat(" UVDPerformanceLevel: {0}{1}",
                      ps.UVDPerformanceLevel, Environment.NewLine);
                    r.AppendFormat(" VCEPerformanceLevel: {0}{1}",
                      ps.VCEPerformanceLevel, Environment.NewLine);
                    r.AppendFormat(" CurrentBusSpeed: {0}{1}",
                      ps.CurrentBusSpeed, Environment.NewLine);
                    r.AppendFormat(" CurrentBusLanes: {0}{1}",
                      ps.CurrentBusLanes, Environment.NewLine);
                    r.AppendFormat(" MaximumBusLanes: {0}{1}",
                      ps.MaximumBusLanes, Environment.NewLine);
                    r.AppendFormat(" VDDC: {0}{1}",
                      ps.VDDC, Environment.NewLine);
                    r.AppendFormat(" VDDCI: {0}{1}",
                      ps.VDDCI, Environment.NewLine);
                }
                catch (EntryPointNotFoundException)
                {
                    r.AppendLine(" Status: Entry point not found");
                }
                catch (Exception e)
                {
                    r.AppendLine(" Status: " + e.Message);
                }
                r.AppendLine();
            }

            if (_context != IntPtr.Zero)
            {
                r.AppendLine("Performance Metrics");
                r.AppendLine();
                try
                {
                    ADLStatus status = ADL.ADL2_New_QueryPMLogData_Get(_context, _adapterIndex,
                      out ADLPMLogDataOutput data);
                    if (status == ADLStatus.OK)
                    {
                        for (int i = 0; i < data.Sensors.Length; i++)
                        {
                            if (data.Sensors[i].Supported)
                            {
                                string st = ((ADLSensorType)i).ToString();
                                r.AppendFormat(" Sensor[{0}].Value: {1}{2}", st,
                                  data.Sensors[i].Value, Environment.NewLine);
                            }
                        }
                    }
                    else
                    {
                        r.Append(" Status: ");
                        r.AppendLine(status.ToString());
                    }
                }
                catch (EntryPointNotFoundException)
                {
                    r.AppendLine(" Status: Entry point not found");
                }
                catch (Exception e)
                {
                    r.AppendLine(" Status: " + e.Message);
                }

                r.AppendLine();
            }

            return r.ToString();
        }

        private void GetPMLog(ADLPMLogDataOutput data,
          ADLSensorType sensorType, Sensor sensor, float factor = 1.0f)
        {
            int i = (int)sensorType;
            if (i < data.Sensors.Length && data.Sensors[i].Supported)
            {
                sensor.Value = data.Sensors[i].Value * factor;
                ActivateSensor(sensor);
            }
        }

        public override void Update()
        {
            if (_context != IntPtr.Zero && _overdriveVersion >= 8 &&
              ADL.ADL2_New_QueryPMLogData_Get(_context, _adapterIndex,
              out ADLPMLogDataOutput data) == ADLStatus.OK)
            {
                GetPMLog(data, ADLSensorType.TEMPERATURE_EDGE, _temperatureCore);
                GetPMLog(data, ADLSensorType.TEMPERATURE_MEM, _temperatureMemory);
                GetPMLog(data, ADLSensorType.TEMPERATURE_VRVDDC, _temperatureVrmCore);
                GetPMLog(data, ADLSensorType.TEMPERATURE_VRMVDD, _temperatureVrmMemory);
                GetPMLog(data, ADLSensorType.TEMPERATURE_VRMVDD0, _temperatureVrmMemory0);
                GetPMLog(data, ADLSensorType.TEMPERATURE_VRMVDD1, _temperatureVrmMemory1);
                GetPMLog(data, ADLSensorType.TEMPERATURE_VRSOC, _temperatureVrmSoc);
                GetPMLog(data, ADLSensorType.TEMPERATURE_LIQUID, _temperatureLiquid);
                GetPMLog(data, ADLSensorType.TEMPERATURE_PLX, _temperaturePlx);
                GetPMLog(data, ADLSensorType.TEMPERATURE_HOTSPOT, _temperatureHotSpot);
                GetPMLog(data, ADLSensorType.GFX_POWER, _powerCore);
                GetPMLog(data, ADLSensorType.ASIC_POWER, _powerTotal);
                GetPMLog(data, ADLSensorType.SOC_POWER, _powerSoc);
                GetPMLog(data, ADLSensorType.FAN_RPM, _fan);
                GetPMLog(data, ADLSensorType.CLK_GFXCLK, _coreClock);
                GetPMLog(data, ADLSensorType.CLK_MEMCLK, _memoryClock);
                GetPMLog(data, ADLSensorType.CLK_SOCCLK, _socClock);
                GetPMLog(data, ADLSensorType.GFX_VOLTAGE, _coreVoltage, 0.001f);
                GetPMLog(data, ADLSensorType.MEM_VOLTAGE, _memoryVoltage, 0.001f);
                GetPMLog(data, ADLSensorType.SOC_VOLTAGE, _socVoltage, 0.001f);
                GetPMLog(data, ADLSensorType.INFO_ACTIVITY_GFX, _coreLoad);
                GetPMLog(data, ADLSensorType.INFO_ACTIVITY_MEM, _memoryLoad);
                GetPMLog(data, ADLSensorType.FAN_PERCENTAGE, _controlSensor);
            }
            else
            {
                if (_context != IntPtr.Zero && _overdriveVersion >= 7)
                {
                    GetODNTemperature(ADLODNTemperatureType.CORE, _temperatureCore);
                    GetODNTemperature(ADLODNTemperatureType.MEMORY, _temperatureMemory);
                    GetODNTemperature(ADLODNTemperatureType.VRM_CORE, _temperatureVrmCore);
                    GetODNTemperature(ADLODNTemperatureType.VRM_MEMORY, _temperatureVrmMemory);
                    GetODNTemperature(ADLODNTemperatureType.LIQUID, _temperatureLiquid);
                    GetODNTemperature(ADLODNTemperatureType.PLX, _temperaturePlx);
                    GetODNTemperature(ADLODNTemperatureType.HOTSPOT, _temperatureHotSpot);
                }
                else
                {
                    ADLTemperature adlt = default(ADLTemperature);
                    if (ADL.ADL_Overdrive5_Temperature_Get(_adapterIndex, 0, ref adlt)
                      == ADLStatus.OK)
                    {
                        _temperatureCore.Value = 0.001f * adlt.Temperature;
                        ActivateSensor(_temperatureCore);
                    }
                    else
                    {
                        _temperatureCore.Value = null;
                    }
                }

                if (_context != IntPtr.Zero && _overdriveVersion >= 6)
                {
                    GetOD6Power(ADLODNCurrentPowerType.TOTAL_POWER, _powerTotal);
                    GetOD6Power(ADLODNCurrentPowerType.CHIP_POWER, _powerCore);
                    GetOD6Power(ADLODNCurrentPowerType.PPT_POWER, _powerPpt);
                    GetOD6Power(ADLODNCurrentPowerType.SOCKET_POWER, _powerSocket);
                }

                ADLFanSpeedValue adlf = new ADLFanSpeedValue
                {
                    SpeedType = ADL.ADL_DL_FANCTRL_SPEED_TYPE_RPM
                };
                if (ADL.ADL_Overdrive5_FanSpeed_Get(_adapterIndex, 0, ref adlf)
                  == ADLStatus.OK)
                {
                    _fan.Value = adlf.FanSpeed;
                    ActivateSensor(_fan);
                }
                else
                {
                    _fan.Value = null;
                }

                adlf = new ADLFanSpeedValue
                {
                    SpeedType = ADL.ADL_DL_FANCTRL_SPEED_TYPE_PERCENT
                };
                if (ADL.ADL_Overdrive5_FanSpeed_Get(_adapterIndex, 0, ref adlf)
                  == ADLStatus.OK)
                {
                    _controlSensor.Value = adlf.FanSpeed;
                    ActivateSensor(_controlSensor);
                }
                else
                {
                    _controlSensor.Value = null;
                }

                ADLPMActivity adlp = default(ADLPMActivity);
                if (ADL.ADL_Overdrive5_CurrentActivity_Get(_adapterIndex, ref adlp)
                  == ADLStatus.OK)
                {
                    if (adlp.EngineClock > 0)
                    {
                        _coreClock.Value = 0.01f * adlp.EngineClock;
                        ActivateSensor(_coreClock);
                    }
                    else
                    {
                        _coreClock.Value = null;
                    }

                    if (adlp.MemoryClock > 0)
                    {
                        _memoryClock.Value = 0.01f * adlp.MemoryClock;
                        ActivateSensor(_memoryClock);
                    }
                    else
                    {
                        _memoryClock.Value = null;
                    }

                    if (adlp.Vddc > 0)
                    {
                        _coreVoltage.Value = 0.001f * adlp.Vddc;
                        ActivateSensor(_coreVoltage);
                    }
                    else
                    {
                        _coreVoltage.Value = null;
                    }

                    if (adlp.ActivityPercent >= 0 && adlp.ActivityPercent <= 100)
                    {
                        _coreLoad.Value = adlp.ActivityPercent;
                        ActivateSensor(_coreLoad);
                    }
                    else
                    {
                        _coreLoad.Value = null;
                    }
                }
                else
                {
                    _coreClock.Value = null;
                    _memoryClock.Value = null;
                    _coreVoltage.Value = null;
                    _coreLoad.Value = null;
                }
            }
        }

        public override void Close()
        {
            _fanControl.ControlModeChanged -= ControlModeChanged;
            _fanControl.SoftwareControlValueChanged -=
              SoftwareControlValueChanged;

            if (_fanControl.ControlMode != ControlMode.Undefined)
                SetDefaultFanSpeed();
            base.Close();
        }
    }
}
