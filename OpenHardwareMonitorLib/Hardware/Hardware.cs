/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2011 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenHardwareMonitor.Hardware
{
    internal abstract class Hardware : IHardware
    {
        protected readonly string name;
        private string _customName;
        protected readonly ISettings settings;
        protected readonly HashSet<ISensor> active = new HashSet<ISensor>();

        public Hardware(string name, Identifier identifier, ISettings settings)
        {
            this.settings = settings;
            Identifier = identifier;
            this.name = name;
            _customName = settings.GetValue(
              new Identifier(Identifier, "name").ToString(), name);
        }

        public IHardware[] SubHardware => Array.Empty<IHardware>();

        public virtual IHardware Parent => null;

        public virtual ISensor[] Sensors => active.ToArray();

        protected virtual void ActivateSensor(ISensor sensor)
        {
            if (active.Add(sensor))
                SensorAdded?.Invoke(this, new SensorEventArgs(sensor));
        }

        protected virtual void DeactivateSensor(ISensor sensor)
        {
            if (active.Remove(sensor))
                SensorRemoved?.Invoke(this, new SensorEventArgs(sensor));
        }

        public string Name
        {
            get => _customName;
            set
            {
                if (!string.IsNullOrEmpty(value))
                    _customName = value;
                else
                    _customName = name;
                settings.SetValue(new Identifier(Identifier, "name").ToString(),
                  _customName);
            }
        }

        public Identifier Identifier { get; }

#pragma warning disable 67
        public event EventHandler<SensorEventArgs> SensorAdded;
        public event EventHandler<SensorEventArgs> SensorRemoved;
#pragma warning restore 67


        public abstract HardwareType HardwareType { get; }

        public virtual string GetReport()
        {
            return null;
        }

        public abstract void Update();

        public event EventHandler<HardwareEventArgs> Closing;

        public virtual void Close()
        {
            Closing?.Invoke(this, new HardwareEventArgs(this));
        }

        public void Accept(IVisitor visitor)
        {
            if (visitor == null)
                throw new ArgumentNullException(nameof(visitor));
            visitor.VisitHardware(this);
        }

        public virtual void Traverse(IVisitor visitor)
        {
            foreach (ISensor sensor in active)
                sensor.Accept(visitor);
        }
    }
}
