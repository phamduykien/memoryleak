using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace MemoryLeak.Controllers
{
    [Route("api")]
    [ApiController]
    public class DiagnosticsController : ControllerBase
    {
        private static Process _process = Process.GetCurrentProcess();
        private static TimeSpan _oldCPUTime = TimeSpan.Zero;
        private static DateTime _lastMonitorTime = DateTime.UtcNow;
        private static DateTime _lastRpsTime = DateTime.UtcNow;
        private static double _cpu = 0, _rps = 0;
        private static readonly double RefreshRate = TimeSpan.FromSeconds(1).TotalMilliseconds;
        public static long Requests = 0;

        [HttpGet("collect")]
        public ActionResult GetCollect()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            return Ok();
        }

        [HttpGet("diagnostics")]
        public ActionResult GetDiagnostics()
        {
            var now = DateTime.UtcNow;
            _process.Refresh();

            var cpuElapsedTime = now.Subtract(_lastMonitorTime).TotalMilliseconds;

            if (cpuElapsedTime > RefreshRate)
            {
                var newCPUTime = _process.TotalProcessorTime;
                var elapsedCPU = (newCPUTime - _oldCPUTime).TotalMilliseconds;
                _cpu = elapsedCPU * 100 / Environment.ProcessorCount / cpuElapsedTime;

                _lastMonitorTime = now;
                _oldCPUTime = newCPUTime;
            }

            var rpsElapsedTime = now.Subtract(_lastRpsTime).TotalMilliseconds;
            if (rpsElapsedTime > RefreshRate)
            {
                _rps = Requests * 1000 / rpsElapsedTime;
                Interlocked.Exchange(ref Requests, 0);
                _lastRpsTime = now;
            }

            var diagnostics = new
            {
                PID = _process.Id,

                // The memory occupied by objects.
                Allocated = GC.GetTotalMemory(false),

                // The working set includes both shared and private data. The shared data includes the pages that contain all the 
                // instructions that the process executes, including instructions in the process modules and the system libraries.
                WorkingSet = _process.WorkingSet64,

                // The value returned by this property represents the current size of memory used by the process, in bytes, that 
                // cannot be shared with other processes.
                PrivateBytes = _process.PrivateMemorySize64,

                // The number of generation 0 collections
                Gen0 = GC.CollectionCount(0),

                // The number of generation 1 collections
                Gen1 = GC.CollectionCount(1),

                // The number of generation 2 collections
                Gen2 = GC.CollectionCount(2),

                CPU = _cpu,

                RPS = _rps
            };

            return new ObjectResult(diagnostics);
        }

        private static List<Assembly> _recentAsb = new List<Assembly>();

        /// <summary>
        /// Lấy danh sách assembly
        /// </summary>
        /// <returns></returns>
        [HttpGet("assembly")]
        public string GetAssembly()
        {
            _process.Refresh();
            long totalSize = 0;
            StringBuilder bd = new();
            
            bd.AppendLine("Assembly cũ: ");
            foreach (var item in _recentAsb)
            {
                bd.AppendLine(item.FullName);
                try
                {
                    string assemblyPath = item.Location;
                    FileInfo fileInfo = new FileInfo(assemblyPath);
                    long sizeInBytes = fileInfo.Length;
                    totalSize += sizeInBytes;
                }
                catch (Exception)
                {

                }

            }
            bd.AppendLine("Assembly mới: ");
            long newSize = 0;
            foreach (var item in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!_recentAsb.Contains(item))
                {
                    _recentAsb.Add(item);
                    bd.AppendLine(item.FullName);
                    try
                    {
                        string assemblyPath = item.Location;
                        FileInfo fileInfo = new FileInfo(assemblyPath);
                        long sizeInBytes = fileInfo.Length;
                        newSize += sizeInBytes;
                    }
                    catch (Exception)
                    {

                    }
                }
            }
            bd.AppendLine($"Tổng số dll: {AppDomain.CurrentDomain.GetAssemblies().Length}");
            bd.AppendLine($"New dll size {ConvertByteToMB(newSize)}");
            bd.AppendLine($"Total dll size {ConvertByteToMB(totalSize + newSize)}");
            bd.AppendLine($"Heap size {ConvertByteToMB(GC.GetTotalMemory(false))}");
            bd.AppendLine($"Working set size {ConvertByteToMB(_process.WorkingSet64)}");
            bd.AppendLine($"Private bytes {ConvertByteToMB(_process.PrivateMemorySize64)}");
            return bd.ToString();
        }


        private long ConvertByteToMB(long inputBytes)
        {
            return (inputBytes / (1024 * 1024));
        }
    }
}
