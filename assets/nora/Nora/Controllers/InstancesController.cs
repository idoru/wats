﻿using System.IO.MemoryMappedFiles;
using System.Web.UI.WebControls;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Nora.helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using System.Web.Http.Results;

namespace nora.Controllers
{
    public class InstancesController : ApiController
    {
        private static Services services;

        static InstancesController()
        {
            var env = Environment.GetEnvironmentVariable("VCAP_SERVICES");
            if (env != null)
            {
                services = JsonConvert.DeserializeObject<Services>(env);
            }
        }

        [Route("~/")]
        [HttpGet]
        public IHttpActionResult Root()
        {
            return Ok(String.Format("hello i am nora running on {0}", Request.RequestUri.AbsoluteUri));
        }

        [Route("~/headers")]
        [HttpGet]
        public IHttpActionResult Headers()
        {
            return Ok(Request.Headers);
        }

        [Route("~/print/{output}")]
        [HttpGet]
        public IHttpActionResult Print(string output)
        {
            System.Console.WriteLine(output);
            return Ok(Request.Headers);
        }

        [Route("~/id")]
        [HttpGet]
        public IHttpActionResult Id()
        {
            var uuid = Environment.GetEnvironmentVariable("INSTANCE_GUID");
            return Ok(uuid);
        }

        [Route("~/env")]
        [HttpGet]
        public IHttpActionResult Env()
        {
            return Ok(Environment.GetEnvironmentVariables());
        }

        [Route("~/curl/{host}/{port}")]
        [HttpGet]
        public IHttpActionResult Curl(string host, int port)
        {
            var req = WebRequest.Create("http://" + host + ":" + port);
            req.Timeout = 1000;
            try
            {
                var resp = (HttpWebResponse)req.GetResponse();
                return Json(new
                {
                    stdout = new StreamReader(resp.GetResponseStream()).ReadToEnd(),
                    return_code = 0,
                });
            }
            catch (WebException ex)
            {
                return Json(new
                {
                    stderr = ex.Message,
                    // ex.Response != null if the response status code wasn't a success, 
                    // null if the operation timedout
                    return_code = ex.Response != null ? 0 : 1,
                });
            }
        }

        [Route("~/env/{name}")]
        [HttpGet]
        public IHttpActionResult EnvName(string name)
        {
            return Ok(Environment.GetEnvironmentVariable(name));
        }

        [Route("~/users")]
        [HttpGet]
        public IHttpActionResult CupsUsers()
        {
            if (services.UserProvided.Count == 0)
            {
                var msg = new HttpResponseMessage();
                msg.StatusCode = HttpStatusCode.NotFound;
                msg.Content = new StringContent("No services found");
                return new ResponseMessageResult(msg);
            }

            var service = services.UserProvided[0];

            var users = UsersFromService(service);
            return Ok(users);
        }

        [Route("~/pmysql")]
        [HttpGet]
        public IHttpActionResult PMysqlUsers()
        {
            if (services.PMySQL.Count == 0)
            {
                var msg = new HttpResponseMessage();
                msg.StatusCode = HttpStatusCode.NotFound;
                msg.Content = new StringContent("No services found");
                return new ResponseMessageResult(msg);
            }

            var service = services.PMySQL[0];

            var users = UsersFromService(service);

            return Ok(users);
        }

        [Route("~/mmapleak")]
        [HttpGet]
        public IHttpActionResult MmapLeak()
        {
            Process.Start(Path.Combine(HttpContext.Current.Request.MapPath("~/bin"), "mmapleak.exe"));
            return Ok();
        }

        [Route("~/breakout/{subprocess?}")]
        [HttpGet]
        public IHttpActionResult Breakout(string subprocess = "notepad")
        {
            var st = new ProcessStartInfo();
            
            var workingDir = HttpContext.Current.Request.MapPath("~/bin");
            st.FileName = Path.Combine(HttpContext.Current.Request.MapPath("~/bin"), "JobBreakoutTest.exe");
            st.Arguments = String.Concat("powershell -c cd ", workingDir, " ; ", subprocess);
            Process.Start(st);

            return Ok();
        }

        [Route("~/breakoutbomb")]
        [HttpGet]
        public IHttpActionResult BreakoutBomb()
        {
            var st = new ProcessStartInfo();

            st.FileName = Path.Combine(HttpContext.Current.Request.MapPath("~/bin"), "BreakoutBomb.exe");
            Process.Start(st);

            return Ok();
        }

        [Route("~/commitcharge")]
        [HttpGet]
        public IHttpActionResult GetCommitCharge()
        {
            var p = new PerformanceCounter("Memory", "Committed Bytes");
            return Ok(p.RawValue);
        }

        private static MemoryMappedFile MmapFile = null;

        [Route("~/mmapleak/{maxbytes}")]
        [HttpGet]
        public IHttpActionResult MmapLeakMax(long maxbytes)
        {
            if (MmapFile != null)
            {
                MmapFile.Dispose();
            }

            MmapFile = MemoryMappedFile.CreateNew(
                Guid.NewGuid().ToString(),
                maxbytes,
                MemoryMappedFileAccess.ReadWrite);

            return Ok();
        }

        [Route("~/exit")]
        [HttpGet]
        public IHttpActionResult Exit()
        {
            Process.GetCurrentProcess().Kill();
            return Ok();
        }


        private static List<string> UsersFromService(Service service)
        {
            var creds = service.Credentials;
            var username = creds["username"];
            var password = creds["password"];
            var host = creds.ContainsKey("host") ? creds["host"] : creds["hostname"];
            var dbname = creds.ContainsKey("name") ? creds["name"] : "mysql";
            var connString = String.Format("server={0};uid={1};pwd={2};database={3}", host, username, password, dbname);

            Console.WriteLine("Connecting to mysql using {0}", connString);

            var users = new List<string>();

            using (var conn = new MySqlConnection())
            {
                conn.ConnectionString = connString;
                conn.Open();

                new MySqlCommand(
                    "CREATE TABLE IF NOT EXISTS Hits(Id INT PRIMARY KEY AUTO_INCREMENT, CreatedAt DATETIME) ENGINE=INNODB;", conn)
                    .ExecuteNonQuery();

                new MySqlCommand(
                    "INSERT INTO Hits(CreatedAt)VALUES(now());", conn)
                    .ExecuteNonQuery();

                using (var cmd = new MySqlCommand("select CreatedAt from Hits order by id desc limit 10", conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        var colIdx = reader.GetOrdinal("CreatedAt");
                        while (reader.Read())
                        {
                            users.Add(reader.GetString(colIdx));
                        }
                    }
                }
            }
            return users;
        }
    }
}