/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OpenHardwareMonitor.Hardware.Nvidia
{

    internal class NvidiaGroup : IGroup
    {

        private readonly List<Hardware> _hardware = new List<Hardware>();
        private readonly StringBuilder _report = new StringBuilder();

        public NvidiaGroup(ISettings settings)
        {
            if (!NVAPI.IsAvailable)
                return;

            _report.AppendLine("NVAPI");
            _report.AppendLine();

            if (NVAPI.NvAPI_GetInterfaceVersionString(out string version) == NvStatus.OK)
            {
                _report.Append(" Version: ");
                _report.AppendLine(version);
            }

            NvPhysicalGpuHandle[] handles =
              new NvPhysicalGpuHandle[NVAPI.MAX_PHYSICAL_GPUS];
            int count;
            if (NVAPI.NvAPI_EnumPhysicalGPUs == null)
            {
                _report.AppendLine(" Error: NvAPI_EnumPhysicalGPUs not available");
                _report.AppendLine();
                return;
            }
            else
            {
                NvStatus status = NVAPI.NvAPI_EnumPhysicalGPUs(handles, out count);
                if (status != NvStatus.OK)
                {
                    _report.AppendLine(" Status: " + status);
                    _report.AppendLine();
                    return;
                }
            }

            NVML.NvmlReturn result = NVML.NvmlInit();

            _report.AppendLine();
            _report.AppendLine("NVML");
            _report.AppendLine();
            _report.AppendLine(" Status: " + result);
            _report.AppendLine();

            IDictionary<NvPhysicalGpuHandle, NvDisplayHandle> displayHandles =
              new Dictionary<NvPhysicalGpuHandle, NvDisplayHandle>();

            if (NVAPI.NvAPI_EnumNvidiaDisplayHandle != null &&
              NVAPI.NvAPI_GetPhysicalGPUsFromDisplay != null)
            {
                NvStatus status = NvStatus.OK;
                int i = 0;
                while (status == NvStatus.OK)
                {
                    NvDisplayHandle displayHandle = default(NvDisplayHandle);
                    status = NVAPI.NvAPI_EnumNvidiaDisplayHandle(i, ref displayHandle);
                    i++;

                    if (status == NvStatus.OK)
                    {
                        NvPhysicalGpuHandle[] handlesFromDisplay =
                          new NvPhysicalGpuHandle[NVAPI.MAX_PHYSICAL_GPUS];
                        if (NVAPI.NvAPI_GetPhysicalGPUsFromDisplay(displayHandle,
                          handlesFromDisplay, out uint countFromDisplay) == NvStatus.OK)
                        {
                            for (int j = 0; j < countFromDisplay; j++)
                            {
                                if (!displayHandles.ContainsKey(handlesFromDisplay[j]))
                                    displayHandles.Add(handlesFromDisplay[j], displayHandle);
                            }
                        }
                    }
                }
            }

            _report.Append("Number of GPUs: ");
            _report.AppendLine(count.ToString(CultureInfo.InvariantCulture));

            for (int i = 0; i < count; i++)
            {
                displayHandles.TryGetValue(handles[i], out NvDisplayHandle displayHandle);
                _hardware.Add(new NvidiaGPU(i, handles[i], displayHandle, settings));
            }

            _report.AppendLine();
        }

        public IHardware[] Hardware => _hardware.ToArray();

        public string GetReport()
        {
            return _report.ToString();
        }

        public void Close()
        {
            foreach (Hardware gpu in _hardware)
                gpu.Close();

            if (NVML.IsInitialized)
            {
                NVML.NvmlShutdown();
            }
        }
    }
}
