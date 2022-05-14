using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Threading;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RoundedTB
{
    public class WindowListener : System.Windows.Forms.Form
    {


        public MainWindow mw;

        public Thread formthread;
        public WindowListener()
        {
            mw = (MainWindow)System.Windows.Application.Current.MainWindow;
            formthread = new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                System.Windows.Forms.Application.Run(this);
            });
            formthread.Start();
        }

        private bool allowVisible;     // ContextMenu's Show command used
        private bool allowClose;       // ContextMenu's Exit command used

        protected override void SetVisibleCore(bool value)
        {
            if (!allowVisible)
            {
                value = false;
                if (!this.IsHandleCreated) CreateHandle();
            }
            base.SetVisibleCore(value);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!allowClose)
            {
                this.Hide();
                e.Cancel = true;
            }
            base.OnFormClosing(e);
        }

        private void showToolStripMenuItem_Click(object sender, EventArgs e)
        {
            allowVisible = true;
            Show();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            allowClose = true;
            Close();
        }


        public void SendRefreshToTaskbar()
        {
            //sets this app as an icon to the taskbar and removes it directly. This forces the taskbar to refresh and removes the bug when
            //being run in dynamic mode after screen change events... todo find a way to do it in the dynamicrefresh routine and not here.
            //This creates a invisible form that instantly closes after showing.
            //But since it creates an icon in the taskbar, windows updates the whole taskbar and the
            //applist rect from their api gets refreshed, which does not seem to happen normally after getting a window change.
            TBUpdateForm updateForm = new TBUpdateForm();
            updateForm.Show();
 
            
        }

        public void ForceRefreshOfTaskbarRoutine()
        {
            //reset taskbar on display change
            System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                mw.ApplyButton_Click(null, null); //Apply Button click fixes the taskbar after a screen change event or Powerstate change event
                //SendRefreshToTaskbar(); //This refreshes the appbar to keep the tray and apps merged after change events
                //Inform Taskbar that an update is needed
                Taskbar.update_needed_on_dynamic_tb = true;
            }));
            
            
            /*
            // If the monitor connected is big, it kinda seems to be delayed and the click apply button comes first and it the rearanges.
            // so this is just a simple fix first...
            new Thread(() => {
                Thread.Sleep(2000);
                System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    mw.ApplyButton_Click(null, null);
                }));
                SendRefreshToTaskbar();
            }).Start();
            */


        }
        
        protected override void WndProc(ref Message m)
        {
            const int WM_DISPLAYCHANGE = 0x007e;
            switch (m.Msg)
            {
                case WM_DISPLAYCHANGE:
                    ForceRefreshOfTaskbarRoutine();
                    break;
            }
            base.WndProc(ref m);
        }


    }
}
