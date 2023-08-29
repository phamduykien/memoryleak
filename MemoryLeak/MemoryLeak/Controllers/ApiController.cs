using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using MySql.Data.MySqlClient;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Dapper.Contrib.Extensions;
using System.Text;
using System.Reflection;
using System.Collections.Generic;

namespace MemoryLeak.Controllers
{
    [Route("api")]
    [ApiController]
    public class ApiController : ControllerBase
    {
        public ApiController()
        {
            Interlocked.Increment(ref DiagnosticsController.Requests);
        }

        private static ConcurrentBag<string> _staticStrings = new ConcurrentBag<string>();

        [HttpGet("staticstring")]
        public ActionResult<string> GetStaticString()
        {
            var bigString = new String('x', 10 * 1024);
            _staticStrings.Add(bigString);
            return bigString;
        }

        [HttpGet("bigstring")]
        public ActionResult<string> GetBigString()
        {
            return new String('x', 10 * 1024);
        }

        [HttpGet("loh/{size=85000}")]
        public int GetLOH(int size)
        {
            return new byte[size].Length;
        }

        private static readonly string TempPath = Path.GetTempPath();

        [HttpGet("fileprovider")]
        public void GetFileProvider()
        {
            var fp = new PhysicalFileProvider(TempPath);
            fp.Watch("*.*");
        }

        [HttpGet("httpclient1")]
        public async Task<int> GetHttpClient1(string url)
        {
            using (var httpClient = new HttpClient())
            {
                var result = await httpClient.GetAsync(url);
                return (int)result.StatusCode;
            }
        }


        
        private static readonly HttpClient _httpClient = new HttpClient();

        [HttpGet("httpclient2")]
        public async Task<int> GetHttpClient2(string url)
        {
            var result = await _httpClient.GetAsync(url);
            return (int)result.StatusCode;
        }

        [HttpGet("array/{size}")]
        public byte[] GetArray(int size)
        {
            var array = new byte[size];

            var random = new Random();
            random.NextBytes(array);

            return array;
        }

        private static ArrayPool<byte> _arrayPool = ArrayPool<byte>.Create();

        private class PooledArray : IDisposable
        {
            public byte[] Array { get; private set; }

            public PooledArray(int size)
            {
                Array = _arrayPool.Rent(size);
            }

            public void Dispose()
            {
                _arrayPool.Return(Array);
            }
        }

        [HttpGet("pooledarray/{size}")]
        public byte[] GetPooledArray(int size)
        {
            var pooledArray = new PooledArray(size);

            var random = new Random();
            random.NextBytes(pooledArray.Array);

            HttpContext.Response.RegisterForDispose(pooledArray);

            return pooledArray.Array;
        }

        [HttpGet("dapper/notdispose")]
        public object GetDapperNotDispose()
        {
            //Kiểm tra cache dapper và đóng mở connection
            var conn = new MySqlConnection("server=127.0.0.1;uid=root;pwd=Chiakhoaso3#;database=OLand");
            try
            {
                var r = Guid.NewGuid().ToString();
                var sql = "Select '" + r + "' as Ran, O.Email, O.EmailPassword, O.Password From OLandAccount O Limit 500";
                var data = conn.Query(sql);
                return data;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                conn.Clone();
                conn.Dispose();
            }
            return "Ok";
        }
        /// <summary>
        /// https://localhost:44340/api/http/notdispose
        /// </summary>
        /// <returns></returns>
        [HttpGet("http/notdispose")]
        public async Task<object> GetHttpNotDispose()
        {
            //Kiểm tra cache dapper và đóng mở connection
            var link = "https://dantri.com.vn";
            try
            {
                var http = new HttpClient();
                var req = await http.SendAsync(new HttpRequestMessage(HttpMethod.Get, link));
                return await req.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {

            }
            return "Ok";
        }

        [HttpGet("analyze")]
        public string GetAnalyze()
        {
            WorkingSet ws = new WorkingSet();
            var res = ws.Analyze();
            return res;
        }

    }
}
