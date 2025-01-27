﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using Interop.UIAutomationClient;
using Newtonsoft.Json.Linq;
using PInvoke;

namespace RoundedTB
{
    public class Background
    {
        private static int POLLING_RATE_IN_MS = 100;

        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        // Just have a reference point for the Dispatcher
        public MainWindow mw;
        bool redrawOverride = false;
        private static Thread workerThread = null;
        int infrequentCount = 0;

        public Background()
        {
            mw = (MainWindow)System.Windows.Application.Current.MainWindow;
        }

        public static int GetPollingRate()
        {
            return Interlocked.CompareExchange(ref POLLING_RATE_IN_MS, 0, 0);
        }
        public static void SetPollingRate(int val)
        {
            Interlocked.Exchange(ref POLLING_RATE_IN_MS, val);
        }

        

        private static bool CheckIfCurrentAppIsFullscreen()
        {
            IntPtr desktopHandle; //Window handle for the desktop
            IntPtr shellHandle;
            desktopHandle = LocalPInvoke.GetDesktopWindow();
            shellHandle = LocalPInvoke.GetShellWindow();
            //Detect if the current app is running in full screen
            bool runningFullScreen = false;
            LocalPInvoke.RECT appBounds;
            System.Drawing.Rectangle screenBounds;
            IntPtr hWnd;


            //get the dimensions of the active window
            hWnd = LocalPInvoke.GetForegroundWindow();
            if (!hWnd.Equals(IntPtr.Zero))
            {
                //Check we haven't picked up the desktop or the shell
                if (!(hWnd.Equals(desktopHandle) || hWnd.Equals(shellHandle)))
                {
                    LocalPInvoke.GetWindowRect(hWnd, out appBounds);
                    //determine if window is fullscreen
                    screenBounds = Screen.FromHandle(hWnd).Bounds;
                    if ((appBounds.Bottom - appBounds.Top) == screenBounds.Height && (appBounds.Right - appBounds.Left) == screenBounds.Width)
                    {
                        runningFullScreen = true;
                    }
                }
            }

            return runningFullScreen;
        }
        

        public static void AttachedThreadInputAction(Action action)
        {
            var foreThread = LocalPInvoke.GetWindowThreadProcessId(LocalPInvoke.GetForegroundWindow(),
                IntPtr.Zero);
            var appThread = LocalPInvoke.GetCurrentThreadId();
            bool threadsAttached = false;

            try
            {
                threadsAttached =
                    foreThread == appThread ||
                    LocalPInvoke.AttachThreadInput(foreThread, appThread, true);

                if (threadsAttached) action();
                else return;
            }
            finally
            {
                if (threadsAttached)
                    LocalPInvoke.AttachThreadInput(foreThread, appThread, false);
            }
        }

        private static IntPtr last_window = IntPtr.Zero;
        public static void ForceWindowToForeground(IntPtr hwnd, bool forceOnTop)
        {
            if (forceOnTop)
            {
                IntPtr tmp = LocalPInvoke.GetForegroundWindow(); 
                if (tmp != IntPtr.Zero)
                {
                    last_window = tmp;
                }
            }
                AttachedThreadInputAction(
                () =>
                {
                    if (forceOnTop)
                    {
                        LocalPInvoke.BringWindowToTop(hwnd);
                    }
                    LocalPInvoke.ShowWindow(hwnd, 5);
                });
        }

        private static void ForceForegroundWindow(IntPtr hWnd)
        {
            uint foreThread = LocalPInvoke.GetWindowThreadProcessId(LocalPInvoke.GetForegroundWindow(),
                IntPtr.Zero);
            uint appThread = LocalPInvoke.GetCurrentThreadId();

            if (foreThread != appThread)
            {
                LocalPInvoke.AttachThreadInput(foreThread, appThread, true);
                LocalPInvoke.BringWindowToTop(hWnd);
                LocalPInvoke.ShowWindow(hWnd, 5);
                LocalPInvoke.AttachThreadInput(foreThread, appThread, false);
            }
            else
            {
                LocalPInvoke.BringWindowToTop(hWnd);
                LocalPInvoke.ShowWindow(hWnd, 5);
            }
        }



            // Main method for the BackgroundWorker - runs indefinitely
            public void DoWork(object sender, DoWorkEventArgs e)
        {
            MainWindow.interaction.AddLog("in bw");
            Debug.WriteLine("BW: IN DO WORK");
            workerThread = new Thread(() => WorkRoutine());
            BackgroundWorker worker = sender as BackgroundWorker;
            while (true)
            {
                try
                {
                    if (worker.CancellationPending == true)
                    {
                        MainWindow.interaction.AddLog("cancelling");
                        e.Cancel = true;
                        break;
                    }

                    // Primary loop for the running process, put in a single thread, so no deadlock can occure.
                    else
                    {
                        if (!workerThread.IsAlive)
                        {
                                workerThread = new Thread(() => WorkRoutine());
                                workerThread.Start();
                        }
                        

                    System.Threading.Thread.Sleep(GetPollingRate());
                    }
                }
                catch (TypeInitializationException ex)
                {
                    MainWindow.interaction.AddLog(ex.Message);
                    MainWindow.interaction.AddLog(ex.InnerException.Message);
                    throw ex;
                }
            }
        }

        public bool StartMenuIsOpen()
        {

            Type tIAppVisibility = Type.GetTypeFromCLSID(new Guid("7E5FE3D9-985F-4908-91F9-EE19F9FD1514"));
            IAppVisibility appVisibility = (IAppVisibility)Activator.CreateInstance(tIAppVisibility);
            if (appVisibility.IsLauncherVisible())
            {
                return true;
            }
            return false;
        }

        public void RefreshWorkAreaIfNeeded(List<Types.Taskbar> taskbars, Types.Settings settings)
        {
            int workingHeight = Screen.PrimaryScreen.WorkingArea.Height;
            int boundsHeight = Screen.PrimaryScreen.Bounds.Height;
            int taskbarHeight = taskbars[0].TaskbarRect.Bottom - taskbars[0].TaskbarRect.Top;


            if (settings.AutoHide > 0)
            {
      
                MonitorStuff.DisplayInfoCollection Displays = MonitorStuff.GetDisplays();
                bool found_problem = false;
                foreach (MonitorStuff.DisplayInfo display in Displays)
                {

                    LocalPInvoke.RECT workArea = display.MonitorArea;
                    LocalPInvoke.RECT toCheck = new();
                    LocalPInvoke.SystemParametersInfo(LocalPInvoke.SPI_GETWORKAREA, 0, ref toCheck, 0);
                    var current = toCheck.Bottom - toCheck.Top;
                    var wanted = workArea.Bottom - 1 - workArea.Top;
                    if (current != wanted)
                    {
                        Debug.WriteLine("Found display with wrong work area, trying to refresh it.");
                        found_problem = true;
                        workArea.Bottom -= 1;
                        Interaction.SetWorkspace(workArea);
                    }
                    
                }
                if (found_problem)
                {
                    foreach (Types.Taskbar taskbar in taskbars)
                    {
                        LocalPInvoke.SetWindowPos(taskbar.TaskbarHwnd, new IntPtr(-1), 0, 0, 0, 0, LocalPInvoke.SetWindowPosFlags.IgnoreMove | LocalPInvoke.SetWindowPosFlags.IgnoreResize);
                        Taskbar.SetTaskbarState(LocalPInvoke.AppBarStates.AlwaysOnTop, taskbar.TaskbarHwnd);
                    }
                }
                
                
            }
        }



        public void WorkRoutine()
        {
            // Section for running less important things without requiring an additional thread
            infrequentCount++;
            if (infrequentCount == 10)
            {
                // Check to see if settings need to be shown
                List<IntPtr> windowList = Interaction.GetTopLevelWindows();
                foreach (IntPtr hwnd in windowList)
                {
                    StringBuilder windowClass = new StringBuilder(1024);
                    StringBuilder windowTitle = new StringBuilder(1024);
                    try
                    {
                        LocalPInvoke.GetClassName(hwnd, windowClass, 1024);
                        LocalPInvoke.GetWindowText(hwnd, windowTitle, 1024);

                        if (windowClass.ToString().Contains("HwndWrapper[RoundedTB.exe") && windowTitle.ToString() == "RoundedTB_SettingsRequest")
                        {
                            mw.Dispatcher.Invoke(() =>
                            {
                                if (mw.Visibility != Visibility.Visible)
                                {
                                    mw.ShowMenuItem_Click(null, null);
                                }
                            });
                            LocalPInvoke.SetWindowText(hwnd, "RoundedTB");
                        }
                    }
                    catch (Exception) { }
                }

                // Update tray icon
                mw.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        mw.TrayIconCheck();

                    }
                    catch (Exception)
                    {

                    }
                });

                infrequentCount = 0;
            }

            //check if the window region is applied correctly while using hide tb
            // Check if the taskbar is centred, and if it is, directly update the settings; using an interim bool to avoid delaying because I'm lazy
            bool isCentred = Taskbar.CheckIfCentred();
            mw.activeSettings.IsCentred = isCentred;

            // Work with static values to avoid some null reference exceptions
            List<Types.Taskbar> taskbars = mw.taskbarDetails;
            Types.Settings settings = mw.activeSettings;
            RefreshWorkAreaIfNeeded(taskbars, settings);

            // If the number of taskbars has changed, regenerate taskbar information
            if (Taskbar.TaskbarCountOrHandleChanged(taskbars.Count, taskbars[0].TaskbarHwnd))
            {
                // Forcefully reset taskbars if the taskbar count or main taskbar handle has changed
                taskbars = Taskbar.GenerateTaskbarInfo();
                Debug.WriteLine("Regenerating taskbar info");
            }


            for (int current = 0; current < taskbars.Count; current++)
            {
                if (taskbars[current].TaskbarHwnd == IntPtr.Zero || taskbars[current].AppListHwnd == IntPtr.Zero)
                {
                    taskbars = Taskbar.GenerateTaskbarInfo();
                    Debug.WriteLine("Regenerating taskbar info due to a missing handle");
                    break;
                }
                // Get the latest quick details of this taskbar
                Types.Taskbar newTaskbar = Taskbar.GetQuickTaskbarRects(taskbars[current]);


                // If the taskbar's monitor has a maximised window, reset it so it's "filled"
                if (Taskbar.TaskbarShouldBeFilled(taskbars[current].TaskbarHwnd, settings))
                {
                    if (taskbars[current].Ignored == false)
                    {
                        Taskbar.ResetTaskbar(taskbars[current], settings);
                        taskbars[current].Ignored = true;
                    }
                    continue;
                }

                // Showhide tray on hover
                if (settings.ShowSegmentsOnHover)
                {
                    LocalPInvoke.RECT currentTrayRect = taskbars[current].TrayRect;
                    LocalPInvoke.RECT currentWidgetsRect = taskbars[current].TaskbarRect;
                    currentWidgetsRect.Right = Convert.ToInt32(currentWidgetsRect.Right - (currentWidgetsRect.Right - currentWidgetsRect.Left) + (168 * taskbars[current].ScaleFactor));

                    if (currentTrayRect.Left != 0)
                    {
                        LocalPInvoke.GetCursorPos(out LocalPInvoke.POINT msPt);
                        bool isHoveringOverTray = LocalPInvoke.PtInRect(ref currentTrayRect, msPt);
                        bool isHoveringOverWidgets = LocalPInvoke.PtInRect(ref currentWidgetsRect, msPt);
                        if (isHoveringOverTray && !settings.ShowTray)
                        {
                            settings.ShowTray = true;
                            taskbars[current].Ignored = true;
                        }
                        else if (!isHoveringOverTray)
                        {
                            taskbars[current].Ignored = true;
                            settings.ShowTray = false;
                        }

                        if (isHoveringOverWidgets && !settings.ShowWidgets)
                        {
                            settings.ShowWidgets = true;
                            taskbars[current].Ignored = true;
                        }
                        else if (!isHoveringOverWidgets)
                        {
                            taskbars[current].Ignored = true;
                            settings.ShowWidgets = false;
                        }

                    }
                }

                if (settings.AutoHide > 0)
                {
                    LocalPInvoke.RECT currentTaskbarRect = taskbars[current].TaskbarRect;
                    LocalPInvoke.GetCursorPos(out LocalPInvoke.POINT msPt);
                    bool isHoveringOverTaskbar;
                    if (taskbars[current].TaskbarHidden)
                    {
                        currentTaskbarRect.Top = currentTaskbarRect.Bottom - 1;
                        isHoveringOverTaskbar = LocalPInvoke.PtInRect(ref currentTaskbarRect, msPt);

                    }
                    else
                    {
                        isHoveringOverTaskbar = LocalPInvoke.PtInRect(ref currentTaskbarRect, msPt);
                    }
                    if (isHoveringOverTaskbar)
                    {
                        //Debug.WriteLine("___");
                    }
                    int animSpeed = 15;
                    byte taskbarOpacity = 0;
                    LocalPInvoke.GetLayeredWindowAttributes(taskbars[current].TaskbarHwnd, out _, out taskbarOpacity, out _);
                    //Debug.WriteLine($"Taskbar opacity:  {taskbarOpacity}");
                    bool startMenuOpened = StartMenuIsOpen();

                    if ((isHoveringOverTaskbar || startMenuOpened) && taskbarOpacity == 1)
                    {
                        LocalPInvoke.SetWindowPos(taskbars[current].TaskbarHwnd, HWND_TOPMOST, 0, 0, 0, 0,
                            LocalPInvoke.SetWindowPosFlags.IgnoreResize | LocalPInvoke.SetWindowPosFlags.IgnoreMove);
                        int style = LocalPInvoke.GetWindowLong(taskbars[current].TaskbarHwnd, LocalPInvoke.GWL_EXSTYLE).ToInt32();
                        if ((style & LocalPInvoke.WS_EX_TRANSPARENT) == LocalPInvoke.WS_EX_TRANSPARENT)
                        {
                            LocalPInvoke.SetWindowLong(taskbars[current].TaskbarHwnd, LocalPInvoke.GWL_EXSTYLE, LocalPInvoke.GetWindowLong(taskbars[current].TaskbarHwnd, LocalPInvoke.GWL_EXSTYLE).ToInt32() ^ LocalPInvoke.WS_EX_TRANSPARENT);
                        }
                        LocalPInvoke.SetLayeredWindowAttributes(taskbars[current].TaskbarHwnd, 0, 63, LocalPInvoke.LWA_ALPHA);
                        System.Threading.Thread.Sleep(animSpeed);
                        LocalPInvoke.SetLayeredWindowAttributes(taskbars[current].TaskbarHwnd, 0, 127, LocalPInvoke.LWA_ALPHA);
                        System.Threading.Thread.Sleep(animSpeed);
                        LocalPInvoke.SetLayeredWindowAttributes(taskbars[current].TaskbarHwnd, 0, 191, LocalPInvoke.LWA_ALPHA);
                        System.Threading.Thread.Sleep(animSpeed);
                        LocalPInvoke.SetLayeredWindowAttributes(taskbars[current].TaskbarHwnd, 0, 255, LocalPInvoke.LWA_ALPHA);
                        taskbars[current].Ignored = true;
                        taskbars[current].TaskbarHidden = false;
                        //Set to be on top of all windows
                        foreach (Types.Taskbar taskbar in taskbars)
                        {
                            Debug.WriteLine("updating tb");
                            //check if current app is fullscreened, if yes, skip to not disrupt it.
                            if (!CheckIfCurrentAppIsFullscreen())
                            {
                                ForceWindowToForeground(taskbar.TaskbarHwnd, settings.ForceTBFocusOnTop);
                            }

                        }

                        Debug.WriteLine("MouseOver TB");
                    }
                    else if (!isHoveringOverTaskbar && !startMenuOpened && taskbarOpacity == 255)
                    {
                        LocalPInvoke.SetLayeredWindowAttributes(taskbars[current].TaskbarHwnd, 0, 191, LocalPInvoke.LWA_ALPHA);
                        System.Threading.Thread.Sleep(animSpeed);
                        LocalPInvoke.SetLayeredWindowAttributes(taskbars[current].TaskbarHwnd, 0, 127, LocalPInvoke.LWA_ALPHA);
                        System.Threading.Thread.Sleep(animSpeed);
                        LocalPInvoke.SetLayeredWindowAttributes(taskbars[current].TaskbarHwnd, 0, 63, LocalPInvoke.LWA_ALPHA);
                        System.Threading.Thread.Sleep(animSpeed);
                        LocalPInvoke.SetLayeredWindowAttributes(taskbars[current].TaskbarHwnd, 0, 1, LocalPInvoke.LWA_ALPHA);
                        int style = LocalPInvoke.GetWindowLong(taskbars[current].TaskbarHwnd, LocalPInvoke.GWL_EXSTYLE).ToInt32();
                        if ((style & LocalPInvoke.WS_EX_TRANSPARENT) != LocalPInvoke.WS_EX_TRANSPARENT)
                        {
                            LocalPInvoke.SetWindowLong(taskbars[current].TaskbarHwnd, LocalPInvoke.GWL_EXSTYLE, LocalPInvoke.GetWindowLong(taskbars[current].TaskbarHwnd, LocalPInvoke.GWL_EXSTYLE).ToInt32() ^ LocalPInvoke.WS_EX_TRANSPARENT);
                        }
                        taskbars[current].Ignored = true;
                        taskbars[current].TaskbarHidden = true;
                        Debug.WriteLine("MouseOff TB");
                        if (settings.ForceTBFocusOnTop)
                        {
                            IntPtr tmp = LocalPInvoke.GetForegroundWindow();
                            if (tmp == taskbars[current].TaskbarHwnd)
                            {
                                ForceWindowToForeground(last_window, settings.ForceTBFocusOnTop);
                            }
                        }
                    }
                }
                else
                {
                    int animSpeed = 15;
                    byte taskbarOpacity = 0;
                    LocalPInvoke.GetLayeredWindowAttributes(taskbars[current].TaskbarHwnd, out _, out taskbarOpacity, out _);
                    if (taskbarOpacity < 255)
                    {
                        int style = LocalPInvoke.GetWindowLong(taskbars[current].TaskbarHwnd, LocalPInvoke.GWL_EXSTYLE).ToInt32();
                        if ((style & LocalPInvoke.WS_EX_TRANSPARENT) == LocalPInvoke.WS_EX_TRANSPARENT)
                        {
                            LocalPInvoke.SetWindowLong(taskbars[current].TaskbarHwnd, LocalPInvoke.GWL_EXSTYLE, LocalPInvoke.GetWindowLong(taskbars[current].TaskbarHwnd, LocalPInvoke.GWL_EXSTYLE).ToInt32() ^ LocalPInvoke.WS_EX_TRANSPARENT);
                        }
                        LocalPInvoke.SetLayeredWindowAttributes(taskbars[current].TaskbarHwnd, 0, 63, LocalPInvoke.LWA_ALPHA);
                        System.Threading.Thread.Sleep(animSpeed);
                        LocalPInvoke.SetLayeredWindowAttributes(taskbars[current].TaskbarHwnd, 0, 127, LocalPInvoke.LWA_ALPHA);
                        System.Threading.Thread.Sleep(animSpeed);
                        LocalPInvoke.SetLayeredWindowAttributes(taskbars[current].TaskbarHwnd, 0, 191, LocalPInvoke.LWA_ALPHA);
                        System.Threading.Thread.Sleep(animSpeed);
                        LocalPInvoke.SetLayeredWindowAttributes(taskbars[current].TaskbarHwnd, 0, 255, LocalPInvoke.LWA_ALPHA);
                        taskbars[current].Ignored = true;
                        taskbars[current].TaskbarHidden = false;
                    }
                }

                // If the taskbar's overall rect has changed, update it. If it's simple, just update. If it's dynamic, check it's a valid change, then update it.
                if (Taskbar.TaskbarRefreshRequired(taskbars[current], newTaskbar, settings.IsDynamic) || taskbars[current].Ignored || redrawOverride)
                {
                    Debug.WriteLine($"Refresh required on taskbar {current}");
                    taskbars[current].Ignored = false;
                    int isFullTest = newTaskbar.TrayRect.Left - newTaskbar.AppListRect.Right;
                    //mw.interaction.AddLog($"Taskbar: {current} - AppList ends: {newTaskbar.AppListRect.Right} - Tray starts: {newTaskbar.TrayRect.Left} - Total gap: {isFullTest}");
                    if (!settings.IsDynamic || (isFullTest <= taskbars[current].ScaleFactor * 25 && isFullTest > 0 && newTaskbar.TrayRect.Left != 0))
                    {
                        // Add the rect changes to the temporary list of taskbars
                        taskbars[current].TaskbarRect = newTaskbar.TaskbarRect;
                        taskbars[current].AppListRect = newTaskbar.AppListRect;
                        taskbars[current].TrayRect = newTaskbar.TrayRect;
                        Taskbar.UpdateSimpleTaskbar(taskbars[current], settings);
                        //mw.interaction.AddLog($"Updated taskbar {current} simply");
                    }
                    else
                    {
                        if (Taskbar.CheckDynamicUpdateIsValid(taskbars[current], newTaskbar))
                        {
                            //Update directly, old routine did not catch it after merging tray and taskbar
                            taskbars[current].TaskbarRect = newTaskbar.TaskbarRect;
                            taskbars[current].AppListRect = newTaskbar.AppListRect;
                            taskbars[current].TrayRect = newTaskbar.TrayRect;
                            Taskbar.UpdateDynamicTaskbar(taskbars[current], settings);
                        }
                    }
                }
            }
            mw.taskbarDetails = taskbars;
        }
    }
}
