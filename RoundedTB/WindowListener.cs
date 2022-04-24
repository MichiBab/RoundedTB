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
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.Load += new EventHandler(WindowListener_Load);
            //Run as form
            this.Visible = false;
            this.Hide();

            formthread = new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                System.Windows.Forms.Application.Run(this);
            });
            formthread.Start();
        }
        void WindowListener_Load(object sender, EventArgs e)
        {
            this.Size = new System.Drawing.Size(0, 0);
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
