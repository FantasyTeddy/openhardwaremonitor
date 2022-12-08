/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2010 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OpenHardwareMonitor.GUI
{
    public class ShowDesktop : IDisposable
    {
        public class ShowDesktopChangedEventArgs : EventArgs
        {
            public ShowDesktopChangedEventArgs(bool showDesktop)
            {
                ShowDesktop = showDesktop;
            }

            public bool ShowDesktop { get; }
        }

        private event EventHandler<ShowDesktopChangedEventArgs> ShowDesktopChangedEvent;

        private readonly System.Threading.Timer timer;
        private bool showDesktop;
        private readonly NativeWindow referenceWindow;
        private readonly string referenceWindowCaption =
          "OpenHardwareMonitorShowDesktopReferenceWindow";

        private ShowDesktop()
        {
            // create a reference window to detect show desktop
            referenceWindow = new NativeWindow();
            CreateParams cp = new CreateParams
            {
                ExStyle = GadgetWindow.WS_EX_TOOLWINDOW,
                Caption = referenceWindowCaption
            };
            referenceWindow.CreateHandle(cp);
            NativeMethods.SetWindowPos(referenceWindow.Handle,
              GadgetWindow.HWND_BOTTOM, 0, 0, 0, 0, GadgetWindow.SWP_NOMOVE |
              GadgetWindow.SWP_NOSIZE | GadgetWindow.SWP_NOACTIVATE |
              GadgetWindow.SWP_NOSENDCHANGING);

            // start a repeated timer to detect "Show Desktop" events
            timer = new System.Threading.Timer(OnTimer, null,
              System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
        }

        private void StartTimer()
        {
            timer.Change(0, 200);
        }

        private void StopTimer()
        {
            timer.Change(System.Threading.Timeout.Infinite,
              System.Threading.Timeout.Infinite);
        }

        // the desktop worker window (if available) can hide the reference window
        private static IntPtr GetDesktopWorkerWindow()
        {
            IntPtr shellWindow = NativeMethods.GetShellWindow();
            if (shellWindow == IntPtr.Zero)
                return IntPtr.Zero;

            NativeMethods.GetWindowThreadProcessId(shellWindow, out int shellId);

            IntPtr workerWindow = IntPtr.Zero;
            while ((workerWindow = NativeMethods.FindWindowEx(
                IntPtr.Zero, workerWindow, "WorkerW", null)) != IntPtr.Zero)
            {

                NativeMethods.GetWindowThreadProcessId(workerWindow, out int workerId);
                if (workerId == shellId)
                {
                    IntPtr window = NativeMethods.FindWindowEx(
                      workerWindow, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (window != IntPtr.Zero)
                    {
                        IntPtr desktopWindow = NativeMethods.FindWindowEx(
                          window, IntPtr.Zero, "SysListView32", null);
                        if (desktopWindow != IntPtr.Zero)
                            return workerWindow;
                    }
                }
            }
            return IntPtr.Zero;
        }

        private void OnTimer(object state)
        {
            bool showDesktopDetected;

            IntPtr workerWindow = GetDesktopWorkerWindow();
            if (workerWindow != IntPtr.Zero)
            {
                // search if the reference window is behind the worker window
                IntPtr reference = NativeMethods.FindWindowEx(
                  IntPtr.Zero, workerWindow, null, referenceWindowCaption);
                showDesktopDetected = reference != IntPtr.Zero;
            }
            else
            {
                // if there is no worker window, then nothing can hide the reference
                showDesktopDetected = false;
            }

            if (showDesktop != showDesktopDetected)
            {
                showDesktop = showDesktopDetected;
                ShowDesktopChangedEvent?.Invoke(this, new ShowDesktopChangedEventArgs(showDesktop));
            }
        }

        public static ShowDesktop Instance { get; } = new ShowDesktop();

        // notify when the "show desktop" mode is changed
        public event EventHandler<ShowDesktopChangedEventArgs> ShowDesktopChanged
        {
            add
            {
                // start the monitor timer when someone is listening
                if (ShowDesktopChangedEvent == null)
                    StartTimer();
                ShowDesktopChangedEvent += value;
            }
            remove
            {
                ShowDesktopChangedEvent -= value;
                // stop the monitor timer if nobody is interested
                if (ShowDesktopChangedEvent == null)
                    StopTimer();
            }
        }

        private static class NativeMethods
        {
            private const string USER = "user32.dll";

            [DllImport(USER, CallingConvention = CallingConvention.Winapi)]
            public static extern bool SetWindowPos(IntPtr hWnd,
              IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

            [DllImport(USER, CallingConvention = CallingConvention.Winapi)]
            public static extern IntPtr FindWindowEx(IntPtr hwndParent,
              IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

            [DllImport(USER, CallingConvention = CallingConvention.Winapi)]
            public static extern IntPtr GetShellWindow();

            [DllImport(USER, CallingConvention = CallingConvention.Winapi)]
            public static extern int GetWindowThreadProcessId(IntPtr hWnd,
              out int processId);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer?.Dispose();
            }
        }
    }
}
