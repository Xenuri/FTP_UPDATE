using System;
using System.IO;
using Microsoft.Web.Administration;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using System.Collections.Generic;


namespace FTP_IP_UPDATE
{
    public static class Library
    {
        // Write to text file, an exception
        public static void WriteErrorLog(SystemException ex)
        {
            StreamWriter sw = null;
            try
            {
                // get value from application config
                sw = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "\\Logfile.txt", true);
                sw.WriteLine(DateTime.Now.ToString() + ": " + ex.Message.ToString().Trim());
                // flush and close stream reader
                sw.Flush();
                sw.Close();
            }

            catch (UnauthorizedAccessException e)
            {
                WriteEventLog(e);
            }
            catch (SystemException e)
            {
                // write any exception to both logs
                WriteEventLog(e);
                WriteErrorLog(e);
            }
        }

        // Write to text file, information 
        public static void WriteErrorLog(string Message)
        {
            StreamWriter sw = null;
            try
            {
                // get value from application config
                sw = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "\\Logfile.txt", true);
                sw.WriteLine(DateTime.Now.ToString() + ": " + Message);
                // flush and close stream reader
                sw.Flush();
                sw.Close();
            }
            catch (UnauthorizedAccessException e)
            {
                WriteEventLog(e);
            }
            catch (SystemException e)
            {
                // write any exception to both logs
                WriteEventLog(e);
                WriteErrorLog(e);
            }
        }

        // overloaded methods for writing to event viewer
        // First method to allow specific message type
        public static void WriteEventLog(string Message, EventLogEntryType type)
        {
            EventLog myLog = new EventLog();
            myLog.Source = "FTP Update Tool";
            myLog.WriteEntry(Message, type);
        }

        // Second method, system exception
        public static void WriteEventLog(SystemException ex)
        {
            EventLog myLog = new EventLog();
            myLog.Source = "FTP Update Tool";
            myLog.WriteEntry(ex.Message, EventLogEntryType.Error);
        }

        // Third method, UnauthorizedAccessException
        public static void WriteEventLog(UnauthorizedAccessException ex)
        {
            EventLog myLog = new EventLog();
            myLog.Source = "FTP Update Tool";
            myLog.WriteEntry(ex.Message, EventLogEntryType.Error);
        }
        


        // Entry point method 
        public static void UpdateIIS()
        {
            // if return values of GetPubIP(), GetIISIP() do NOT match, fetch public IP and Commit change to system.applicationHost XML file
            if (!IsIPNew(GetPubIP(), GetIISIP()))
            {
                using (ServerManager serverManager = new ServerManager())
                {
                    // create Element Collection and grab site name from application config 
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
                    // Select firewallSupportElement attribute -> replace this value with the return value supplied by funcation GetPubIP()
                    ConfigurationElement ftpServerElement = siteElement.GetChildElement("ftpServer");
                    ConfigurationElement firewallSupportElement = ftpServerElement.GetChildElement("firewallSupport");
                    firewallSupportElement["externalIp4Address"] = GetPubIP();
                    // Commit 
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

        // return HTML from remote server and extract IP address from file
        public static string GetPubIP()
        {
            try
            {
                string IP = "";
                List<System.Text.RegularExpressions.Match> IPList = new List<System.Text.RegularExpressions.Match>();
                string address = System.Configuration.ConfigurationManager.AppSettings["WebAddressEndpoint"];
                // Web Request 
                WebRequest request = WebRequest.Create(address);
                using (WebResponse response = request.GetResponse())
                using (StreamReader stream = new StreamReader(response.GetResponseStream()))
                {
                    // Fill address with entire HTML page as a char stream
                    address = stream.ReadToEnd();
                    // Regular Expression to find the IP address in char stream ---->
                    // This should now work with many services that return the public IP such as:
                    //    IPChicken.com         //
                    //    icanhazip.com         // 
                    //    checkip.dyndns.org    //
                    // AVOID USING ANY PAGE WITH MORE THAN ONE ADDRESS AS WE ALWAYS EXPECT ONLY ONE OBJECT IN ELEMENT!!
                    IPList.Add(Regex.Match(address, @"\b(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})\b"));
                    IP = IPList[0].ToString();
                    return IP;
                }

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

        // Function to extract currently set IP address from IIS server
        public static string GetIISIP()
        {
            // init new server manager object
            ServerManager serverManager = new ServerManager();
            // selection application host / sites config
            Configuration config = serverManager.GetApplicationHostConfiguration();
            ConfigurationSection sitesSection = config.GetSection("system.applicationHost/sites");
            ConfigurationElementCollection sitesCollection = sitesSection.GetCollection();

            // get site name from application config
            string FTPSiteName = System.Configuration.ConfigurationManager.AppSettings["FTPSiteName"];

            // using above information, search for XML element 
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

            // now get IP Address from externalIp4Address Attribute
            ConfigurationElement firewallSupportElement = ftpServerElement.GetChildElement("firewallSupport");
            string iis_ip = (string)firewallSupportElement.Attributes["externalIp4Address"].Value;
            // return internal IP as string
            return iis_ip;
        }
            


        // finder function from element traversal
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


        // compare strings, return false if different 
        public static bool IsIPNew(string GetPubIP, string GetIISIP)
        {
            bool result = GetPubIP.Equals(GetIISIP, StringComparison.Ordinal);
            return result;
        }



    }
}
