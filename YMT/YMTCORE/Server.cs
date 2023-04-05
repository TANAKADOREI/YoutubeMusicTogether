using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Net;
using System.Text;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;
using NAudio.Dmo;

namespace YMTCORE
{
    public class Server
    {
        private TcpListener m_listener;

        private object m_clients_lock = new object();
        private Dictionary<string, TcpClient> m_clients = new Dictionary<string, TcpClient>();

        private object m_playlist_lock = new object();
        private List<string> m_playlist = new List<string>();
        private readonly YoutubeClient m_youtube;
        private Action<string> Log;
        private Thread m_thread;

        public Server(int port, Action<string> Log)
        {
            this.Log = Log;
            m_listener = new TcpListener(IPAddress.Any, port);
            m_thread = new Thread(Start);
            m_thread.Start();
        }

        public void Start()
        {
            try
            {
                m_listener.Start();
                Log("Start Server");

                while (true)
                {
                    var client = m_listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(HandleClient, client);
                }
            }
            catch (Exception ex)
            {
                Log(ex.Message);
            }

            Log("Destroy Server");
        }

        public void Destroy()
        {
            lock (m_clients_lock)
            {
                foreach (var clientId in m_clients)
                {
                    clientId.Value.Close();
                    clientId.Value.Dispose();
                }
            }
            m_listener.Stop();
            m_thread.Join();
        }

        private void HandleClient(object state)
        {
            var client = (TcpClient)state;
            var clientId = client.GetRemoteIPv4String();

            lock (m_clients_lock)
            {
                m_clients.TryAdd(clientId, client);
            }

            Console.WriteLine("connected client:" + clientId);

            using (var stream = client.GetStream())
            {
                byte[] buffer = Packet.NewRawPacket;
                while (stream.Read(buffer, 0, buffer.Length) != buffer.Length)
                {
                    MainProc(buffer);
                }
            }

            Console.WriteLine("disconected client:" + clientId);
            lock (m_clients_lock)
            {
                m_clients.Remove(clientId);
            }
        }

        public const string CMD_ADDLIST = "AddList";
        public const string CMD_PLAY = "Play";
        public const string CMD_SKIP = "Skip";

        private void MainProc(Packet packet)
        {
            if (packet == null) return;
            switch (packet.Data[0])
            {
                case CMD_ADDLIST:
                    if (ProcAddList(packet)) SendAll(new Packet(packet, CMD_ADDLIST));
                    break;
                case CMD_PLAY:
                    ProcPlay(packet);
                    break;
                case CMD_SKIP:
                    if (ProcSkip(packet)) SendAll(new Packet(packet, CMD_SKIP));
                    break;
            }
        }

        private bool ProcSkip(Packet packet)
        {
            if (packet.Data.Length != 2) return false;
            uint count = uint.Parse(packet.Data[1]);
            lock (m_playlist_lock)
            {
                for (int i = 0; i < count; i++)
                {
                    if (m_playlist.Count == 0) break;
                    m_playlist.RemoveAt(0);
                }
            }
            return true;
        }

        private void ProcPlay(Packet packet)
        {
            string url = null;
            lock (m_playlist_lock)
            {
                if (m_playlist.Count == 0)
                {
                    url = null;
                }
                else
                {
                    url = m_playlist[0];
                    m_playlist.RemoveAt(0);
                }
            }
            if (url == null)
            {
                SendAll(new Packet(packet, CMD_PLAY, "null"));
            }
            else
            {
                SendAll(new Packet(packet, CMD_PLAY, url, DateTime.UtcNow.AddSeconds(1).ToString()));
            }
        }

        private bool ProcAddList(Packet packet)
        {
            if (packet.Data.Length != 2) return false;

            {
                string youtube_url = packet.Data[1];
                string[] direct_urls = null;
                DateTime dateTime = DateTime.Now;

                {
                    try
                    {
                        var streamManifest = m_youtube.Videos.Streams.GetManifestAsync(youtube_url).Result;
                        var audioStreamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
                        direct_urls = new string[] { audioStreamInfo.Url };
                    }
                    catch
                    {
                        direct_urls = null;
                    }
                }

                if (direct_urls == null)
                {
                    try
                    {
                        var vs = m_youtube.Playlists.GetVideosAsync(youtube_url);
                        var temp = from mani in from url in vs.GetAwaiter().GetResult().Select(_ => _.Url) select m_youtube.Videos.Streams.GetManifestAsync(url).Result select mani.GetAudioOnlyStreams().GetWithHighestBitrate().Url;
                        direct_urls = temp.ToArray();
                    }
                    catch
                    {
                        direct_urls = null;
                    }
                }

                if (direct_urls != null)
                {
                    lock (m_playlist_lock)
                    {
                        m_playlist.AddRange(direct_urls);
                    }
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        private void SendAll(byte[] buffer)
        {
            IEnumerable<Task> tasks = null;
            lock (m_clients_lock)
            {
                tasks = from c in m_clients select c.Value.GetStream().WriteAsync(buffer).AsTask();
            }

            if (tasks == null) return;

            Task.WaitAll(tasks.ToArray());
        }
    }
}