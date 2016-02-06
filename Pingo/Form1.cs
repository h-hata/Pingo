using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Threading;

namespace Pingo
{

    
    public partial class Pingo : Form
    {
        delegate void DelegateUpdateControl(int sq, uint delay);
        HPing ping;
        Thread send_thread=null;
        Thread recv_thread=null;

        private void UpdateControl(int sq, uint delay)
        {
            int level = sq % 100;
            progressBar1.Value = level;
            textBox4.Text = sq.ToString();
        }



        private void UpdatePacketRecv(int ret_code, int sq, uint delay)
        {
            int level;
            level = sq % 100;
            //progressBar1.Value = level;
            if (textBox4.InvokeRequired == true)
            {
                DelegateUpdateControl dele = new DelegateUpdateControl(UpdateControl);
                //object[] param = {ret_code,sq,delay};
                object[] param = { sq, delay };
                Invoke(dele, param);
            }
        }
        public Pingo()
        {
            InitializeComponent();
        }
        
        private void start_button2_Click(object sender, EventArgs e)
        {
            if (ping == null) return;
            start_button2.Enabled = false;
            stop_button3.Enabled = true;
            set_button1.Enabled = false;
            textBox1.Enabled = false;
            send_thread = new Thread(this.PingSend);
            recv_thread = new Thread(this.PingRecv);
            recv_thread.Start();
            send_thread.Start();
        }
        private void set_button1_Click(object sender, EventArgs e)
        {
            string hostname = textBox1.Text;
            ping = HPing.ObjectFactory(textBox1.Text);
            if (ping != null)
            {
                textBox2.Text = ping.GetTargetAddress();
            }
            else
            {
                textBox2.Text = "";
            }
        }
        private void PingSend()
        {
            for (;;)
            {
                ping.Send();
                System.Threading.Thread.Sleep(93);
            }
        }
        private void PingRecv()
        {
            int sq=0;
            uint delay=0;
            int ret_code;
            for ( ; ; )
            {
                ret_code=ping.Recv(ref sq, ref delay);
                UpdatePacketRecv(ret_code, sq, delay);
            }
        }

        private void stop_button3_Click(object sender, EventArgs e)
        {
            if (send_thread != null)
            {
                send_thread.Abort();
                send_thread.Join();
                send_thread = null;
            }
            if (recv_thread != null)
            {
                recv_thread.Abort();
                recv_thread.Join();
                recv_thread = null;
            }    
            start_button2.Enabled = true;
            stop_button3.Enabled = false;
            set_button1.Enabled = true;
            textBox1.Enabled = true;
        }
    }
    class RecvPacket
    {
        public int sq;
        public int delay;
        public RecvPacket(int psq, int pdelay)
        {
            sq = psq;
            delay = pdelay;
        }
    }
    static class Program
    {
        /// <summary>
        /// アプリケーションのメイン エントリ ポイントです。
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Pingo());
        }
    }

}