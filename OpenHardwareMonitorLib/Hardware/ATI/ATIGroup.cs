/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OpenHardwareMonitor.Hardware.ATI
{
    internal class ATIGroup : IGroup
    {

        private readonly List<ATIGPU> _hardware = new List<ATIGPU>();
        private readonly StringBuilder _report = new StringBuilder();

        private IntPtr _context = IntPtr.Zero;

        public ATIGroup(ISettings settings)
        {
            try
            {
                ADLStatus adlStatus = ADL.ADL_Main_Control_Create(1);
                ADLStatus adl2Status = ADL.ADL2_Main_Control_Create(1, out _context);

                _report.AppendLine("AMD Display Library");
                _report.AppendLine();
                _report.Append("ADL Status: ");
                _report.AppendLine(adlStatus.ToString());
                _report.Append("ADL2 Status: ");
                _report.AppendLine(adl2Status.ToString());
                _report.AppendLine();

                _report.AppendLine("Graphics Versions");
                _report.AppendLine();
                try
                {
                    ADLStatus status = ADL.ADL_Graphics_Versions_Get(out ADLVersionsInfo versionInfo);
                    _report.Append(" Status: ");
                    _report.AppendLine(status.ToString());
                    _report.Append(" DriverVersion: ");
                    _report.AppendLine(versionInfo.DriverVersion);
                    _report.Append(" CatalystVersion: ");
                    _report.AppendLine(versionInfo.CatalystVersion);
                    _report.Append(" CatalystWebLink: ");
                    _report.AppendLine(versionInfo.CatalystWebLink);
                }
                catch (DllNotFoundException)
                {
                    _report.AppendLine(" Status: DLL not found");
                }
                catch (Exception e)
                {
                    _report.AppendLine(" Status: " + e.Message);
                }
                _report.AppendLine();

                if (adlStatus == ADLStatus.OK)
                {
                    int numberOfAdapters = 0;
                    ADL.ADL_Adapter_NumberOfAdapters_Get(ref numberOfAdapters);

                    _report.Append("Number of adapters: ");
                    _report.AppendLine(numberOfAdapters.ToString(CultureInfo.InvariantCulture));
                    _report.AppendLine();

                    if (numberOfAdapters > 0)
                    {
                        ADLAdapterInfo[] adapterInfo = new ADLAdapterInfo[numberOfAdapters];
                        if (ADL.ADL_Adapter_AdapterInfo_Get(adapterInfo) == ADLStatus.OK)
                        {
                            for (int i = 0; i < numberOfAdapters; i++)
                            {
                                ADL.ADL_Adapter_Active_Get(adapterInfo[i].AdapterIndex,
                                  out int isActive);
                                ADL.ADL_Adapter_ID_Get(adapterInfo[i].AdapterIndex,
                                  out int adapterID);

                                _report.Append("AdapterIndex: ");
                                _report.AppendLine(i.ToString(CultureInfo.InvariantCulture));
                                _report.Append("isActive: ");
                                _report.AppendLine(isActive.ToString(CultureInfo.InvariantCulture));
                                _report.Append("AdapterName: ");
                                _report.AppendLine(adapterInfo[i].AdapterName);
                                _report.Append("UDID: ");
                                _report.AppendLine(adapterInfo[i].UDID);
                                _report.Append("Present: ");
                                _report.AppendLine(adapterInfo[i].Present.ToString(
                                  CultureInfo.InvariantCulture));
                                _report.Append("VendorID: 0x");
                                _report.AppendLine(adapterInfo[i].VendorID.ToString("X",
                                  CultureInfo.InvariantCulture));
                                _report.Append("BusNumber: ");
                                _report.AppendLine(adapterInfo[i].BusNumber.ToString(
                                  CultureInfo.InvariantCulture));
                                _report.Append("DeviceNumber: ");
                                _report.AppendLine(adapterInfo[i].DeviceNumber.ToString(
                                 CultureInfo.InvariantCulture));
                                _report.Append("FunctionNumber: ");
                                _report.AppendLine(adapterInfo[i].FunctionNumber.ToString(
                                  CultureInfo.InvariantCulture));
                                _report.Append("AdapterID: 0x");
                                _report.AppendLine(adapterID.ToString("X",
                                  CultureInfo.InvariantCulture));

                                if (!string.IsNullOrEmpty(adapterInfo[i].UDID) &&
                                  adapterInfo[i].VendorID == ADL.ATI_VENDOR_ID)
                                {
                                    bool found = false;
                                    foreach (ATIGPU gpu in _hardware)
                                    {
                                        if (gpu.BusNumber == adapterInfo[i].BusNumber &&
                                          gpu.DeviceNumber == adapterInfo[i].DeviceNumber)
                                        {
                                            found = true;
                                            break;
                                        }
                                    }

                                    if (!found)
                                    {
                                        var nameBuilder = new StringBuilder(adapterInfo[i].AdapterName);
                                        nameBuilder.Replace("(TM)", " ");
                                        for (int j = 0; j < 10; j++) nameBuilder.Replace("  ", " ");
                                        string name = nameBuilder.ToString().Trim();

                                        _hardware.Add(new ATIGPU(name,
                                          adapterInfo[i].AdapterIndex,
                                          adapterInfo[i].BusNumber,
                                          adapterInfo[i].DeviceNumber, _context, settings));
                                    }
                                }

                                _report.AppendLine();
                            }
                        }
                    }
                }
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException e)
            {
                _report.AppendLine();
                _report.AppendLine(e.ToString());
                _report.AppendLine();
            }
        }

        public IHardware[] Hardware => _hardware.ToArray();

        public string GetReport()
        {
            return _report.ToString();
        }

        public void Close()
        {
            try
            {
                foreach (ATIGPU gpu in _hardware)
                    gpu.Close();

                if (_context != IntPtr.Zero)
                {
                    ADL.ADL2_Main_Control_Destroy(_context);
                    _context = IntPtr.Zero;
                }

                ADL.ADL_Main_Control_Destroy();
            }
            catch (Exception) { }
        }
    }
}
