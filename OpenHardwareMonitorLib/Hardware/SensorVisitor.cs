﻿/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2010 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;

namespace OpenHardwareMonitor.Hardware
{

    public class SensorVisitor : IVisitor
    {
        private readonly EventHandler<SensorEventArgs> _handler;

        public SensorVisitor(EventHandler<SensorEventArgs> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public void VisitComputer(IComputer computer)
        {
            if (computer == null)
                throw new ArgumentNullException(nameof(computer));
            computer.Traverse(this);
        }

        public void VisitHardware(IHardware hardware)
        {
            if (hardware == null)
                throw new ArgumentNullException(nameof(hardware));
            hardware.Traverse(this);
        }

        public void VisitSensor(ISensor sensor)
        {
            _handler(this, new SensorEventArgs(sensor));
        }

        public void VisitParameter(IParameter parameter) { }
    }
}
