/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;

namespace OpenHardwareMonitor.GUI
{
    public class StartupManager
    {

        private readonly TaskService _taskService;
        private bool _startup;
        private const string REGISTRY_RUN =
          @"Software\Microsoft\Windows\CurrentVersion\Run";

        private static bool IsAdministrator()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        public StartupManager()
        {
            if (Hardware.OperatingSystem.IsUnix)
            {
                _taskService = null;
                IsAvailable = false;
                return;
            }

            if (IsAdministrator())
            {
                try
                {
                    _taskService = new TaskService();
                }
                catch
                {
                    _taskService = null;
                }

                if (_taskService != null)
                {
                    try
                    {
                        try
                        {
                            // check if the taskscheduler is running
                            RunningTaskCollection collection = _taskService.GetRunningTasks(false);
                        }
                        catch (ArgumentException) { }

                        TaskFolder folder = _taskService.GetFolder("\\Open Hardware Monitor");
                        if (folder != null)
                        {
                            Task task = folder.Tasks["Startup"];
                            _startup = (task != null) &&
                              (task.Definition.Triggers.Count > 0) &&
                              (task.Definition.Triggers[1].TriggerType ==
                                TaskTriggerType.Logon) &&
                              (task.Definition.Actions.Count > 0) &&
                              (task.Definition.Actions[1].ActionType ==
                                TaskActionType.Execute) &&
                              ((task.Definition.Actions[1] as ExecAction) != null) &&
                              ((task.Definition.Actions[1] as ExecAction).Path ==
                                Application.ExecutablePath);
                        }
                        else
                        {
                            _startup = false;
                        }

                    }
                    catch (IOException)
                    {
                        _startup = false;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        _taskService = null;
                    }
                    catch (COMException)
                    {
                        _taskService = null;
                    }
                    catch (NotImplementedException)
                    {
                        _taskService = null;
                    }
                }
            }
            else
            {
                _taskService = null;
            }

            if (_taskService == null)
            {
                try
                {
                    using (RegistryKey key =
                      Registry.CurrentUser.OpenSubKey(REGISTRY_RUN))
                    {
                        _startup = false;
                        if (key != null)
                        {
                            string value = (string)key.GetValue("OpenHardwareMonitor");
                            if (value != null)
                                _startup = value == Application.ExecutablePath;
                        }
                    }
                    IsAvailable = true;
                }
                catch (SecurityException)
                {
                    IsAvailable = false;
                }
            }
            else
            {
                IsAvailable = true;
            }
        }

        private void CreateSchedulerTask()
        {
            TaskDefinition definition = _taskService.NewTask();
            definition.RegistrationInfo.Description =
              "This task starts the Open Hardware Monitor on Windows startup.";
            definition.Principal.RunLevel =
              TaskRunLevel.Highest;
            definition.Settings.DisallowStartIfOnBatteries = false;
            definition.Settings.StopIfGoingOnBatteries = false;
            definition.Settings.ExecutionTimeLimit = TimeSpan.Zero;

            LogonTrigger trigger = (LogonTrigger)definition.Triggers.AddNew(
              TaskTriggerType.Logon);

            ExecAction action = (ExecAction)definition.Actions.AddNew(
              TaskActionType.Execute);
            action.Path = Application.ExecutablePath;
            action.WorkingDirectory =
              Path.GetDirectoryName(Application.ExecutablePath);

            TaskFolder root = _taskService.GetFolder("\\");
            TaskFolder folder;
            try
            {
                folder = root.SubFolders["Open Hardware Monitor"];
            }
            catch (IOException)
            {
                folder = root.CreateFolder("Open Hardware Monitor", string.Empty);
            }
            folder.RegisterTaskDefinition("Startup", definition,
              TaskCreation.CreateOrUpdate, null, null,
              TaskLogonType.InteractiveToken, string.Empty);
        }

        private void DeleteSchedulerTask()
        {
            TaskFolder root = _taskService.GetFolder("\\");
            try
            {
                TaskFolder folder = root.SubFolders["Open Hardware Monitor"];
                folder.DeleteTask("Startup");
            }
            catch (IOException) { }
            try
            {
                root.DeleteFolder("Open Hardware Monitor");
            }
            catch (IOException) { }
        }

        private static void CreateRegistryRun()
        {
            RegistryKey key = Registry.CurrentUser.CreateSubKey(REGISTRY_RUN);
            key.SetValue("OpenHardwareMonitor", Application.ExecutablePath);
        }

        private static void DeleteRegistryRun()
        {
            RegistryKey key = Registry.CurrentUser.CreateSubKey(REGISTRY_RUN);
            key.DeleteValue("OpenHardwareMonitor");
        }

        public bool IsAvailable { get; }

        public bool Startup
        {
            get => _startup;
            set
            {
                if (_startup != value)
                {
                    if (IsAvailable)
                    {
                        if (_taskService != null)
                        {
                            if (value)
                                CreateSchedulerTask();
                            else
                                DeleteSchedulerTask();
                            _startup = value;
                        }
                        else
                        {
                            try
                            {
                                if (value)
                                    CreateRegistryRun();
                                else
                                    DeleteRegistryRun();
                                _startup = value;
                            }
                            catch (UnauthorizedAccessException)
                            {
                                throw new InvalidOperationException();
                            }
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
            }
        }
    }

}
