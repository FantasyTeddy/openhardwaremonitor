/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2012 Prince Samuel <prince.samuel@gmail.com>

*/

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using OpenHardwareMonitor.Utilities;

namespace OpenHardwareMonitor.GUI
{
    public partial class PortForm : Form
    {
        private readonly PersistentSettings settings;
        private string localIP;

        public PortForm(PersistentSettings s)
        {
            InitializeComponent();

            settings = s;
        }

        private void PortForm_Load(object sender, EventArgs e)
        {
            localIP = getLocalIP();

            portNumericUpDn.Value = settings.GetValue("listenerPort", 8085);
            portNumericUpDn_ValueChanged(null, null);
        }

        private void portNumericUpDn_ValueChanged(object sender, EventArgs e)
        {
            string url = "http://" + localIP + ":" + portNumericUpDn.Value + "/";
            webServerLinkLabel.Text = url;
        }

        private void portOKButton_Click(object sender, EventArgs e)
        {
            settings.SetValue("listenerPort", (int)portNumericUpDn.Value);
            Close();
        }

        private void portCancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void webServerLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                Process.Start(((LinkLabel)sender).Text);
            }
            catch
            {
            }
        }

        private static string getLocalIP()
        {
            string localIP = "?";
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    localIP = ip.ToString();
                }
            }
            return localIP;
        }
    }
}
