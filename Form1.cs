/*    
 * IIS Tuner 1.1
 * Copyright 2011. PokeIn http://www.pokein.com
 * 
 * This library is free software; you can redistribute it and/or modify it under the terms of the GNU Lesser 
 * General Public License (LGPL) as published by the Free Software Foundation; either version 2.1 of the License, 
 * or (at your option) any later version. 
 * This library is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the
 * implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Lesser General Public 
 * License for more details.
 * 
 * You should have received a copy of the GNU Lesser General Public License along with this library; if not, 
 * write to the Free Software Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307 USA  
*/
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32; 

namespace IISTuner
{
    public partial class Form1 : Form
    {  
        public Form1()
        { 
            InitializeComponent(); 
        } 

        internal string[] SetRegistryEntry(Dictionary<string, int> entries)
        {
            List<string> cmds = new List<string>();

            foreach (string k in entries.Keys)
            {
                string position = k.Split(':')[0];
                string parameter = k.Split(':')[1];
                int value = Convert.ToInt32(entries[k]);

                //reg add HKLM\System\CurrentControlSet\Services\TcpIp\Parameters /v TcpTimedWaitDelay /t REG_DWORD /d 30 /f
                cmds.Add("reg add HKLM\\" + position + " /v " + parameter + " /t REG_DWORD /d "+value.ToString()+" /f");
            }

            return cmds.ToArray();
        } 

        internal void Tune()
        {
            Dictionary<string, int> cmds = new Dictionary<string, int>();
            cmds.Add("System\\CurrentControlSet\\Services\\HTTP\\Parameters:EnableAggressiveMemoryUsage", 1);//1
            cmds.Add("System\\CurrentControlSet\\Services\\HTTP\\Parameters:EnableCopySend", 1);//1
            cmds.Add("System\\CurrentControlSet\\Services\\TcpIp\\Parameters:TcpTimedWaitDelay", 30);//30
            cmds.Add("System\\CurrentControlSet\\Services\\HTTP\\Parameters:MaxConnections", 65535);//65535

            if (Loading.Net2Exist)
            {
                cmds.Add("SOFTWARE\\Microsoft\\ASP.NET\\2.0.50727.0:MaxConcurrentRequestsPerCPU", 0);//0
                cmds.Add("SOFTWARE\\Microsoft\\ASP.NET\\2.0.50727.0:MaxConcurrentThreadsPerCPU", 0);//0 
            }

            if (Loading.Net4Exist)
            {
                cmds.Add("SOFTWARE\\Microsoft\\ASP.NET\\4.0.30319.0:MaxConcurrentRequestsPerCPU", 0);//0
                cmds.Add("SOFTWARE\\Microsoft\\ASP.NET\\4.0.30319.0:MaxConcurrentThreadsPerCPU", 0);//0
            }

            string [] cmd = SetRegistryEntry(cmds);
            RunMSDOS(cmd);
        }

        internal void CreateIIS7Pool()
        {
            RunMSDOS(new string[]{
                Loading.InetDir + "\\appcmd stop apppool PokeIn", 
            	Loading.InetDir + "\\appcmd add apppool /name:PokeIn /managedPipelineMode:Integrated",
                Loading.InetDir + "\\appcmd set apppool PokeIn /queueLength:65535",
                Loading.InetDir + "\\appcmd set config /section:processModel /autoConfig:false /commit:MACHINE",
                Loading.InetDir + "\\appcmd set apppool PokeIn /autoStart:true",
                Loading.InetDir + "\\appcmd set apppool PokeIn /enable32BitAppOnWin64:true",
                Loading.InetDir + "\\appcmd set apppool PokeIn /startMode:AlwaysRunning",
                Loading.InetDir + "\\appcmd set apppool PokeIn /processModel.shutdownTimeLimit:110",
                Loading.InetDir + "\\appcmd set config /section:processModel /maxWorkerThreads:100 /commit:MACHINE",
                Loading.InetDir + "\\appcmd set config /section:processModel /maxIoThreads:100 /commit:MACHINE",
                Loading.InetDir + "\\appcmd set config /section:processModel /minWorkerThreads:100 /commit:MACHINE",
                Loading.InetDir + "\\appcmd set config /section:processModel /requestQueueLimit:Infinite /commit:MACHINE",
                Loading.InetDir + "\\appcmd set config /section:serverRuntime /appConcurrentRequestLimit:65535",
                Loading.InetDir + "\\appcmd start apppool PokeIn"
            });
        }

        bool _processWorking = false; 
        internal void RunMSDOS(string [] cmds)
        {
            if (_processWorking)
                return;
              
            btnTune.Enabled = false;

            _processWorking = true; 

            ProcessStartInfo psi = new ProcessStartInfo("cmd.exe")
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            Process msdos = new Process { StartInfo = psi, EnableRaisingEvents = true };

            msdos.Exited += new EventHandler(MSDOSExited);
            msdos.Start();
            foreach (string cmd in cmds)
                msdos.StandardInput.WriteLine(cmd); 


            msdos.StandardInput.WriteLine("exit");

            WaitMSDOS(); 
            btnTune.Enabled = true; 
        }

        internal void WaitMSDOS()
        {
            while (true)
            {
                if (!_processWorking)
                    break;
                Thread.Sleep(100);
                Application.DoEvents();
            }
        }

        void MSDOSExited(object sender, EventArgs e)
        {
            _processWorking = false; 
        } 

        static bool TuneMachineConfig()
        {
            List<string> frameworks = new List<string>();
            if (Loading.Net2Exist) frameworks.Add(Loading.Net2Dir + "\\Config\\");
            if (Loading.Net4Exist) frameworks.Add(Loading.Net4Dir + "\\Config\\");
            if (Loading.Net264Exist) frameworks.Add(Loading.Net264Dir + "\\Config\\");
            if (Loading.Net464Exist) frameworks.Add(Loading.Net464Dir + "\\Config\\");

            foreach (string dir in frameworks)
            {
                try
                {
                    File.Copy(dir + "machine.config", dir + "machine.config.bak." + DateTime.Now.ToFileTimeUtc().ToString());
                }
                catch
                {
                    MessageBox.Show(Loading.AdminMessage);
                    return false;
                }
                StreamReader rd = new StreamReader(dir + "machine.config", Encoding.Default);
                string config = rd.ReadToEnd();
                rd.Close();

                int posweb = config.IndexOf("<system.web>");
                if (posweb > 0)
                {
                    posweb = config.IndexOf("<processModel", posweb);
                    int endpos = config.IndexOf("/>", posweb);
                    string left = config.Substring(0, posweb);
                    string right = config.Substring(endpos + 2, config.Length - (endpos + 2));
                    config = left 
                        + "<processModel autoConfig=\"true\" minIoThreads=\"30\" maxWorkerThreads=\"100\" maxIoThreads=\"100\" minWorkerThreads=\"100\" requestQueueLimit=\"Infinite\" />"
                        + right;

                    StreamWriter wr = new StreamWriter(dir + "machine.config", false);
                    wr.Write(config);
                    wr.Close();
                }
            }

            frameworks.Clear();

            return true;
        }

        static bool TuneASPNETConfig()
        {
            List<string> frameworks = new List<string>();
            if (Loading.Net2Exist) frameworks.Add(Loading.Net2Dir + "\\");
            if (Loading.Net4Exist) frameworks.Add(Loading.Net4Dir + "\\");
            if (Loading.Net264Exist) frameworks.Add(Loading.Net264Dir + "\\");
            if (Loading.Net464Exist) frameworks.Add(Loading.Net464Dir + "\\");

            foreach (string dir in frameworks)
            {
                try
                {
                    File.Copy(dir + "Aspnet.config", dir + "Aspnet.config.bak." + DateTime.Now.ToFileTimeUtc().ToString());
                }
                catch
                {
                    MessageBox.Show(Loading.AdminMessage);
                    return false;
                }
                StreamReader rd = new StreamReader(dir + "Aspnet.config", Encoding.Default);
                string config = rd.ReadToEnd();
                rd.Close();

                bool found = config.Contains("</configuration>");
                found = found && !config.Contains("maxConcurrentRequestsPerCPU");
                if (found)
                {
                    if (!config.Contains("</system.web>"))
                        config = config.Replace("</configuration>", "<system.web><applicationPool maxConcurrentRequestsPerCPU=\"5000\" maxConcurrentThreadsPerCPU=\"0\" requestQueueLimit=\"25000\" /></system.web></configuration>");
                    else
                        config = config.Replace("</system.web>", "<applicationPool maxConcurrentRequestsPerCPU=\"5000\" maxConcurrentThreadsPerCPU=\"0\" requestQueueLimit=\"25000\" /></system.web>");

                    StreamWriter wr = new StreamWriter(dir + "Aspnet.config", false);
                    wr.Write(config);
                    wr.Close();
                }
            }

            frameworks.Clear();

            return true;
        }

        private void BtnTuneClick(object sender, EventArgs e)
        {

            if (!TuneMachineConfig() || !TuneASPNETConfig())
            {
                this.Close();
                return;
            }

            string iisNotes;

            if (Loading.IISVersion == 7)
            {
                CreateIIS7Pool();
                iisNotes = "Optimized IIS Application pool PokeIn is created.\nPlease move your web application onto PokeIn app. pool on IIS to receive optimization benefits.";
            }
            else
            {
                iisNotes = "Open properties page of your IIS6 application pool and remove 'request que limit'";
            }

            Tune(); 

            MessageBox.Show("You environment is optimized for performance!\nTCP, HTTP, ASP.NET Registry settings optimized.\nMachine.config file optimized and backed up\n\n"+iisNotes+"\n\nYou must restart this computer to activate the changes.", "READ CAREFULLY!"); 
        }


        private void LinkLabel1LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("http://pokein.com");
        } 
    }
}
