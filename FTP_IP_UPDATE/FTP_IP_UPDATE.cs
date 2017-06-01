using System.Diagnostics;
using System.ServiceProcess;
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
                EventLog.CreateEventSource("FTP Update Tool", "XENET");
            }

        }

        
        protected override void OnStart(string[] args)
        {
            // Send start message to text log and event viewer 
            Library.WriteErrorLog("IP Update Service Started");
            Library.WriteEventLog("IP Update Service Started", EventLogEntryType.Information);
            // init timer
            timer1 = new Timer();

            // Set service update rate initially to zero, later on use value set in application config to define this value
            float ServiceUpdateRate = 0;
            // Check values in application config are true
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
                // No issue, parse update rate value
                ServiceUpdateRate = float.Parse(System.Configuration.ConfigurationManager.AppSettings["ServiceUpdateRate"]);
            }

            // setup service timer loop, multiply value by 60000(ms)
            this.timer1.Interval = ServiceUpdateRate * 60000;
            this.timer1.Elapsed += new System.Timers.ElapsedEventHandler(this.timer1_tick);
            // start timer
            timer1.Enabled = true;
        }

        // Each timer tick -> run Update method 
        private void timer1_tick(object sender, ElapsedEventArgs e)
        {
            Library.UpdateIIS();
        }

        // Send to text file and even viewer service stop
        protected override void OnStop()
        {
            Library.WriteErrorLog("IP Update Service Stopped <--!");
            Library.WriteEventLog("IP Update Service Stopped", EventLogEntryType.Warning);
        }
    }

}
