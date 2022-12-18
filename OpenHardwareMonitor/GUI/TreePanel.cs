/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>
  Copyright (C) 2010 Paul Werelds <paul@werelds.net>
  Copyright (C) 2012 Prince Samuel <prince.samuel@gmail.com>

*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Aga.Controls.Tree;
using Aga.Controls.Tree.NodeControls;
using OpenHardwareMonitor.Hardware;
using OpenHardwareMonitor.Utilities;

namespace OpenHardwareMonitor.GUI
{
    public partial class TreePanel : UserControl
    {
        private readonly PersistentSettings _settings;
        private readonly SystemTray _systemTray;
        private readonly SensorGadget _gadget;
        private readonly TreeModel _treeModel;
        private readonly Node _root;

        private bool _selectionDragging;

        public TreePanel(PersistentSettings settings, SystemTray systemTray, SensorGadget gadget, Node root)
        {
            InitializeComponent();

            _settings = settings;
            _systemTray = systemTray;
            _gadget = gadget;
            _root = root;

            treeView.Font = SystemFonts.MessageBoxFont;

            nodeCheckBox.IsVisibleValueNeeded += nodeCheckBox_IsVisibleValueNeeded;
            nodeTextBoxText.DrawText += nodeTextBoxText_DrawText;
            nodeTextBoxValue.DrawText += nodeTextBoxText_DrawText;
            nodeTextBoxMin.DrawText += nodeTextBoxText_DrawText;
            nodeTextBoxMax.DrawText += nodeTextBoxText_DrawText;
            nodeTextBoxText.EditorShowing += nodeTextBoxText_EditorShowing;

            sensor.Width = DpiHelper.LogicalToDeviceUnits(250);
            value.Width = DpiHelper.LogicalToDeviceUnits(100);
            min.Width = DpiHelper.LogicalToDeviceUnits(100);
            max.Width = DpiHelper.LogicalToDeviceUnits(100);

            foreach (TreeColumn column in treeView.Columns)
            {
                column.Width = Math.Max(DpiHelper.LogicalToDeviceUnits(20), Math.Min(
                    DpiHelper.LogicalToDeviceUnits(400),
                    _settings.GetValue("treeView.Columns." + column.Header + ".Width", column.Width)));
            }

            _treeModel = new TreeModel();
            _treeModel.Nodes.Add(_root);
            treeView.Model = _treeModel;

            if (Hardware.OperatingSystem.IsUnix)
            {
                treeView.RowHeight = Math.Max(treeView.RowHeight, DpiHelper.LogicalToDeviceUnits(18));
                treeView.BorderStyle = BorderStyle.Fixed3D;
            }
            else
            {
                treeView.RowHeight = Math.Max(
                    treeView.Font.Height + DpiHelper.LogicalToDeviceUnits(1),
                    DpiHelper.LogicalToDeviceUnits(18));
            }
        }

        public bool ShowPlot { get; set; }

        public IDictionary<ISensor, Color> SensorPlotColors { get; set; } = new Dictionary<ISensor, Color>();

        public void ShowHiddenSensors(bool value)
        {
            _treeModel.ForceVisible = value;
        }

        public void SetVisibility(int column, bool value)
        {
            treeView.Columns[column].IsVisible = value;
        }

        public IEnumerable<object> GetAllTags()
        {
            return treeView.AllNodes.Select(node => node.Tag);
        }

        public void SetCurrentSettings()
        {
            foreach (TreeColumn column in treeView.Columns)
            {
                _settings.SetValue("treeView.Columns." + column.Header + ".Width", column.Width);
            }
        }

        private void nodeTextBoxText_DrawText(object sender, DrawEventArgs e)
        {
            if (e.Node.Tag is Node node)
            {
                if (node.IsVisible)
                {
                    if (ShowPlot && node is SensorNode sensorNode &&
                        SensorPlotColors.TryGetValue(sensorNode.Sensor, out Color color))
                    {
                        e.TextColor = color;
                    }
                }
                else
                {
                    e.TextColor = Color.DarkGray;
                }
            }
        }

        private void nodeTextBoxText_EditorShowing(object sender, CancelEventArgs e)
        {
            e.Cancel = !(treeView.CurrentNode != null &&
                (treeView.CurrentNode.Tag is SensorNode ||
                treeView.CurrentNode.Tag is HardwareNode));
        }

        private void nodeCheckBox_IsVisibleValueNeeded(object sender, NodeControlValueEventArgs e)
        {
            e.Value = (e.Node.Tag is SensorNode) && ShowPlot;
        }

        private void treeView_Click(object sender, EventArgs e)
        {
            if (!(e is MouseEventArgs m) || m.Button != MouseButtons.Right)
                return;

            NodeControlInfo info = treeView.GetNodeControlInfoAt(new Point(m.X, m.Y));
            treeView.SelectedNode = info.Node;
            if (info.Node != null)
            {
                if (info.Node.Tag is SensorNode node && node.Sensor != null)
                {
                    treeContextMenu.MenuItems.Clear();
                    if (node.Sensor.Parameters.Count > 0)
                    {
                        MenuItem item = new MenuItem("Parameters...");
                        item.Click += (obj, args) =>
                        {
                            ShowParameterForm(node.Sensor);
                        };
                        treeContextMenu.MenuItems.Add(item);
                    }

                    if (nodeTextBoxText.EditEnabled)
                    {
                        MenuItem item = new MenuItem("Rename");
                        item.Click += (obj, args) =>
                        {
                            nodeTextBoxText.BeginEdit();
                        };
                        treeContextMenu.MenuItems.Add(item);
                    }

                    if (node.IsVisible)
                    {
                        MenuItem item = new MenuItem("Hide");
                        item.Click += (obj, args) =>
                        {
                            node.IsVisible = false;
                        };
                        treeContextMenu.MenuItems.Add(item);
                    }
                    else
                    {
                        MenuItem item = new MenuItem("Unhide");
                        item.Click += (obj, args) =>
                        {
                            node.IsVisible = true;
                        };
                        treeContextMenu.MenuItems.Add(item);
                    }

                    treeContextMenu.MenuItems.Add(new MenuItem("-"));
                    {
                        MenuItem item = new MenuItem("Pen Color...");
                        item.Click += (obj, args) =>
                        {
                            ColorDialog dialog = new ColorDialog
                            {
                                Color = node.PenColor.GetValueOrDefault(),
                            };
                            if (dialog.ShowDialog() == DialogResult.OK)
                                node.PenColor = dialog.Color;
                        };
                        treeContextMenu.MenuItems.Add(item);
                    }

                    {
                        MenuItem item = new MenuItem("Reset Pen Color");
                        item.Click += (obj, args) =>
                        {
                            node.PenColor = null;
                        };
                        treeContextMenu.MenuItems.Add(item);
                    }

                    treeContextMenu.MenuItems.Add(new MenuItem("-"));
                    {
                        MenuItem item = new MenuItem("Show in Tray")
                        {
                            Checked = _systemTray.Contains(node.Sensor),
                        };
                        item.Click += (obj, args) =>
                        {
                            if (item.Checked)
                                _systemTray.Remove(node.Sensor);
                            else
                                _systemTray.Add(node.Sensor, true);
                        };
                        treeContextMenu.MenuItems.Add(item);
                    }

                    if (_gadget != null)
                    {
                        MenuItem item = new MenuItem("Show in Gadget")
                        {
                            Checked = _gadget.Contains(node.Sensor),
                        };
                        item.Click += (obj, args) =>
                        {
                            if (item.Checked)
                            {
                                _gadget.Remove(node.Sensor);
                            }
                            else
                            {
                                _gadget.Add(node.Sensor);
                            }
                        };
                        treeContextMenu.MenuItems.Add(item);
                    }

                    if (node.Sensor.Control != null)
                    {
                        treeContextMenu.MenuItems.Add(new MenuItem("-"));
                        IControl control = node.Sensor.Control;
                        MenuItem controlItem = new MenuItem("Control");
                        MenuItem defaultItem = new MenuItem("Default")
                        {
                            Checked = control.ControlMode == ControlMode.Default,
                        };
                        controlItem.MenuItems.Add(defaultItem);
                        defaultItem.Click += (obj, args) =>
                        {
                            control.SetDefault();
                        };
                        MenuItem manualItem = new MenuItem("Manual");
                        controlItem.MenuItems.Add(manualItem);
                        manualItem.Checked = control.ControlMode == ControlMode.Software;
                        for (int i = 0; i <= 100; i += 5)
                        {
                            if (i <= control.MaxSoftwareValue &&
                                i >= control.MinSoftwareValue)
                            {
                                MenuItem item = new MenuItem(i + " %")
                                {
                                    RadioCheck = true,
                                };
                                manualItem.MenuItems.Add(item);
                                item.Checked = control.ControlMode == ControlMode.Software &&
                                    Math.Round(control.SoftwareValue) == i;
                                int softwareValue = i;
                                item.Click += (obj, args) =>
                                {
                                    control.SetSoftware(softwareValue);
                                };
                            }
                        }

                        treeContextMenu.MenuItems.Add(controlItem);
                    }

                    treeContextMenu.Show(treeView, new Point(m.X, m.Y));
                }

                if (info.Node.Tag is HardwareNode hardwareNode && hardwareNode.Hardware != null)
                {
                    treeContextMenu.MenuItems.Clear();

                    if (nodeTextBoxText.EditEnabled)
                    {
                        MenuItem item = new MenuItem("Rename");
                        item.Click += (obj, args) =>
                        {
                            nodeTextBoxText.BeginEdit();
                        };
                        treeContextMenu.MenuItems.Add(item);
                    }

                    treeContextMenu.Show(treeView, new Point(m.X, m.Y));
                }
            }
        }

        private static void ShowParameterForm(ISensor sensor)
        {
            ParameterForm form = new ParameterForm
            {
                Parameters = sensor.Parameters,
            };
            form.captionLabel.Text = sensor.Name;
            form.ShowDialog();
        }

        private void treeView_NodeMouseDoubleClick(object sender, TreeNodeAdvMouseEventArgs e)
        {
            if (e.Node.Tag is SensorNode node && node.Sensor != null &&
                node.Sensor.Parameters.Count > 0)
            {
                ShowParameterForm(node.Sensor);
            }
        }

        private void treeView_MouseMove(object sender, MouseEventArgs e)
        {
            _selectionDragging &= (e.Button & (MouseButtons.Left | MouseButtons.Right)) > 0;

            if (_selectionDragging)
                treeView.SelectedNode = treeView.GetNodeAt(e.Location);
        }

        private void treeView_MouseDown(object sender, MouseEventArgs e)
        {
            _selectionDragging = true;
        }

        private void treeView_MouseUp(object sender, MouseEventArgs e)
        {
            _selectionDragging = false;
        }
    }
}
