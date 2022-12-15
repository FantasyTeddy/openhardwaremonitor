﻿/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace OpenHardwareMonitor.Hardware.CPU
{

    internal sealed class AMD10CPU : AMDCPU, IDisposable
    {

        private readonly Sensor _coreTemperature;
        private readonly Sensor[] _coreClocks;
        private readonly Sensor _busClock;

        private const uint PERF_CTL_0 = 0xC0010000;
        private const uint PERF_CTR_0 = 0xC0010004;
        private const uint HWCR = 0xC0010015;
        private const uint P_STATE_0 = 0xC0010064;
        private const uint COFVID_STATUS = 0xC0010071;

        private const byte MISCELLANEOUS_CONTROL_FUNCTION = 3;
        private const ushort FAMILY_10H_MISCELLANEOUS_CONTROL_DEVICE_ID = 0x1203;
        private const ushort FAMILY_11H_MISCELLANEOUS_CONTROL_DEVICE_ID = 0x1303;
        private const ushort FAMILY_12H_MISCELLANEOUS_CONTROL_DEVICE_ID = 0x1703;
        private const ushort FAMILY_14H_MISCELLANEOUS_CONTROL_DEVICE_ID = 0x1703;
        private const ushort FAMILY_15H_MODEL_00_MISC_CONTROL_DEVICE_ID = 0x1603;
        private const ushort FAMILY_15H_MODEL_10_MISC_CONTROL_DEVICE_ID = 0x1403;
        private const ushort FAMILY_15H_MODEL_30_MISC_CONTROL_DEVICE_ID = 0x141D;
        private const ushort FAMILY_15H_MODEL_60_MISC_CONTROL_DEVICE_ID = 0x1573;
        private const ushort FAMILY_15H_MODEL_70_MISC_CONTROL_DEVICE_ID = 0x15B3;
        private const ushort FAMILY_16H_MODEL_00_MISC_CONTROL_DEVICE_ID = 0x1533;
        private const ushort FAMILY_16H_MODEL_30_MISC_CONTROL_DEVICE_ID = 0x1583;

        private const uint REPORTED_TEMPERATURE_CONTROL_REGISTER = 0xA4;
        private const uint CLOCK_POWER_TIMING_CONTROL_0_REGISTER = 0xD4;
        private const uint SMU_REPORTED_TEMP_CONTROL_REGISTER = 0xD8200CA4;

        private readonly bool _hasSmuTemperatureRegister;

        private readonly uint _miscellaneousControlAddress;
        private readonly ushort _miscellaneousControlDeviceId;

        private readonly FileStream _temperatureStream;

        private readonly double _timeStampCounterMultiplier;
        private readonly bool _corePerformanceBoostSupport;

        public AMD10CPU(int processorIndex, CPUID[][] cpuid, ISettings settings)
          : base(processorIndex, cpuid, settings)
        {
            // AMD family 1Xh processors support only one temperature sensor
            _coreTemperature = new Sensor(
              "Core" + (coreCount > 1 ? " #1 - #" + coreCount : string.Empty), 0,
              SensorType.Temperature, this, new[] {
            new ParameterDescription("Offset [°C]", "Temperature offset.", 0)
                }, settings);

            switch (family)
            {
                case 0x10:
                    _miscellaneousControlDeviceId =
                    FAMILY_10H_MISCELLANEOUS_CONTROL_DEVICE_ID; break;
                case 0x11:
                    _miscellaneousControlDeviceId =
                    FAMILY_11H_MISCELLANEOUS_CONTROL_DEVICE_ID; break;
                case 0x12:
                    _miscellaneousControlDeviceId =
                    FAMILY_12H_MISCELLANEOUS_CONTROL_DEVICE_ID; break;
                case 0x14:
                    _miscellaneousControlDeviceId =
                    FAMILY_14H_MISCELLANEOUS_CONTROL_DEVICE_ID; break;
                case 0x15:
                    switch (model & 0xF0)
                    {
                        case 0x00:
                            _miscellaneousControlDeviceId =
                            FAMILY_15H_MODEL_00_MISC_CONTROL_DEVICE_ID; break;
                        case 0x10:
                            _miscellaneousControlDeviceId =
                            FAMILY_15H_MODEL_10_MISC_CONTROL_DEVICE_ID; break;
                        case 0x30:
                            _miscellaneousControlDeviceId =
                            FAMILY_15H_MODEL_30_MISC_CONTROL_DEVICE_ID; break;
                        case 0x60:
                            _miscellaneousControlDeviceId =
                            FAMILY_15H_MODEL_60_MISC_CONTROL_DEVICE_ID;
                            _hasSmuTemperatureRegister = true;
                            break;
                        case 0x70:
                            _miscellaneousControlDeviceId =
                            FAMILY_15H_MODEL_70_MISC_CONTROL_DEVICE_ID;
                            _hasSmuTemperatureRegister = true;
                            break;
                        default: _miscellaneousControlDeviceId = 0; break;
                    }
                    break;
                case 0x16:
                    switch (model & 0xF0)
                    {
                        case 0x00:
                            _miscellaneousControlDeviceId =
                            FAMILY_16H_MODEL_00_MISC_CONTROL_DEVICE_ID; break;
                        case 0x30:
                            _miscellaneousControlDeviceId =
                            FAMILY_16H_MODEL_30_MISC_CONTROL_DEVICE_ID; break;
                        default: _miscellaneousControlDeviceId = 0; break;
                    }
                    break;
                default: _miscellaneousControlDeviceId = 0; break;
            }

            // get the pci address for the Miscellaneous Control registers
            _miscellaneousControlAddress = GetPciAddress(
              MISCELLANEOUS_CONTROL_FUNCTION, _miscellaneousControlDeviceId);

            _busClock = new Sensor("Bus Speed", 0, SensorType.Clock, this, settings);
            _coreClocks = new Sensor[coreCount];
            for (int i = 0; i < _coreClocks.Length; i++)
            {
                _coreClocks[i] = new Sensor(CoreString(i), i + 1, SensorType.Clock,
                  this, settings);
                if (HasTimeStampCounter)
                    ActivateSensor(_coreClocks[i]);
            }

            _corePerformanceBoostSupport = (cpuid[0][0].ExtData[7, 3] & (1 << 9)) > 0;

            // set affinity to the first thread for all frequency estimations
            GroupAffinity previousAffinity = ThreadAffinity.Set(cpuid[0][0].Affinity);

            // disable core performance boost
            Ring0.Rdmsr(HWCR, out uint hwcrEax, out uint hwcrEdx);
            if (_corePerformanceBoostSupport)
                Ring0.Wrmsr(HWCR, hwcrEax | (1 << 25), hwcrEdx);

            Ring0.Rdmsr(PERF_CTL_0, out uint ctlEax, out uint ctlEdx);
            Ring0.Rdmsr(PERF_CTR_0, out uint ctrEax, out uint ctrEdx);

            _timeStampCounterMultiplier = estimateTimeStampCounterMultiplier();

            // restore the performance counter registers
            Ring0.Wrmsr(PERF_CTL_0, ctlEax, ctlEdx);
            Ring0.Wrmsr(PERF_CTR_0, ctrEax, ctrEdx);

            // restore core performance boost
            if (_corePerformanceBoostSupport)
                Ring0.Wrmsr(HWCR, hwcrEax, hwcrEdx);

            // restore the thread affinity.
            ThreadAffinity.Set(previousAffinity);

            // the file reader for lm-sensors support on Linux
            _temperatureStream = null;
            if (OperatingSystem.IsUnix)
            {
                string[] devicePaths = Directory.GetDirectories("/sys/class/hwmon/");
                foreach (string path in devicePaths)
                {
                    string name = null;
                    try
                    {
                        using (StreamReader reader = new StreamReader(path + "/device/name"))
                            name = reader.ReadLine();
                    }
                    catch (IOException) { }
                    switch (name)
                    {
                        case "k10temp":
                            _temperatureStream = new FileStream(path + "/device/temp1_input",
                              FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                            break;
                    }
                }
            }

            Update();
        }

        private double estimateTimeStampCounterMultiplier()
        {
            // preload the function
            estimateTimeStampCounterMultiplier(0);
            estimateTimeStampCounterMultiplier(0);

            // estimate the multiplier
            List<double> estimate = new List<double>(3);
            for (int i = 0; i < 3; i++)
                estimate.Add(estimateTimeStampCounterMultiplier(0.025));
            estimate.Sort();
            return estimate[1];
        }

        private double estimateTimeStampCounterMultiplier(double timeWindow)
        {

            // select event "076h CPU Clocks not Halted" and enable the counter
            Ring0.Wrmsr(PERF_CTL_0,
              (1 << 22) | // enable performance counter
              (1 << 17) | // count events in user mode
              (1 << 16) | // count events in operating-system mode
              0x76, 0x00000000);

            // set the counter to 0
            Ring0.Wrmsr(PERF_CTR_0, 0, 0);

            long ticks = (long)(timeWindow * Stopwatch.Frequency);

            long timeBegin = Stopwatch.GetTimestamp() +
              (long)Math.Ceiling(0.001 * ticks);
            long timeEnd = timeBegin + ticks;
            while (Stopwatch.GetTimestamp() < timeBegin) { }
            Ring0.Rdmsr(PERF_CTR_0, out uint lsbBegin, out uint msbBegin);

            while (Stopwatch.GetTimestamp() < timeEnd) { }
            Ring0.Rdmsr(PERF_CTR_0, out uint lsbEnd, out uint msbEnd);
            Ring0.Rdmsr(COFVID_STATUS, out uint eax, out uint edx);
            double coreMultiplier = GetCoreMultiplier(eax);

            ulong countBegin = ((ulong)msbBegin << 32) | lsbBegin;
            ulong countEnd = ((ulong)msbEnd << 32) | lsbEnd;

            double coreFrequency = 1e-6 *
              ((double)(countEnd - countBegin) * Stopwatch.Frequency) /
              (timeEnd - timeBegin);

            double busFrequency = coreFrequency / coreMultiplier;

            return 0.25 * Math.Round(4 * TimeStampCounterFrequency / busFrequency);
        }

        protected override uint[] GetMSRs()
        {
            return new uint[] { PERF_CTL_0, PERF_CTR_0, HWCR, P_STATE_0,
        COFVID_STATUS };
        }

        public override string GetReport()
        {
            StringBuilder r = new StringBuilder();
            r.Append(base.GetReport());

            r.Append("Miscellaneous Control Address: 0x");
            r.AppendLine(_miscellaneousControlAddress.ToString("X",
              CultureInfo.InvariantCulture));
            r.Append("Time Stamp Counter Multiplier: ");
            r.AppendLine(_timeStampCounterMultiplier.ToString(
              CultureInfo.InvariantCulture));
            if (family == 0x14)
            {
                Ring0.ReadPciConfig(_miscellaneousControlAddress,
                  CLOCK_POWER_TIMING_CONTROL_0_REGISTER, out uint value);
                r.Append("PCI Register D18F3xD4: ");
                r.AppendLine(value.ToString("X8", CultureInfo.InvariantCulture));
            }
            r.AppendLine();

            return r.ToString();
        }

        private double GetCoreMultiplier(uint cofvidEax)
        {
            switch (family)
            {
                case 0x10:
                case 0x11:
                case 0x15:
                case 0x16:
                    {
                        // 8:6 CpuDid: current core divisor ID
                        // 5:0 CpuFid: current core frequency ID
                        uint cpuDid = (cofvidEax >> 6) & 7;
                        uint cpuFid = cofvidEax & 0x1F;
                        return 0.5 * (cpuFid + 0x10) / (1 << (int)cpuDid);
                    }
                case 0x12:
                    {
                        // 8:4 CpuFid: current CPU core frequency ID
                        // 3:0 CpuDid: current CPU core divisor ID
                        uint cpuFid = (cofvidEax >> 4) & 0x1F;
                        uint cpuDid = cofvidEax & 0xF;
                        double divisor;
                        switch (cpuDid)
                        {
                            case 0: divisor = 1; break;
                            case 1: divisor = 1.5; break;
                            case 2: divisor = 2; break;
                            case 3: divisor = 3; break;
                            case 4: divisor = 4; break;
                            case 5: divisor = 6; break;
                            case 6: divisor = 8; break;
                            case 7: divisor = 12; break;
                            case 8: divisor = 16; break;
                            default: divisor = 1; break;
                        }
                        return (cpuFid + 0x10) / divisor;
                    }
                case 0x14:
                    {
                        // 8:4: current CPU core divisor ID most significant digit
                        // 3:0: current CPU core divisor ID least significant digit
                        uint divisorIdMSD = (cofvidEax >> 4) & 0x1F;
                        uint divisorIdLSD = cofvidEax & 0xF;
                        Ring0.ReadPciConfig(_miscellaneousControlAddress,
                          CLOCK_POWER_TIMING_CONTROL_0_REGISTER, out uint value);
                        uint frequencyId = value & 0x1F;
                        return (frequencyId + 0x10) /
                          (divisorIdMSD + divisorIdLSD * 0.25 + 1);
                    }
                default:
                    return 1;
            }
        }

        private static string ReadFirstLine(Stream stream)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                stream.Seek(0, SeekOrigin.Begin);
                int b = stream.ReadByte();
                while (b != -1 && b != 10)
                {
                    sb.Append((char)b);
                    b = stream.ReadByte();
                }
            }
            catch { }
            return sb.ToString();
        }

        private static bool ReadSmuRegister(uint address, out uint value)
        {
            if (Ring0.WaitPciBusMutex(10))
            {

                if (!Ring0.WritePciConfig(0, 0xB8, address))
                {
                    value = 0;

                    Ring0.ReleasePciBusMutex();
                    return false;
                }
                bool result = Ring0.ReadPciConfig(0, 0xBC, out value);

                Ring0.ReleasePciBusMutex();
                return result;
            }
            else
            {
                value = 0;
                return false;
            }
        }

        public override void Update()
        {
            base.Update();

            if (_temperatureStream == null)
            {
                if (_miscellaneousControlAddress != Ring0.InvalidPciAddress)
                {
                    uint value;
                    bool valueValid;
                    if (_hasSmuTemperatureRegister)
                    {
                        valueValid =
                          ReadSmuRegister(SMU_REPORTED_TEMP_CONTROL_REGISTER, out value);
                    }
                    else
                    {
                        valueValid = Ring0.ReadPciConfig(_miscellaneousControlAddress,
                          REPORTED_TEMPERATURE_CONTROL_REGISTER, out value);
                    }
                    if (valueValid)
                    {
                        if ((family == 0x15 || family == 0x16) && (value & 0x30000) == 0x30000)
                        {
                            _coreTemperature.Value = ((value >> 21) & 0x7FF) * 0.125f +
                              _coreTemperature.Parameters[0].Value - 49;
                        }
                        else
                        {
                            _coreTemperature.Value = ((value >> 21) & 0x7FF) * 0.125f +
                              _coreTemperature.Parameters[0].Value;
                        }
                        ActivateSensor(_coreTemperature);
                    }
                    else
                    {
                        DeactivateSensor(_coreTemperature);
                    }
                }
            }
            else
            {
                string s = ReadFirstLine(_temperatureStream);
                try
                {
                    _coreTemperature.Value = 0.001f *
                      long.Parse(s, CultureInfo.InvariantCulture);
                    ActivateSensor(_coreTemperature);
                }
                catch
                {
                    DeactivateSensor(_coreTemperature);
                }
            }

            if (HasTimeStampCounter)
            {
                double newBusClock = 0;

                for (int i = 0; i < _coreClocks.Length; i++)
                {
                    Thread.Sleep(1);

                    if (Ring0.RdmsrTx(COFVID_STATUS, out uint curEax, out uint curEdx,
                      cpuid[i][0].Affinity))
                    {
                        double multiplier;
                        multiplier = GetCoreMultiplier(curEax);

                        _coreClocks[i].Value =
                          (float)(multiplier * TimeStampCounterFrequency /
                          _timeStampCounterMultiplier);
                        newBusClock =
                          (float)(TimeStampCounterFrequency / _timeStampCounterMultiplier);
                    }
                    else
                    {
                        _coreClocks[i].Value = (float)TimeStampCounterFrequency;
                    }
                }

                if (newBusClock > 0)
                {
                    _busClock.Value = (float)newBusClock;
                    ActivateSensor(_busClock);
                }
            }
        }

        public override void Close()
        {
            _temperatureStream?.Close();
            base.Close();
        }

        public void Dispose()
        {
            _temperatureStream?.Dispose();
        }
    }
}
