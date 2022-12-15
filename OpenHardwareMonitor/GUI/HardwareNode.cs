/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Collections.Generic;
using OpenHardwareMonitor.Hardware;
using OpenHardwareMonitor.Utilities;

namespace OpenHardwareMonitor.GUI
{
    public class HardwareNode : Node
    {

        private readonly PersistentSettings _settings;
        private readonly UnitManager _unitManager;
        private readonly Identifier _expandedIdentifier;

        private readonly List<TypeNode> _typeNodes = new List<TypeNode>();

        public HardwareNode(IHardware hardware, PersistentSettings settings,
          UnitManager unitManager)
            : base()
        {
            _settings = settings;
            _unitManager = unitManager;
            Hardware = hardware;
            Image = HardwareTypeImage.Instance.GetImage(hardware.HardwareType);

            foreach (SensorType sensorType in Enum.GetValues(typeof(SensorType)))
                _typeNodes.Add(new TypeNode(sensorType, hardware, settings));

            foreach (ISensor sensor in hardware.Sensors)
                SensorAdded(sensor);

            hardware.SensorAdded += (_, e) => SensorAdded(e.Sensor);
            hardware.SensorRemoved += (_, e) => SensorRemoved(e.Sensor);

            _expandedIdentifier = new Identifier(hardware.Identifier, "expanded");
            base.IsExpanded =
              settings.GetValue(_expandedIdentifier.ToString(), base.IsExpanded);
        }

        public override string Text
        {
            get => Hardware.Name;
            set => Hardware.Name = value;
        }

        public IHardware Hardware { get; }

        public override bool IsExpanded
        {
            get => base.IsExpanded;
            set
            {
                if (base.IsExpanded != value)
                {
                    base.IsExpanded = value;
                    _settings.SetValue(_expandedIdentifier.ToString(), value);
                }
            }
        }

        private void UpdateNode(TypeNode node)
        {
            if (node.Nodes.Count > 0)
            {
                if (!Nodes.Contains(node))
                {
                    int i = 0;
                    while (i < Nodes.Count &&
                      ((TypeNode)Nodes[i]).SensorType < node.SensorType)
                    {
                        i++;
                    }

                    Nodes.Insert(i, node);
                }
            }
            else
            {
                if (Nodes.Contains(node))
                    Nodes.Remove(node);
            }
        }

        private void SensorRemoved(ISensor sensor)
        {
            foreach (TypeNode typeNode in _typeNodes)
            {
                if (typeNode.SensorType == sensor.SensorType)
                {
                    SensorNode sensorNode = null;
                    foreach (Node node in typeNode.Nodes)
                    {
                        if (node is SensorNode n && n.Sensor == sensor)
                            sensorNode = n;
                    }
                    if (sensorNode != null)
                    {
                        sensorNode.PlotSelectionChanged -= SensorPlotSelectionChanged;
                        typeNode.Nodes.Remove(sensorNode);
                        UpdateNode(typeNode);
                    }
                }
            }

            PlotSelectionChanged?.Invoke(this, null);
        }

        private void InsertSorted(Node node, ISensor sensor)
        {
            int i = 0;
            while (i < node.Nodes.Count &&
              ((SensorNode)node.Nodes[i]).Sensor.Index < sensor.Index)
            {
                i++;
            }

            SensorNode sensorNode = new SensorNode(sensor, _settings, _unitManager);
            sensorNode.PlotSelectionChanged += SensorPlotSelectionChanged;
            node.Nodes.Insert(i, sensorNode);
        }

        private void SensorPlotSelectionChanged(object sender, EventArgs e)
        {
            PlotSelectionChanged?.Invoke(this, null);
        }

        private void SensorAdded(ISensor sensor)
        {
            foreach (TypeNode typeNode in _typeNodes)
            {
                if (typeNode.SensorType == sensor.SensorType)
                {
                    InsertSorted(typeNode, sensor);
                    UpdateNode(typeNode);
                }
            }

            PlotSelectionChanged?.Invoke(this, null);
        }

        public event EventHandler PlotSelectionChanged;
    }
}
