using Newtonsoft.Json.Linq;
using Octokit;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using YMTCORE;

namespace YMT
{
    public class Program
    {
        static object m_lock = new object();
        public const string RELEASE_FILE = "YMT_Console.zip";
        public const string RELEASE_FILENAME = "YMT_Console";

        static void Main(string[] args)
        {
            Console.WriteLine("Server : S, Client : C, Update : U");
            switch (Console.ReadLine().ToUpper())
            {
                case "S":
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    ProcServer();
                    break;
                case "C":
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    ProcClient();
                    break;
                case "U":
                    Console.ForegroundColor = ConsoleColor.Green;
                    {
                        string dest_dir = $"{Path.Combine(Environment.CurrentDirectory, "UpdateTemp")}";
                        if (!Directory.Exists(dest_dir))
                        {
                            Directory.CreateDirectory(dest_dir);
                        }

                        if (!DownloadLatestReleaseAsync(@"https://github.com/TANAKADOREI/YoutubeMusicTogether", dest_dir).Result)
                        {
                            Console.WriteLine("error");
                        }

                        ExtractZipFile(Path.Combine(dest_dir, RELEASE_FILE));
                        string[] new_files = Directory.GetFiles(Path.Combine(dest_dir, "net6.0"));

                        {
                            Process process= new Process();
                            process.StartInfo.FileName = "YMTUPDATER";
                            process.StartInfo.UseShellExecute = true;
                            process.Start();
                        }
                    }
                    break;
            }
        }

        public static void ExtractZipFile(string zipFilePath)
        {
            ZipFile.ExtractToDirectory(zipFilePath, Path.GetDirectoryName(zipFilePath), true);
        }

        public static async Task<bool> DownloadLatestReleaseAsync(string repository_url, string output_directory)
        {
            var uri = new Uri(repository_url);
            string owner = uri.Segments[1].TrimEnd('/');
            string name = uri.Segments[2];

            var client = new GitHubClient(new Octokit.ProductHeaderValue("MyApp"));
            var release = await client.Repository.Release.GetLatest(owner, name);

            if (release.Assets.Count > 0)
            {
                string downloadUrl = release.Assets[0].BrowserDownloadUrl;
                string fileName = release.Assets[0].Name;

                if (fileName != RELEASE_FILE) return false;

                string outputFilePath = Path.Combine(output_directory, fileName);
                using (var httpClient = new HttpClient())
                {
                    var response = await httpClient.GetAsync(downloadUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        using (var fileStream = new FileStream(outputFilePath, System.IO.FileMode.Create))
                        {
                            await response.Content.CopyToAsync(fileStream);
                        }

                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            else
            {
                return false;
            }
        }

        const string C_JSON_FILE = "fast_connect.json";
        const string S_JSON_FILE = "fast_listen.json";
        const string JSON_ID_SERVER_PORT = "server_port";
        const string JSON_ID_SERVER_IP = "server_ip";

        private static void ProcClient()
        {
            int fast_port = -1;
            string fast_ip = null;
            {
                try
                {
                    JObject json = JObject.Parse(File.ReadAllText(C_JSON_FILE));
                    fast_port = (int)json[JSON_ID_SERVER_PORT];
                    fast_ip = (string)json[JSON_ID_SERVER_IP];
                }
                catch
                {
                    fast_ip = null;
                    fast_port = -1;
                }
            }

        port_re:
            int port = 0;
            string ip = null;
            Console.WriteLine("port(49154~65530) : ");

            {
                string line = Console.ReadLine();

                if (line == "")
                {
                    if (fast_port == -1) goto port_re;
                    port = fast_port;
                }
                else if (!int.TryParse(line, out port))
                {
                    goto port_re;
                }
            }

            Client client = null;

        re_connect:

            if (client != null)
            {
                client.Dispose();
                client = null;
            }

            try
            {
                Console.WriteLine("ip : ");

                ip = Console.ReadLine();
                if (ip == "")
                {
                    if (fast_ip == null) goto re_connect;
                    ip = fast_ip;
                }

                DebugLogger.SubLogger = Log;
                client = new Client(ip, port);
            }
            catch (Exception e)
            {
                Console.WriteLine("404 error");
                goto re_connect;
            }

            {
                try
                {
                    JObject json = new JObject();
                    json[JSON_ID_SERVER_PORT] = port;
                    json[JSON_ID_SERVER_IP] = ip;
                    File.WriteAllText(C_JSON_FILE, json.ToString());
                }
                catch
                {
                }
            }

            while (true)
            {
                lock (m_lock)
                {
                    Console.WriteLine("=======================Commands...========================");
                    Console.WriteLine($"{Server.CMD_ADDLIST}");
                    Console.WriteLine($"{Server.CMD_PLAY}");
                    Console.WriteLine($"{Server.SEND_CMD_SKIP}");
                    Console.WriteLine($"{Server.CMD_SHOWLIST}");
                    Console.WriteLine($"{Server.CMD_SHUFFLE}");
                    Console.WriteLine($"VOLUME_UP");
                    Console.WriteLine($"VOLUME_DOWN");
                    Console.WriteLine("==========================================================");
                    Console.WriteLine(@"https://github.com/TANAKADOREI/YoutubeMusicTogether");
                    Console.WriteLine("==========================================================\n>>");
                }

                switch (Console.ReadLine().ToUpper())
                {
                    case Server.CMD_ADDLIST:
                        Console.WriteLine("url:");
                        client.SEND_CMD_AddList(Console.ReadLine());
                        break;
                    case Server.CMD_PLAY:
                        client.SEND_CMD_Play();
                        break;
                    case Server.SEND_CMD_SKIP:
                        client.SEND_CMD_Skip();
                        break;
                    case Server.CMD_SHOWLIST:
                        client.SEND_CMD_ShowList();
                        break;
                    case Server.CMD_SHUFFLE:
                        client.SEND_CMD_Shuffle();
                        break;
                    case "VOLUME_UP":
                        client.Volume(true);
                        break;
                    case "VOLUME_DOWN":
                        client.Volume(false);
                        break;
                }
                Console.Clear();
            }
        }

        private static void Log(string obj)
        {
            lock (m_lock)
            {
                Console.WriteLine($"=====<Server>=====\n{obj}\n==================");
            }
        }

        private static void ProcServer()
        {
            int fast_port = -1;
            {
                try
                {
                    JObject json = JObject.Parse(File.ReadAllText(S_JSON_FILE));
                    fast_port = (int)json[JSON_ID_SERVER_PORT];
                }
                catch
                {
                    fast_port = -1;
                }
            }
        port_re:

            int port = 0;
            Console.WriteLine("port(49154~65530) : ");

            {
                string line = Console.ReadLine();

                if (line == "")
                {
                    if (fast_port == -1) goto port_re;
                    port = fast_port;
                }
                else if (!int.TryParse(line, out port))
                {
                    goto port_re;
                }
            }

            {
                try
                {
                    JObject json = new JObject();
                    json[JSON_ID_SERVER_PORT] = port;
                    File.WriteAllText(S_JSON_FILE, json.ToString());
                }
                catch
                {
                }
            }

            DebugLogger.SubLogger = Log;
            Server server = new Server(port);
            Console.ReadLine();
            Console.ReadLine();
            server.Destroy();
        }
    }
}