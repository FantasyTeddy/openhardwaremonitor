/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2012-2013 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

namespace OpenHardwareMonitor.Hardware.RAM
{
    internal class RAMGroup : IGroup
    {

        private readonly Hardware[] _hardware;

        public RAMGroup(SMBIOS smbios, ISettings settings)
        {

            // No implementation for RAM on Unix systems
            if (OperatingSystem.IsUnix)
            {
                _hardware = System.Array.Empty<Hardware>();
                return;
            }

            _hardware = new Hardware[] { new GenericRAM("Generic Memory", settings) };
        }

        public string GetReport()
        {
            return null;
        }

        public IHardware[] Hardware => _hardware;

        public void Close()
        {
            foreach (Hardware ram in _hardware)
                ram.Close();
        }
    }
}
