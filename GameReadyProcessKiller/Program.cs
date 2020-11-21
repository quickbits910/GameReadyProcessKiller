using System;
using System.Collections.Specialized;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Xml;

namespace GameReadyProcessKiller
{

    public class ProcessHelper
    {

        public string GetProcessOwner(int processId)
        {
            string query = "Select * From Win32_Process Where ProcessID = " + processId;
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);
            ManagementObjectCollection processList = searcher.Get();

            foreach (ManagementObject obj in processList)
            {
                string[] argList = new string[] { string.Empty, string.Empty };
                int returnVal = Convert.ToInt32(obj.InvokeMethod("GetOwner", argList));
                if (returnVal == 0)
                {
                    return argList[1] + "\\" + argList[0];   // return DOMAIN\user
                }
            }
            return "NO OWNER";
        }

        /// <summary>
        /// Kill processes if they meet the parameter values of process name, owner name, expired started times.
        /// </summary>
        /// <param name="ProcessName">Process Name, case sensitive, for emample "EXCEL" could not be "excel"</param>
        /// <param name="ProcessUserName">Owner name or user name of the process, case sensitive</param>
        /// <param name="HasStartedForHours">if process has started for more than n (parameter input) hours. 0 means regardless how many hours ago</param>
        public void KillProcessByNameAndUser(string ProcessName, string ProcessUserName, int HasStartedForHours)
        {
            Process[] foundProcesses = Process.GetProcessesByName(ProcessName);
            Console.WriteLine(foundProcesses.Length.ToString() + " processes found.");
            string strMessage = string.Empty;
            foreach (Process p in foundProcesses)
            {
                try 
                { 
                string UserName = GetProcessOwner(p.Id);
                strMessage = string.Format("Process Name: {0} | Process ID: {1} | User Name : {2} | StartTime {3}",
                                                 p.ProcessName, p.Id.ToString(), UserName, p.StartTime.ToString());
                //Console.WriteLine(strMessage);
                bool TimeExpired = (p.StartTime.AddHours(HasStartedForHours) < DateTime.Now) || HasStartedForHours == 0;
                bool PrcoessUserName_Is_Matched = UserName.Equals(ProcessUserName);

                if ((ProcessUserName.ToLower() == "all" && TimeExpired) ||
                     PrcoessUserName_Is_Matched && TimeExpired)
                {
                    p.Kill();
                    Console.WriteLine("Process ID " + p.Id.ToString() + " is killed.");
                }
                }
                catch (Exception ex) 
                {
                    Console.WriteLine(ex.Message.ToString());
                }
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Killing processes loaded from app.config");

            var map = new ExeConfigurationFileMap
            {
                ExeConfigFilename = @"App.config"
            };
            var configuration = ConfigurationManager.OpenMappedExeConfiguration(map, ConfigurationUserLevel.None);
            
            var myParamsSection = configuration.GetSection("GameProcessPurge");

            var rawXml = myParamsSection.SectionInformation.GetRawXml();
 
            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(rawXml);
            XmlNodeList elemList = xmlDocument.GetElementsByTagName("Settings");

            for (int i = 0; i < elemList.Count; i++)
            {
                string attrVal = elemList[i].Attributes["processname"].Value;
                string attrValOwner = elemList[i].Attributes["autokilluser"].Value;
                Console.WriteLine(attrVal);
                var ph = new ProcessHelper();
                ph.KillProcessByNameAndUser(attrVal, attrValOwner, 0);
            }

            Console.ReadLine();
        }
    }
}
