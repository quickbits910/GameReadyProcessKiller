using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;

namespace GameReadyProcessKiller
{
    public interface IProcessWrapper
    {
        int Id { get; }
        string ProcessName { get; }
        DateTime StartTime { get; }
        void Kill();
    }

    public interface IProcessProvider
    {
        IEnumerable<IProcessWrapper> GetProcessesByName(string processName);
        string GetProcessOwner(int processId);
    }

    public class ProcessWrapper : IProcessWrapper
    {
        private readonly Process _process;

        public ProcessWrapper(Process process)
        {
            _process = process;
        }

        public int Id => _process.Id;
        public string ProcessName => _process.ProcessName;
        public DateTime StartTime => _process.StartTime;
        public void Kill() => _process.Kill();
    }

    public class SystemProcessProvider : IProcessProvider
    {
        public IEnumerable<IProcessWrapper> GetProcessesByName(string processName)
        {
            var processes = Process.GetProcessesByName(processName);
            return processes.Select(p => new ProcessWrapper(p));
        }

        public string GetProcessOwner(int processId)
        {
            try
            {
                string query = $"Select * From Win32_Process Where ProcessID = {processId}";
                using (var searcher = new ManagementObjectSearcher(query))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string[] ownerInfo = new string[2];
                        int ret = Convert.ToInt32(obj.InvokeMethod("GetOwner", ownerInfo));
                        if (ret == 0)
                        {
                            return $"{ownerInfo[1]}\\{ownerInfo[0]}";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get owner for PID {processId}: {ex.Message}");
            }
            return "NO OWNER";
        }
    }

    public partial class ProcessHelper
    {
        private readonly IProcessProvider _processProvider;

        public ProcessHelper(IProcessProvider processProvider)
        {
            _processProvider = processProvider ?? throw new ArgumentNullException(nameof(processProvider));
        }

        public virtual void KillProcessByNameAndUserWithTimeout(string processName, string processUserName, int startedForHours = 0)
        {
            if (string.IsNullOrWhiteSpace(processName))
            {
                Console.WriteLine("Process name is empty or invalid.");
                return;
            }

            var foundProcesses = _processProvider.GetProcessesByName(processName).ToList();
            if (!foundProcesses.Any())
            {
                Console.WriteLine($"No processes found with name '{processName}'.");
                return;
            }

            Console.WriteLine($"{foundProcesses.Count} processes found with name '{processName}'.");

            foreach (var process in foundProcesses)
            {
                try
                {
                    string owner = _processProvider.GetProcessOwner(process.Id);
                    bool userMatch = processUserName.Equals("all", StringComparison.OrdinalIgnoreCase) ||
                                   owner.Equals(processUserName, StringComparison.OrdinalIgnoreCase);

                    bool timeExpired = startedForHours == 0 || 
                                     (DateTime.Now - process.StartTime).TotalHours >= startedForHours;

                    if (userMatch && timeExpired)
                    {
                        process.Kill();
                        Console.WriteLine($"Process {process.Id} killed.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing PID {process.Id}: {ex.Message}");
                }
            }
        }
    }
}