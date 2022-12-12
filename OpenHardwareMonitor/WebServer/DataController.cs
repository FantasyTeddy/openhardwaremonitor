/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2012 Prince Samuel <prince.samuel@gmail.com>
  Copyright (C) 2012-2013 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.IO;
using System.Net;
using System.Text;
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
        public void Get()
        {
            string json = "{\"id\": 0, \"Text\": \"Sensor\", \"Children\": [";
            nodeCount = 1;
            json += GenerateJSON(Root);
            json += "]";
            json += ", \"Min\": \"Min\"";
            json += ", \"Value\": \"Value\"";
            json += ", \"Max\": \"Max\"";
            json += ", \"ImageURL\": \"\"";
            json += "}";

            string responseContent = json;
            byte[] buffer = Encoding.UTF8.GetBytes(responseContent);

            Response.Headers.Add("Cache-Control", "no-cache");

            Response.ContentLength = buffer.Length;
            Response.ContentType = "application/json";

            try
            {
                Stream output = Response.Body;
                output.Write(buffer, 0, buffer.Length);
                output.Close();
            }
            catch (HttpListenerException)
            {
            }
        }

        private string GenerateJSON(Node n)
        {
            string json = "{\"id\": " + nodeCount + ", \"Text\": \"" + n.Text
              + "\", \"Children\": [";
            nodeCount++;

            foreach (Node child in n.Nodes)
                json += GenerateJSON(child) + ", ";
            if (json.EndsWith(", ", StringComparison.Ordinal))
                json = json.Remove(json.LastIndexOf(",", StringComparison.Ordinal));
            json += "]";

            if (n is SensorNode)
            {
                json += ", \"Min\": \"" + ((SensorNode)n).Min + "\"";
                json += ", \"Value\": \"" + ((SensorNode)n).Value + "\"";
                json += ", \"Max\": \"" + ((SensorNode)n).Max + "\"";
                json += ", \"ImageURL\": \"images/transparent.png\"";
            }
            else if (n is HardwareNode)
            {
                json += ", \"Min\": \"\"";
                json += ", \"Value\": \"\"";
                json += ", \"Max\": \"\"";
                json += ", \"ImageURL\": \"images_icon/" +
                  GetHardwareImageFile((HardwareNode)n) + "\"";
            }
            else if (n is TypeNode)
            {
                json += ", \"Min\": \"\"";
                json += ", \"Value\": \"\"";
                json += ", \"Max\": \"\"";
                json += ", \"ImageURL\": \"images_icon/" +
                  GetTypeImageFile((TypeNode)n) + "\"";
            }
            else
            {
                json += ", \"Min\": \"\"";
                json += ", \"Value\": \"\"";
                json += ", \"Max\": \"\"";
                json += ", \"ImageURL\": \"images_icon/computer.png\"";
            }

            json += "}";
            return json;
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
