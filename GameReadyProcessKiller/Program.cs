using System;
using System.Configuration;
using System.Diagnostics;
using System.Management;
using System.Xml;

namespace GameReadyProcessKiller
{
    public partial class ProcessHelper
    {
        public string GetProcessOwner(int processId)
        {
            try
            {
                string query = $"Select * From Win32_Process Where ProcessID = {processId}";
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string[] ownerInfo = new string[2];
                        int ret = Convert.ToInt32(obj.InvokeMethod("GetOwner", ownerInfo));
                        if (ret == 0)
                        {
                            return $"{ownerInfo[1]}\\{ownerInfo[0]}"; // DOMAIN\user
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log or handle error retrieving owner
                Console.WriteLine($"Failed to get owner for PID {processId}: {ex.Message}");
            }
            return "NO OWNER";
        }

        public void KillProcessByNameAndUser(string processName, string processUserName, int startedForHours = 0)
        {
            if (string.IsNullOrWhiteSpace(processName))
            {
                Console.WriteLine("Process name is empty or invalid.");
                return;
            }

            var foundProcesses = Process.GetProcessesByName(processName);
            if (foundProcesses.Length == 0)
            {
                Console.WriteLine($"No processes found with name '{processName}'.");
                return;
            }

            Console.WriteLine($"{foundProcesses.Length} processes found with name '{processName}'.");

            foreach (var process in foundProcesses)
            {
                try
                {
                    string owner = GetProcessOwner(process.Id);
                    bool userMatch = processUserName.Equals("all", StringComparison.OrdinalIgnoreCase) ||
                                     owner.Equals(processUserName, StringComparison.OrdinalIgnoreCase);

                    bool timeExpired = startedForHours == 0 || (DateTime.Now - process.StartTime).TotalHours >= startedForHours;

                    Console.WriteLine($"Process: {process.ProcessName} | PID: {process.Id} | Owner: {owner} | Start Time: {process.StartTime}");

                    if (userMatch && timeExpired)
                    {
                        process.Kill();
                        Console.WriteLine($"Process {process.Id} killed.");
                    }
                    else
                    {
                        Console.WriteLine($"Process {process.Id} not killed due to filtering criteria.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing PID {process.Id}: {ex.Message}");
                }
            }
        }
    }

    class Program
    {
        static void Main()
        {
            Console.WriteLine("Killing processes loaded from app.config");

            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var section = config.GetSection("GameProcessPurge");

                if (section == null)
                {
                    Console.WriteLine("Config section 'GameProcessPurge' not found.");
                    return;
                }

                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(section.SectionInformation.GetRawXml());

                var settingsList = xmlDoc.GetElementsByTagName("Settings");
                if (settingsList.Count == 0)
                {
                    Console.WriteLine("No process settings found in configuration.");
                    return;
                }

                var processHelper = new GameReadyProcessKiller.ProcessHelper(new SystemProcessProvider());

                foreach (XmlNode node in settingsList)
                {
                    var processName = node.Attributes?["processname"]?.Value;
                    var autoKillUser = node.Attributes?["autokilluser"]?.Value ?? "all";

                    if (string.IsNullOrWhiteSpace(processName))
                    {
                        Console.WriteLine("Process name attribute missing or empty in configuration.");
                        continue;
                    }

                    Console.WriteLine($"Processing process '{processName}', auto kill user '{autoKillUser}'.");

                    processHelper.KillProcessByNameAndUser(processName, autoKillUser);
                }
            }
            catch (ConfigurationErrorsException ex)
            {
                Console.WriteLine($"Error reading configuration: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
            }

            Console.WriteLine("Press ENTER to exit.");
            Console.ReadLine();
        }
    }
}