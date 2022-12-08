/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2010 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

using System;

namespace OpenHardwareMonitor.Hardware
{
    public class HardwareEventArgs : EventArgs
    {
        public HardwareEventArgs(IHardware hardware)
        {
            Hardware = hardware;
        }

        public IHardware Hardware { get; }
    }

    public interface IComputer : IElement
    {

        IHardware[] Hardware { get; }

        bool MainboardEnabled { get; }
        bool CPUEnabled { get; }
        bool RAMEnabled { get; }
        bool GPUEnabled { get; }
        bool FanControllerEnabled { get; }
        bool HDDEnabled { get; }


        string GetReport();

        event EventHandler<HardwareEventArgs> HardwareAdded;
        event EventHandler<HardwareEventArgs> HardwareRemoved;
    }
}
