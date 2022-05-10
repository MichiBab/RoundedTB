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
    public class TBUpdateForm : System.Windows.Forms.Form
    {

        public TBUpdateForm()
        {
            this.Load += new EventHandler(Form1_Load);
            this.Shown += TBUpdateForm_Shown;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.ShowInTaskbar = true;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.Size = new System.Drawing.Size(0, 0);
        }
        private void TBUpdateForm_Shown(Object sender, EventArgs e)
            {
                this.Close();
            }


    }
}
