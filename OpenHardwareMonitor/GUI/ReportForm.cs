﻿/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Windows.Forms;

namespace OpenHardwareMonitor.GUI
{
    public partial class ReportForm : Form
    {

        private string _report;

        public ReportForm()
        {
            InitializeComponent();
            try
            {
                titleLabel.Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold);
                reportTextBox.Font = new Font(FontFamily.GenericMonospace,
                  SystemFonts.DefaultFont.Size);
            }
            catch { }
        }

        public string Report
        {
            get => _report;
            set
            {
                _report = value;
                reportTextBox.Text = _report;
            }
        }

        private void sendButton_Click(object sender, EventArgs e)
        {
            Version version = typeof(CrashForm).Assembly.GetName().Version;
            WebRequest request = WebRequest.Create(
              "http://openhardwaremonitor.org/report.php");
            request.Method = "POST";
            request.Timeout = 5000;
            request.ContentType = "application/x-www-form-urlencoded";

            string report =
              "type=hardware&" +
              "version=" + HttpUtility.UrlEncode(version.ToString()) + "&" +
              "report=" + HttpUtility.UrlEncode(reportTextBox.Text) + "&" +
              "comment=" + HttpUtility.UrlEncode(commentTextBox.Text) + "&" +
              "email=" + HttpUtility.UrlEncode(emailTextBox.Text);
            byte[] byteArray = Encoding.UTF8.GetBytes(report);
            request.ContentLength = byteArray.Length;

            try
            {
                Stream dataStream = request.GetRequestStream();
                dataStream.Write(byteArray, 0, byteArray.Length);
                dataStream.Close();

                WebResponse response = request.GetResponse();
                dataStream = response.GetResponseStream();
                StreamReader reader = new StreamReader(dataStream);
                string responseFromServer = reader.ReadToEnd();
                reader.Close();
                dataStream.Close();
                response.Close();

                Close();
            }
            catch (WebException)
            {
                MessageBox.Show("Sending the hardware report failed.", "Error",
                  MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
