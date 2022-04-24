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
        string m = "";

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

        protected override void WndProc(ref Message m)
        {
            const int WM_DISPLAYCHANGE = 0x007e;
            switch (m.Msg)
            {
                case WM_DISPLAYCHANGE:
                    //reset taskbar on display change
                    System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        mw.ApplyButton_Click(null, null);
                    }));
                    break;
            }
            base.WndProc(ref m);
        }


    }
}
