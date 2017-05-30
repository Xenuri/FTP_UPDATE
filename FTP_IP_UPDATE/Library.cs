using System;
using System.IO;
using Microsoft.Web.Administration;
using System.Diagnostics;
using System.Net;


namespace FTP_IP_UPDATE
{
    public static class Library
    {
        public static void WriteErrorLog(SystemException ex)
        {
            StreamWriter sw = null;
            try
            {
                sw = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "\\Logfile.txt", true);
                sw.WriteLine(DateTime.Now.ToString() + ": " + ex.Message.ToString().Trim());
                sw.Flush();
                sw.Close();
            }

            catch (UnauthorizedAccessException e)
            {
                WriteEventLog(e);
            }
            catch (SystemException e)
            {
                WriteEventLog(e);
                WriteErrorLog(e);
            }
        }

        public static void WriteErrorLog(string Message)
        {
            StreamWriter sw = null;
            try
            {
                sw = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "\\Logfile.txt", true);
                sw.WriteLine(DateTime.Now.ToString() + ": " + Message);
                sw.Flush();
                sw.Close();
            }
            catch (UnauthorizedAccessException e)
            {
                WriteEventLog(e);
            }
            catch (SystemException e)
            {
                WriteEventLog(e);
                WriteErrorLog(e);
            }
        }

        public static void WriteEventLog(string Message, EventLogEntryType type)
        {
            EventLog myLog = new EventLog();
            myLog.Source = "FTP Update Tool";
            myLog.WriteEntry(Message, type);
        }

        public static void WriteEventLog(SystemException ex)
        {
            EventLog myLog = new EventLog();
            myLog.Source = "FTP Update Tool";
            myLog.WriteEntry(ex.Message, EventLogEntryType.Error);
        }

        public static void WriteEventLog(UnauthorizedAccessException ex)
        {
            EventLog myLog = new EventLog();
            myLog.Source = "FTP Update Tool";
            myLog.WriteEntry(ex.Message, EventLogEntryType.Error);
        }
        



        public static void UpdateIIS()
        {
            if (!IsIPNew(GetPubIP(), GetIISIP()))
            {
                using (ServerManager serverManager = new ServerManager())
                {
                    Configuration config = serverManager.GetApplicationHostConfiguration();
                    ConfigurationSection sitesSection = config.GetSection("system.applicationHost/sites");
                    ConfigurationElementCollection sitesCollection = sitesSection.GetCollection();

                    string SiteName = System.Configuration.ConfigurationManager.AppSettings["FTPSiteName"];
                    ConfigurationElement siteElement = FindElement(sitesCollection, "site", "name", SiteName);
                    try
                    {
                        if (siteElement == null) throw new InvalidOperationException("Site '" + SiteName + "' not found! Check Application config.");
                    }
                    catch (InvalidOperationException e)
                    {
                        WriteEventLog(e);
                        WriteErrorLog(e);
                    }
                    ConfigurationElement ftpServerElement = siteElement.GetChildElement("ftpServer");
                    ConfigurationElement firewallSupportElement = ftpServerElement.GetChildElement("firewallSupport");
                    firewallSupportElement["externalIp4Address"] = GetPubIP();
                    serverManager.CommitChanges();

                    WriteErrorLog("Public IP change. Updating IIS with new IP: " + GetIISIP());
                    WriteEventLog("Public IP change. Updating IIS with new IP: " + GetIISIP(), EventLogEntryType.Information);
                }
            }
            else
            {
                WriteEventLog("No IP change", EventLogEntryType.Information);
                WriteErrorLog("No IP change");
            }
        }


        public static string GetPubIP()
        {
            try
            {
                string address = System.Configuration.ConfigurationManager.AppSettings["WebAddressEndpoint"];
                WebRequest request = WebRequest.Create(address);
                using (WebResponse response = request.GetResponse())
                using (StreamReader stream = new StreamReader(response.GetResponseStream()))
                {
                    address = stream.ReadToEnd();
                }

                //Search for the ip in the html
                int first = address.IndexOf("Address: ") + 9;
                int last = address.LastIndexOf("");
                address = address.Substring(first, last - first - 15);
                return address;
            }
            catch (System.Configuration.ConfigurationErrorsException e)
            {
                WriteErrorLog(e);
                WriteEventLog(e);
                return null;
            }
            catch (System.SystemException e)
            {
                WriteErrorLog(e);
                WriteEventLog(e);
                return null;
            }



        }

        public static string GetIISIP()
        {

            ServerManager serverManager = new ServerManager();
            Configuration config = serverManager.GetApplicationHostConfiguration();
            ConfigurationSection sitesSection = config.GetSection("system.applicationHost/sites");
            ConfigurationElementCollection sitesCollection = sitesSection.GetCollection();

            string FTPSiteName = System.Configuration.ConfigurationManager.AppSettings["FTPSiteName"];

            ConfigurationElement siteElement = FindElement(sitesCollection, "site", "name", FTPSiteName);
            try
            {
                if (siteElement == null) throw new InvalidOperationException("Site '" + FTPSiteName + "' not found! Check Application config.");
            }
            catch (InvalidOperationException e)
            {
                WriteEventLog(e);
                WriteErrorLog(e);
            }
            ConfigurationElement ftpServerElement = siteElement.GetChildElement("ftpServer");

            ConfigurationElement firewallSupportElement = ftpServerElement.GetChildElement("firewallSupport");
            string iis_ip = (string)firewallSupportElement.Attributes["externalIp4Address"].Value;
            return iis_ip;
        }
            



        public static ConfigurationElement FindElement(ConfigurationElementCollection collection, string elementTagName, params string[] keyValues)
        {
            foreach (ConfigurationElement element in collection)
            {
                if (String.Equals(element.ElementTagName, elementTagName, StringComparison.OrdinalIgnoreCase))
                {
                    bool matches = true;
                    for (int i = 0; i < keyValues.Length; i += 2)
                    {
                        object o = element.GetAttributeValue(keyValues[i]);
                        string value = null;
                        if (o != null)
                        {
                            value = o.ToString();
                        }
                        if (!String.Equals(value, keyValues[i + 1], StringComparison.OrdinalIgnoreCase))
                        {
                            matches = false;
                            break;
                        }
                    }
                    if (matches)
                    {
                        return element;
                    }
                }
            }
            return null;
        }

        public static bool IsIPNew(string GetPubIP, string GetIISIP)
        {
            bool result = GetPubIP.Equals(GetIISIP, StringComparison.Ordinal);
            return result;
        }



    }
}
