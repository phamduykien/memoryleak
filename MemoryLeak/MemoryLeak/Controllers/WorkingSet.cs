using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MemoryLeak.Controllers
{
    public class WorkingSet
    {

        private StringBuilder _result = new();

        [DllImport("psapi.dll")]
        public static extern bool GetProcessMemoryInfo(IntPtr hProcess, out PROCESS_MEMORY_COUNTERS counters, int size);

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_MEMORY_COUNTERS
        {
            public uint cb;
            public uint PageFaultCount;
            public IntPtr PeakWorkingSetSize;
            public IntPtr WorkingSetSize;
            public IntPtr QuotaPeakPagedPoolUsage;
            public IntPtr QuotaPagedPoolUsage;
            public IntPtr QuotaPeakNonPagedPoolUsage;
            public IntPtr QuotaNonPagedPoolUsage;
            public IntPtr PagefileUsage;
            public IntPtr PeakPagefileUsage;
        }

        public string Analyze()
        {
            _result.Clear();
            Process process = Process.GetCurrentProcess();
            AddText($"Thông tin về tiến trình: {process.ProcessName} (ID: {process.Id})");


            AddText("Các module được tải vào tiến trình:");
            foreach (ProcessModule module in process.Modules)
            {
                AddText($"Module: {module.ModuleName}");
                AddText($"   Đường dẫn: {module.FileName}");

                IntPtr moduleHandle = LoadLibrary(module.FileName);
                PROCESS_MEMORY_COUNTERS memoryCounters;

                if (GetProcessMemoryInfo(process.Handle, out memoryCounters, Marshal.SizeOf(typeof(PROCESS_MEMORY_COUNTERS))))
                {
                    long workingSetSize = memoryCounters.WorkingSetSize.ToInt64();
                    AddText($"   Dung lượng bộ nhớ được sử dụng: {workingSetSize} bytes");
                    AddText($"   Dung lượng bộ nhớ được sử dụng: {workingSetSize / (1024 * 1024)} MB");
                }
                else
                {
                    AddText("   Không thể truy cập thông tin bộ nhớ.");
                }

                FreeLibrary(moduleHandle);
            }
            return _result.ToString();
        }

        private void AddText(string text)
        {
            _result.AppendLine(text);
        }


        [DllImport("kernel32.dll")]
        public static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll")]
        public static extern bool FreeLibrary(IntPtr hModule);
    }

}

