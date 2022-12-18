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
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using OpenHardwareMonitor.Hardware;
using OpenHardwareMonitor.Utilities;
using OpenHardwareMonitor.WebServer;
using OpenHardwareMonitor.WMI;

namespace OpenHardwareMonitor.GUI
{
    public partial class MainForm : Form
    {

        private readonly PersistentSettings _settings;
        private readonly UnitManager _unitManager;
        private readonly Computer _computer;
        private readonly Node _root;
        private readonly TreePanel _treePanel;
        private readonly Color[] _plotColorPalette;
        private readonly SystemTray _systemTray;
        private readonly StartupManager _startupManager = new StartupManager();
        private readonly UpdateVisitor _updateVisitor = new UpdateVisitor();
        private readonly SensorGadget _gadget;
        private Form _plotForm;
        private readonly PlotPanel _plotPanel;

        private readonly UserOption _showHiddenSensors;
        private UserOption _showPlot;
        private readonly UserOption _showValue;
        private readonly UserOption _showMin;
        private readonly UserOption _showMax;
        private readonly UserOption _startMinimized;
        private readonly UserOption _minimizeToTray;
        private readonly UserOption _minimizeOnClose;
        private readonly UserOption _autoStart;

        private readonly UserOption _readMainboardSensors;
        private readonly UserOption _readCpuSensors;
        private readonly UserOption _readRamSensors;
        private readonly UserOption _readGpuSensors;
        private readonly UserOption _readFanControllersSensors;
        private readonly UserOption _readHddSensors;

        private readonly UserOption _showGadget;
        private UserRadioGroup _plotLocation;
        private readonly WmiProvider _wmiProvider;

        private readonly UserOption _runWebServer;
        private readonly UserOption _logSensors;
        private readonly UserRadioGroup _loggingInterval;
        private readonly Logger _logger;

        public MainForm()
        {
            InitializeComponent();

            // check if the OpenHardwareMonitorLib assembly has the correct version
            if (Assembly.GetAssembly(typeof(Computer)).GetName().Version !=
              Assembly.GetExecutingAssembly().GetName().Version)
            {
                MessageBox.Show(
                  "The version of the file OpenHardwareMonitorLib.dll is incompatible.",
                  "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(0);
            }

            _settings = new PersistentSettings();
            _settings.Load(Path.ChangeExtension(
              Application.ExecutablePath, ".config"));

            _unitManager = new UnitManager(_settings);

            // make sure the buffers used for double buffering are not disposed
            // after each draw call
            BufferedGraphicsManager.Current.MaximumBuffer =
              Screen.PrimaryScreen.Bounds.Size;

            // set the DockStyle here, to avoid conflicts with the MainMenu
            splitContainer.Dock = DockStyle.Fill;

            Font = SystemFonts.MessageBoxFont;

            _plotPanel = new PlotPanel(_settings, _unitManager)
            {
                Font = SystemFonts.MessageBoxFont,
                Dock = DockStyle.Fill
            };

            _root = new Node(System.Environment.MachineName)
            {
                Image = Utilities.EmbeddedResources.GetImage("computer.png")
            };

            _computer = new Computer(_settings);

            _systemTray = new SystemTray(_computer, _settings, _unitManager);
            _systemTray.HideShowCommand += hideShowClick;
            _systemTray.ExitCommand += exitClick;

            if (Hardware.OperatingSystem.IsUnix)
            { // Unix
                splitContainer.BorderStyle = BorderStyle.None;
                splitContainer.SplitterWidth = 4;
                _plotPanel.BorderStyle = BorderStyle.Fixed3D;
                gadgetMenuItem.Visible = false;
                minCloseMenuItem.Visible = false;
                minTrayMenuItem.Visible = false;
                startMinMenuItem.Visible = false;
            }
            else
            { // Windows
                _gadget = new SensorGadget(_computer, _settings, _unitManager);
                _gadget.HideShowCommand += hideShowClick;

                _wmiProvider = new WmiProvider(_computer);
            }

            _treePanel = new TreePanel(_settings, _systemTray, _gadget, _root)
            {
                Dock = DockStyle.Fill,
            };
            splitContainer.Panel1.Controls.Add(_treePanel);

            _logger = new Logger(_computer);

            _plotColorPalette = new Color[13];
            _plotColorPalette[0] = Color.Blue;
            _plotColorPalette[1] = Color.OrangeRed;
            _plotColorPalette[2] = Color.Green;
            _plotColorPalette[3] = Color.LightSeaGreen;
            _plotColorPalette[4] = Color.Goldenrod;
            _plotColorPalette[5] = Color.DarkViolet;
            _plotColorPalette[6] = Color.YellowGreen;
            _plotColorPalette[7] = Color.SaddleBrown;
            _plotColorPalette[8] = Color.RoyalBlue;
            _plotColorPalette[9] = Color.DeepPink;
            _plotColorPalette[10] = Color.MediumSeaGreen;
            _plotColorPalette[11] = Color.Olive;
            _plotColorPalette[12] = Color.Firebrick;

            _computer.HardwareAdded += (_, e) => HardwareAdded(e.Hardware);
            _computer.HardwareRemoved += (_, e) => HardwareRemoved(e.Hardware);

            _computer.Open();

            Microsoft.Win32.SystemEvents.PowerModeChanged += PowerModeChanged;

            timer.Enabled = true;

            _showHiddenSensors = new UserOption("hiddenMenuItem", false,
              hiddenMenuItem, _settings);
            _showHiddenSensors.Changed += (sender, e) =>
            {
                _treePanel.ShowHiddenSensors(_showHiddenSensors.Value);
            };

            _showValue = new UserOption("valueMenuItem", true, valueMenuItem,
              _settings);
            _showValue.Changed += (sender, e) =>
            {
                _treePanel.SetVisibility(1, _showValue.Value);
            };

            _showMin = new UserOption("minMenuItem", false, minMenuItem, _settings);
            _showMin.Changed += (sender, e) =>
            {
                _treePanel.SetVisibility(2, _showMin.Value);
            };

            _showMax = new UserOption("maxMenuItem", true, maxMenuItem, _settings);
            _showMax.Changed += (sender, e) =>
            {
                _treePanel.SetVisibility(3, _showMax.Value);
            };

            _startMinimized = new UserOption("startMinMenuItem", false,
              startMinMenuItem, _settings);

            _minimizeToTray = new UserOption("minTrayMenuItem", true,
              minTrayMenuItem, _settings);
            _minimizeToTray.Changed += (sender, e) =>
            {
                _systemTray.IsMainIconEnabled = _minimizeToTray.Value;
            };

            _minimizeOnClose = new UserOption("minCloseMenuItem", false,
              minCloseMenuItem, _settings);

            _autoStart = new UserOption(null, _startupManager.Startup,
              startupMenuItem, _settings);
            _autoStart.Changed += (sender, e) =>
            {
                try
                {
                    _startupManager.Startup = _autoStart.Value;
                }
                catch (InvalidOperationException)
                {
                    MessageBox.Show("Updating the auto-startup option failed.", "Error",
                      MessageBoxButtons.OK, MessageBoxIcon.Error);
                    _autoStart.Value = _startupManager.Startup;
                }
            };

            _readMainboardSensors = new UserOption("mainboardMenuItem", true,
              mainboardMenuItem, _settings);
            _readMainboardSensors.Changed += (sender, e) =>
            {
                _computer.MainboardEnabled = _readMainboardSensors.Value;
            };

            _readCpuSensors = new UserOption("cpuMenuItem", true,
              cpuMenuItem, _settings);
            _readCpuSensors.Changed += (sender, e) =>
            {
                _computer.CPUEnabled = _readCpuSensors.Value;
            };

            _readRamSensors = new UserOption("ramMenuItem", true,
              ramMenuItem, _settings);
            _readRamSensors.Changed += (sender, e) =>
            {
                _computer.RAMEnabled = _readRamSensors.Value;
            };

            _readGpuSensors = new UserOption("gpuMenuItem", true,
              gpuMenuItem, _settings);
            _readGpuSensors.Changed += (sender, e) =>
            {
                _computer.GPUEnabled = _readGpuSensors.Value;
            };

            _readFanControllersSensors = new UserOption("fanControllerMenuItem", true,
              fanControllerMenuItem, _settings);
            _readFanControllersSensors.Changed += (sender, e) =>
            {
                _computer.FanControllerEnabled = _readFanControllersSensors.Value;
            };

            _readHddSensors = new UserOption("hddMenuItem", true, hddMenuItem,
              _settings);
            _readHddSensors.Changed += (sender, e) =>
            {
                _computer.HDDEnabled = _readHddSensors.Value;
            };

            _showGadget = new UserOption("gadgetMenuItem", false, gadgetMenuItem,
              _settings);
            _showGadget.Changed += (sender, e) =>
            {
                if (_gadget != null)
                    _gadget.Visible = _showGadget.Value;
            };

            celsiusMenuItem.Checked =
              _unitManager.TemperatureUnit == TemperatureUnit.Celsius;
            fahrenheitMenuItem.Checked = !celsiusMenuItem.Checked;

            Server = new RemoteWebServer(_root);

            _runWebServer = new UserOption("runWebServerMenuItem", false,
              runWebServerMenuItem, _settings);
            _runWebServer.Changed += (sender, e) =>
            {
                if (_runWebServer.Value)
                    Server.Start(_settings.GetValue("listenerPort", 8085));
                else
                    Server.Stop();
            };

            _logSensors = new UserOption("logSensorsMenuItem", false, logSensorsMenuItem,
              _settings);

            _loggingInterval = new UserRadioGroup("loggingInterval", 0,
              new[] { log1sMenuItem, log2sMenuItem, log5sMenuItem, log10sMenuItem,
        log30sMenuItem, log1minMenuItem, log2minMenuItem, log5minMenuItem,
        log10minMenuItem, log30minMenuItem, log1hMenuItem, log2hMenuItem,
        log6hMenuItem
              },
              _settings);
            _loggingInterval.Changed += (sender, e) =>
            {
                switch (_loggingInterval.Value)
                {
                    case 0: _logger.LoggingInterval = new TimeSpan(0, 0, 1); break;
                    case 1: _logger.LoggingInterval = new TimeSpan(0, 0, 2); break;
                    case 2: _logger.LoggingInterval = new TimeSpan(0, 0, 5); break;
                    case 3: _logger.LoggingInterval = new TimeSpan(0, 0, 10); break;
                    case 4: _logger.LoggingInterval = new TimeSpan(0, 0, 30); break;
                    case 5: _logger.LoggingInterval = new TimeSpan(0, 1, 0); break;
                    case 6: _logger.LoggingInterval = new TimeSpan(0, 2, 0); break;
                    case 7: _logger.LoggingInterval = new TimeSpan(0, 5, 0); break;
                    case 8: _logger.LoggingInterval = new TimeSpan(0, 10, 0); break;
                    case 9: _logger.LoggingInterval = new TimeSpan(0, 30, 0); break;
                    case 10: _logger.LoggingInterval = new TimeSpan(1, 0, 0); break;
                    case 11: _logger.LoggingInterval = new TimeSpan(2, 0, 0); break;
                    case 12: _logger.LoggingInterval = new TimeSpan(6, 0, 0); break;
                }
            };

            InitializePlotForm();

            startupMenuItem.Visible = _startupManager.IsAvailable;

            if (startMinMenuItem.Checked)
            {
                if (!minTrayMenuItem.Checked)
                {
                    WindowState = FormWindowState.Minimized;
                    Show();
                }
            }
            else
            {
                Show();
            }

            // Create a handle, otherwise calling Close() does not fire FormClosed
            IntPtr handle = Handle;

            // Make sure the settings are saved when the user logs off
            Microsoft.Win32.SystemEvents.SessionEnded += (sender, e) =>
            {
                _computer.Close();
                SaveConfiguration();
                if (_runWebServer.Value)
                    Server.Stop();
            };
        }

        private void PowerModeChanged(object sender,
          Microsoft.Win32.PowerModeChangedEventArgs e)
        {

            if (e.Mode == Microsoft.Win32.PowerModes.Resume)
            {
                _computer.Reset();
            }
        }

        private void InitializePlotForm()
        {
            _plotForm = new Form
            {
                FormBorderStyle = FormBorderStyle.SizableToolWindow,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual
            };
            AddOwnedForm(_plotForm);
            _plotForm.Bounds = new Rectangle
            {
                X = _settings.GetValue("plotForm.Location.X", -100000),
                Y = _settings.GetValue("plotForm.Location.Y", 100),
                Width = _settings.GetValue("plotForm.Width", 600),
                Height = _settings.GetValue("plotForm.Height", 400)
            };

            _showPlot = new UserOption("plotMenuItem", false, plotMenuItem, _settings);
            _plotLocation = new UserRadioGroup("plotLocation", 0,
              new[] { plotWindowMenuItem, plotBottomMenuItem, plotRightMenuItem },
              _settings);

            _showPlot.Changed += (sender, e) =>
            {
                if (_plotLocation.Value == 0)
                {
                    if (_showPlot.Value && Visible)
                        _plotForm.Show();
                    else
                        _plotForm.Hide();
                }
                else
                {
                    splitContainer.Panel2Collapsed = !_showPlot.Value;
                }
                _treePanel.ShowPlot = _showPlot.Value;
                _treePanel.Invalidate(true);
            };
            _plotLocation.Changed += (sender, e) =>
            {
                switch (_plotLocation.Value)
                {
                    case 0:
                        splitContainer.Panel2.Controls.Clear();
                        splitContainer.Panel2Collapsed = true;
                        _plotForm.Controls.Add(_plotPanel);
                        if (_showPlot.Value && Visible)
                            _plotForm.Show();
                        break;
                    case 1:
                        _plotForm.Controls.Clear();
                        _plotForm.Hide();
                        splitContainer.Orientation = Orientation.Horizontal;
                        splitContainer.Panel2.Controls.Add(_plotPanel);
                        splitContainer.Panel2Collapsed = !_showPlot.Value;
                        break;
                    case 2:
                        _plotForm.Controls.Clear();
                        _plotForm.Hide();
                        splitContainer.Orientation = Orientation.Vertical;
                        splitContainer.Panel2.Controls.Add(_plotPanel);
                        splitContainer.Panel2Collapsed = !_showPlot.Value;
                        break;
                }
            };

            _plotForm.FormClosing += (sender, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    // just switch off the plotting when the user closes the form
                    if (_plotLocation.Value == 0)
                    {
                        _showPlot.Value = false;
                    }
                    e.Cancel = true;
                }
            };

            EventHandler moveOrResizePlotForm = (sender, e) =>
            {
                if (_plotForm.WindowState != FormWindowState.Minimized)
                {
                    _settings.SetValue("plotForm.Location.X", _plotForm.Bounds.X);
                    _settings.SetValue("plotForm.Location.Y", _plotForm.Bounds.Y);
                    _settings.SetValue("plotForm.Width", _plotForm.Bounds.Width);
                    _settings.SetValue("plotForm.Height", _plotForm.Bounds.Height);
                }
            };
            _plotForm.Move += moveOrResizePlotForm;
            _plotForm.Resize += moveOrResizePlotForm;

            _plotForm.VisibleChanged += (sender, e) =>
            {
                Rectangle bounds = new Rectangle(_plotForm.Location, _plotForm.Size);
                Screen screen = Screen.FromRectangle(bounds);
                Rectangle intersection =
                  Rectangle.Intersect(screen.WorkingArea, bounds);
                if (intersection.Width < Math.Min(16, bounds.Width) ||
                    intersection.Height < Math.Min(16, bounds.Height))
                {
                    _plotForm.Location = new Point(
                      screen.WorkingArea.Width / 2 - bounds.Width / 2,
                      screen.WorkingArea.Height / 2 - bounds.Height / 2);
                }
            };

            VisibleChanged += (sender, e) =>
            {
                if (Visible && _showPlot.Value && _plotLocation.Value == 0)
                    _plotForm.Show();
                else
                    _plotForm.Hide();
            };
        }

        private static void InsertSorted(Collection<Node> nodes, HardwareNode node)
        {
            int i = 0;
            while (i < nodes.Count && nodes[i] is HardwareNode &&
              ((HardwareNode)nodes[i]).Hardware.HardwareType <=
                node.Hardware.HardwareType)
            {
                i++;
            }

            nodes.Insert(i, node);
        }

        private void SubHardwareAdded(IHardware hardware, Node node)
        {
            HardwareNode hardwareNode =
              new HardwareNode(hardware, _settings, _unitManager);
            hardwareNode.PlotSelectionChanged += PlotSelectionChanged;

            InsertSorted(node.Nodes, hardwareNode);

            foreach (IHardware subHardware in hardware.SubHardware)
                SubHardwareAdded(subHardware, hardwareNode);
        }

        private void HardwareAdded(IHardware hardware)
        {
            SubHardwareAdded(hardware, _root);
            PlotSelectionChanged(this, null);
        }

        private void HardwareRemoved(IHardware hardware)
        {
            List<HardwareNode> nodesToRemove = new List<HardwareNode>();
            foreach (Node node in _root.Nodes)
            {
                if (node is HardwareNode hardwareNode && hardwareNode.Hardware == hardware)
                    nodesToRemove.Add(hardwareNode);
            }
            foreach (HardwareNode hardwareNode in nodesToRemove)
            {
                _root.Nodes.Remove(hardwareNode);
                hardwareNode.PlotSelectionChanged -= PlotSelectionChanged;
            }
            PlotSelectionChanged(this, null);
        }

        private void PlotSelectionChanged(object sender, EventArgs e)
        {
            List<ISensor> selected = new List<ISensor>();
            IDictionary<ISensor, Color> colors = new Dictionary<ISensor, Color>();
            int colorIndex = 0;
            foreach (object tag in _treePanel.GetAllTags())
            {
                if (tag is SensorNode sensorNode)
                {
                    if (sensorNode.Plot)
                    {
                        if (!sensorNode.PenColor.HasValue)
                        {
                            colors.Add(sensorNode.Sensor,
                              _plotColorPalette[colorIndex % _plotColorPalette.Length]);
                        }
                        selected.Add(sensorNode.Sensor);
                    }
                    colorIndex++;
                }
            }

            // if a sensor is assigned a color that's already being used by another
            // sensor, try to assign it a new color. This is done only after the
            // previous loop sets an unchanging default color for all sensors, so that
            // colors jump around as little as possible as sensors get added/removed
            // from the plot
            var usedColors = new List<Color>();
            foreach (ISensor curSelectedSensor in selected)
            {
                if (!colors.ContainsKey(curSelectedSensor)) continue;
                Color curColor = colors[curSelectedSensor];
                if (usedColors.Contains(curColor))
                {
                    foreach (Color potentialNewColor in _plotColorPalette)
                    {
                        if (!colors.Values.Contains(potentialNewColor))
                        {
                            colors[curSelectedSensor] = potentialNewColor;
                            usedColors.Add(potentialNewColor);
                            break;
                        }
                    }
                }
                else
                {
                    usedColors.Add(curColor);
                }
            }

            foreach (object tag in _treePanel.GetAllTags())
            {
                if (tag is SensorNode sensorNode && sensorNode.Plot && sensorNode.PenColor.HasValue)
                    colors.Add(sensorNode.Sensor, sensorNode.PenColor.Value);
            }

            _treePanel.SensorPlotColors = colors;
            _plotPanel.SetSensors(selected, colors);
        }

        private void exitClick(object sender, EventArgs e)
        {
            Close();
        }

        private int _delayCount;
        private void timer_Tick(object sender, EventArgs e)
        {
            _computer.Accept(_updateVisitor);
            _treePanel.Invalidate(true);
            _plotPanel.InvalidatePlot();
            _systemTray.Redraw();
            _gadget?.Redraw();

            _wmiProvider?.Update();


            if (_logSensors != null && _logSensors.Value && _delayCount >= 4)
                _logger.Log();

            if (_delayCount < 4)
                _delayCount++;
        }

        private void SaveConfiguration()
        {
            if (_settings == null)
                return;

            if (_plotPanel != null)
            {
                _plotPanel.SetCurrentSettings();
                _treePanel.SetCurrentSettings();
            }

            string fileName = Path.ChangeExtension(
                System.Windows.Forms.Application.ExecutablePath, ".config");
            try
            {
                _settings.Save(fileName);
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("Access to the path '" + fileName + "' is denied. " +
                  "The current settings could not be saved.",
                  "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (IOException)
            {
                MessageBox.Show("The path '" + fileName + "' is not writeable. " +
                  "The current settings could not be saved.",
                  "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            Rectangle newBounds = new Rectangle
            {
                X = _settings.GetValue("mainForm.Location.X", Location.X),
                Y = _settings.GetValue("mainForm.Location.Y", Location.Y),
                Width = _settings.GetValue("mainForm.Width",
                DpiHelper.LogicalToDeviceUnits(470)),
                Height = _settings.GetValue("mainForm.Height",
                DpiHelper.LogicalToDeviceUnits(640))
            };

            Rectangle fullWorkingArea = new Rectangle(int.MaxValue, int.MaxValue,
              int.MinValue, int.MinValue);

            foreach (Screen screen in Screen.AllScreens)
                fullWorkingArea = Rectangle.Union(fullWorkingArea, screen.Bounds);

            Rectangle intersection = Rectangle.Intersect(fullWorkingArea, newBounds);
            if (intersection.Width < 20 || intersection.Height < 20 ||
              !_settings.Contains("mainForm.Location.X"))
            {
                newBounds.X = Screen.PrimaryScreen.WorkingArea.Width / 2 -
                              newBounds.Width / 2;

                newBounds.Y = Screen.PrimaryScreen.WorkingArea.Height / 2 -
                              newBounds.Height / 2;
            }

            Bounds = newBounds;
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            Visible = false;
            _systemTray.IsMainIconEnabled = false;
            timer.Enabled = false;
            _computer.Close();
            SaveConfiguration();
            if (_runWebServer.Value)
                Server.Stop();
            _systemTray.Dispose();
        }

        private void aboutMenuItem_Click(object sender, EventArgs e)
        {
            new AboutBox().ShowDialog();
        }

        private void saveReportMenuItem_Click(object sender, EventArgs e)
        {
            string report = _computer.GetReport();
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                using (TextWriter w = new StreamWriter(saveFileDialog.FileName))
                {
                    w.Write(report);
                }
            }
        }

        private void SysTrayHideShow()
        {
            Visible = !Visible;
            if (Visible)
                Activate();
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_SYSCOMMAND = 0x112;
            const int SC_MINIMIZE = 0xF020;
            const int SC_CLOSE = 0xF060;

            if (_minimizeToTray.Value &&
              m.Msg == WM_SYSCOMMAND && m.WParam.ToInt64() == SC_MINIMIZE)
            {
                SysTrayHideShow();
            }
            else if (_minimizeOnClose.Value &&
              m.Msg == WM_SYSCOMMAND && m.WParam.ToInt64() == SC_CLOSE)
            {
                /*
                 * Apparently the user wants to minimize rather than close
                 * Now we still need to check if we're going to the tray or not
                 *
                 * Note: the correct way to do this would be to send out SC_MINIMIZE,
                 * but since the code here is so simple,
                 * that would just be a waste of time.
                 */
                if (_minimizeToTray.Value)
                    SysTrayHideShow();
                else
                    WindowState = FormWindowState.Minimized;
            }
            else
            {
                base.WndProc(ref m);
            }
        }

        private void hideShowClick(object sender, EventArgs e)
        {
            SysTrayHideShow();
        }

        private void celsiusMenuItem_Click(object sender, EventArgs e)
        {
            celsiusMenuItem.Checked = true;
            fahrenheitMenuItem.Checked = false;
            _unitManager.TemperatureUnit = TemperatureUnit.Celsius;
        }

        private void fahrenheitMenuItem_Click(object sender, EventArgs e)
        {
            celsiusMenuItem.Checked = false;
            fahrenheitMenuItem.Checked = true;
            _unitManager.TemperatureUnit = TemperatureUnit.Fahrenheit;
        }

        private void sumbitReportMenuItem_Click(object sender, EventArgs e)
        {
            ReportForm form = new ReportForm
            {
                Report = _computer.GetReport()
            };
            form.ShowDialog();
        }

        private void resetMinMaxMenuItem_Click(object sender, EventArgs e)
        {
            _computer.Accept(new SensorVisitor((_, args) =>
            {
                args.Sensor.ResetMin();
                args.Sensor.ResetMax();
            }));
        }

        private void MainForm_MoveOrResize(object sender, EventArgs e)
        {
            if (WindowState != FormWindowState.Minimized)
            {
                _settings.SetValue("mainForm.Location.X", Bounds.X);
                _settings.SetValue("mainForm.Location.Y", Bounds.Y);
                _settings.SetValue("mainForm.Width", Bounds.Width);
                _settings.SetValue("mainForm.Height", Bounds.Height);
            }
        }

        private void resetClick(object sender, EventArgs e)
        {
            // disable the fallback MainIcon during reset, otherwise icon visibility
            // might be lost
            _systemTray.IsMainIconEnabled = false;
            _computer.Reset();
            // restore the MainIcon setting
            _systemTray.IsMainIconEnabled = _minimizeToTray.Value;
        }

        private void serverPortMenuItem_Click(object sender, EventArgs e)
        {
            new PortForm(_settings).ShowDialog();
        }

        public RemoteWebServer Server { get; }
    }
}
