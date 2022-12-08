/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2011 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;

namespace OpenHardwareMonitor.Hardware
{
    public class SensorEventArgs : EventArgs
    {
        public SensorEventArgs(ISensor sensor)
        {
            Sensor = sensor;
        }

        public ISensor Sensor { get; }
    }

    public enum HardwareType
    {
        Mainboard,
        SuperIO,
        CPU,
        RAM,
        GpuNvidia,
        GpuAti,
        TBalancer,
        Heatmaster,
        HDD
    }

    public interface IHardware : IElement
    {

        string Name { get; set; }
        Identifier Identifier { get; }

        HardwareType HardwareType { get; }

        string GetReport();

        void Update();

        IHardware[] SubHardware { get; }

        IHardware Parent { get; }

        ISensor[] Sensors { get; }

        event EventHandler<SensorEventArgs> SensorAdded;
        event EventHandler<SensorEventArgs> SensorRemoved;
    }
}
