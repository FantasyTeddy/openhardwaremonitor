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

        private readonly TaskService taskService;
        private bool startup;
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
                taskService = null;
                IsAvailable = false;
                return;
            }

            if (IsAdministrator())
            {
                try
                {
                    taskService = new TaskService();
                }
                catch
                {
                    taskService = null;
                }

                if (taskService != null)
                {
                    try
                    {
                        try
                        {
                            // check if the taskscheduler is running
                            RunningTaskCollection collection = taskService.GetRunningTasks(false);
                        }
                        catch (ArgumentException) { }

                        TaskFolder folder = taskService.GetFolder("\\Open Hardware Monitor");
                        if (folder != null)
                        {
                            Task task = folder.Tasks["Startup"];
                            startup = (task != null) &&
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
                            startup = false;
                        }

                    }
                    catch (IOException)
                    {
                        startup = false;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        taskService = null;
                    }
                    catch (COMException)
                    {
                        taskService = null;
                    }
                    catch (NotImplementedException)
                    {
                        taskService = null;
                    }
                }
            }
            else
            {
                taskService = null;
            }

            if (taskService == null)
            {
                try
                {
                    using (RegistryKey key =
                      Registry.CurrentUser.OpenSubKey(REGISTRY_RUN))
                    {
                        startup = false;
                        if (key != null)
                        {
                            string value = (string)key.GetValue("OpenHardwareMonitor");
                            if (value != null)
                                startup = value == Application.ExecutablePath;
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
            TaskDefinition definition = taskService.NewTask();
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

            TaskFolder root = taskService.GetFolder("\\");
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
            TaskFolder root = taskService.GetFolder("\\");
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
            get => startup;
            set
            {
                if (startup != value)
                {
                    if (IsAvailable)
                    {
                        if (taskService != null)
                        {
                            if (value)
                                CreateSchedulerTask();
                            else
                                DeleteSchedulerTask();
                            startup = value;
                        }
                        else
                        {
                            try
                            {
                                if (value)
                                    CreateRegistryRun();
                                else
                                    DeleteRegistryRun();
                                startup = value;
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
