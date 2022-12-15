﻿/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2012 Prince Samuel <prince.samuel@gmail.com>
  Copyright (C) 2012-2013 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using OpenHardwareMonitor.GUI;
using OpenHardwareMonitor.Hardware;

namespace OpenHardwareMonitor.WebServer
{
    [Route("data.json")]
    [ApiController]
    public class DataController : ControllerBase
    {
        public static Node Root;
        private int nodeCount;

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Get()
        {
            nodeCount = 1;

            var data = new DataNode
            {
                Id = 0,
                Text = "Sensor",
                Children = new List<DataNode> { GenerateNode(Root) },
                Min = "Min",
                Value = "Value",
                Max = "Max",
                ImageURL = string.Empty,
            };

            return new JsonResult(data);
        }

        private DataNode GenerateNode(Node n)
        {
            var node = new DataNode
            {
                Id = nodeCount,
                Text = n.Text,
                Children = new List<DataNode>(),
            };
            nodeCount++;

            foreach (Node child in n.Nodes)
                node.Children.Add(GenerateNode(child));

            if (n is SensorNode sensor)
            {
                node.Min = sensor.Min;
                node.Value = sensor.Value;
                node.Max = sensor.Max;
                node.ImageURL = "images/transparent.png";
            }
            else if (n is HardwareNode hardware)
            {
                node.Min = string.Empty;
                node.Value = string.Empty;
                node.Max = string.Empty;
                node.ImageURL = "images_icon/" + GetHardwareImageFile(hardware);
            }
            else if (n is TypeNode type)
            {
                node.Min = string.Empty;
                node.Value = string.Empty;
                node.Max = string.Empty;
                node.ImageURL = "images_icon/" + GetTypeImageFile(type);
            }
            else
            {
                node.Min = string.Empty;
                node.Value = string.Empty;
                node.Max = string.Empty;
                node.ImageURL = "images_icon/computer.png";
            }

            return node;
        }

        private static string GetHardwareImageFile(HardwareNode hn)
        {
            switch (hn.Hardware.HardwareType)
            {
                case HardwareType.CPU:
                    return "cpu.png";
                case HardwareType.GpuNvidia:
                    return "nvidia.png";
                case HardwareType.GpuAti:
                    return "ati.png";
                case HardwareType.HDD:
                    return "hdd.png";
                case HardwareType.Heatmaster:
                    return "bigng.png";
                case HardwareType.Mainboard:
                    return "mainboard.png";
                case HardwareType.SuperIO:
                    return "chip.png";
                case HardwareType.TBalancer:
                    return "bigng.png";
                case HardwareType.RAM:
                    return "ram.png";
                default:
                    return "cpu.png";
            }
        }

        private static string GetTypeImageFile(TypeNode tn)
        {
            switch (tn.SensorType)
            {
                case SensorType.Voltage:
                    return "voltage.png";
                case SensorType.Clock:
                    return "clock.png";
                case SensorType.Load:
                    return "load.png";
                case SensorType.Temperature:
                    return "temperature.png";
                case SensorType.Fan:
                    return "fan.png";
                case SensorType.Flow:
                    return "flow.png";
                case SensorType.Control:
                    return "control.png";
                case SensorType.Level:
                    return "level.png";
                case SensorType.Power:
                    return "power.png";
                default:
                    return "power.png";
            }
        }
    }
}
