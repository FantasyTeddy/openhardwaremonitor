/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using OpenHardwareMonitor.Hardware;
using OpenHardwareMonitor.Utilities;

namespace OpenHardwareMonitor.GUI
{
    public class TypeNode : Node
    {

        private readonly PersistentSettings settings;
        private readonly IHardware hardware;
        private readonly Identifier expandedIdentifier;

        public TypeNode(SensorType sensorType, IHardware hardware,
          PersistentSettings settings) : base()
        {
            this.settings = settings;
            SensorType = sensorType;
            this.hardware = hardware;

            switch (sensorType)
            {
                case SensorType.Voltage:
                    Image = Utilities.EmbeddedResources.GetImage("voltage.png");
                    Text = "Voltages";
                    break;
                case SensorType.Clock:
                    Image = Utilities.EmbeddedResources.GetImage("clock.png");
                    Text = "Clocks";
                    break;
                case SensorType.Load:
                    Image = Utilities.EmbeddedResources.GetImage("load.png");
                    Text = "Load";
                    break;
                case SensorType.Temperature:
                    Image = Utilities.EmbeddedResources.GetImage("temperature.png");
                    Text = "Temperatures";
                    break;
                case SensorType.Fan:
                    Image = Utilities.EmbeddedResources.GetImage("fan.png");
                    Text = "Fans";
                    break;
                case SensorType.Flow:
                    Image = Utilities.EmbeddedResources.GetImage("flow.png");
                    Text = "Flows";
                    break;
                case SensorType.Control:
                    Image = Utilities.EmbeddedResources.GetImage("control.png");
                    Text = "Controls";
                    break;
                case SensorType.Level:
                    Image = Utilities.EmbeddedResources.GetImage("level.png");
                    Text = "Levels";
                    break;
                case SensorType.Power:
                    Image = Utilities.EmbeddedResources.GetImage("power.png");
                    Text = "Powers";
                    break;
                case SensorType.Data:
                    Image = Utilities.EmbeddedResources.GetImage("data.png");
                    Text = "Data";
                    break;
                case SensorType.SmallData:
                    Image = Utilities.EmbeddedResources.GetImage("data.png");
                    Text = "Data";
                    break;
                case SensorType.Factor:
                    Image = Utilities.EmbeddedResources.GetImage("factor.png");
                    Text = "Factors";
                    break;
                case SensorType.Throughput:
                    Image = Utilities.EmbeddedResources.GetImage("throughput.png");
                    Text = "Throughput";
                    break;
            }

            NodeAdded += (_, e) => TypeNode_NodeAdded(e.Node);
            NodeRemoved += (_, e) => TypeNode_NodeRemoved(e.Node);

            this.expandedIdentifier = new Identifier(new Identifier(hardware.Identifier,
              sensorType.ToString().ToLowerInvariant()), "expanded");
            base.IsExpanded =
              settings.GetValue(expandedIdentifier.ToString(), base.IsExpanded);
        }

        private void TypeNode_NodeRemoved(Node node)
        {
            node.IsVisibleChanged -= node_IsVisibleChanged;
            node_IsVisibleChanged(this, new NodeEventArgs(null));
        }

        private void TypeNode_NodeAdded(Node node)
        {
            node.IsVisibleChanged += node_IsVisibleChanged;
            node_IsVisibleChanged(this, new NodeEventArgs(null));
        }

        private void node_IsVisibleChanged(object sender, NodeEventArgs e)
        {
            foreach (Node n in Nodes)
            {
                if (n.IsVisible)
                {
                    IsVisible = true;
                    return;
                }
            }

            IsVisible = false;
        }

        public SensorType SensorType { get; }

        public override bool IsExpanded
        {
            get => base.IsExpanded;
            set
            {
                if (base.IsExpanded != value)
                {
                    base.IsExpanded = value;
                    settings.SetValue(expandedIdentifier.ToString(), value);
                }
            }
        }
    }
}
