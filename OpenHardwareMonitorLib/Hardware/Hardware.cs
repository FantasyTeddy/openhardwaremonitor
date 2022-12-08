/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2011 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

using System;
using OpenHardwareMonitor.Collections;

namespace OpenHardwareMonitor.Hardware
{
    internal abstract class Hardware : IHardware
    {
        protected readonly string name;
        private string customName;
        protected readonly ISettings settings;
        protected readonly ListSet<ISensor> active = new ListSet<ISensor>();

        public Hardware(string name, Identifier identifier, ISettings settings)
        {
            this.settings = settings;
            Identifier = identifier;
            this.name = name;
            this.customName = settings.GetValue(
              new Identifier(Identifier, "name").ToString(), name);
        }

        public IHardware[] SubHardware => new IHardware[0];

        public virtual IHardware Parent => null;

        public virtual ISensor[] Sensors => active.ToArray();

        protected virtual void ActivateSensor(ISensor sensor)
        {
            if (active.Add(sensor))
                SensorAdded?.Invoke(sensor);
        }

        protected virtual void DeactivateSensor(ISensor sensor)
        {
            if (active.Remove(sensor))
                SensorRemoved?.Invoke(sensor);
        }

        public string Name
        {
            get => customName;
            set
            {
                if (!string.IsNullOrEmpty(value))
                    customName = value;
                else
                    customName = name;
                settings.SetValue(new Identifier(Identifier, "name").ToString(),
                  customName);
            }
        }

        public Identifier Identifier { get; }

#pragma warning disable 67
        public event SensorEventHandler SensorAdded;
        public event SensorEventHandler SensorRemoved;
#pragma warning restore 67


        public abstract HardwareType HardwareType { get; }

        public virtual string GetReport()
        {
            return null;
        }

        public abstract void Update();

        public event HardwareEventHandler Closing;

        public virtual void Close()
        {
            Closing?.Invoke(this);
        }

        public void Accept(IVisitor visitor)
        {
            if (visitor == null)
                throw new ArgumentNullException("visitor");
            visitor.VisitHardware(this);
        }

        public virtual void Traverse(IVisitor visitor)
        {
            foreach (ISensor sensor in active)
                sensor.Accept(visitor);
        }
    }
}
