using System;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using Microsoft.Web.Administration;
using System.IO;
using System.Timers;


namespace FTP_IP_UPDATE
{
    public partial class UpdateTool : ServiceBase
    {
        private Timer timer1 = null;

        public UpdateTool()
        {
            InitializeComponent();

            // Create the source, if it does not already exist.
            if (!EventLog.SourceExists("FTP Update Tool"))
            {
                //An event log source should not be created and immediately used.
                //There is a latency time to enable the source, it should be created
                //prior to executing the application that uses the source.
                //Execute this sample a second time to use the new source.
                EventLog.CreateEventSource("FTP Update Tool", "XENET");
            }

        }


        protected override void OnStart(string[] args)
        {
            Library.WriteErrorLog("IP Update Service Started");
            Library.WriteEventLog("IP Update Service Started", EventLogEntryType.Information);
            timer1 = new Timer();

            float ServiceUpdateRate = 0;
            if (System.Configuration.ConfigurationManager.AppSettings["ServiceUpdateRate"] == "")
            {
                ServiceUpdateRate = 5;
                Library.WriteErrorLog("ServiceUpdateRate setting is blank, Defaulting to 5.");
                Library.WriteEventLog("ServiceUpdateRate setting is blank, Defaulting to 5.", EventLogEntryType.Warning);
            }
            else if (!float.TryParse(System.Configuration.ConfigurationManager.AppSettings["ServiceUpdateRate"], out ServiceUpdateRate))
            {
                ServiceUpdateRate =  5;
                Library.WriteErrorLog("ServiceUpdateRate needs to be a number! Defaulting to 5.");
                Library.WriteEventLog("ServiceUpdateRate needs to be a number! Defaulting to 5.", EventLogEntryType.Warning);
            }
            else
            {
                ServiceUpdateRate = float.Parse(System.Configuration.ConfigurationManager.AppSettings["ServiceUpdateRate"]);
            }

            this.timer1.Interval = ServiceUpdateRate * 60000;
            this.timer1.Elapsed += new System.Timers.ElapsedEventHandler(this.timer1_tick);
            timer1.Enabled = true;
        }

        private void timer1_tick(object sender, ElapsedEventArgs e)
        {
            Library.UpdateIIS();
        }


        protected override void OnStop()
        {
            Library.WriteErrorLog("IP Update Service Stopped <--!");
            Library.WriteEventLog("IP Update Service Stopped", EventLogEntryType.Warning);
        }
    }

}
