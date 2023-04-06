using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Net;
using System.Text;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;
using NAudio.Dmo;
using AngleSharp.Dom;

namespace YMTCORE
{
    public class Server
    {
        private TcpListener m_listener;

        private object m_clients_lock = new object();
        private Dictionary<string, TcpClient> m_clients = new Dictionary<string, TcpClient>();

        private object m_playlist_lock = new object();
        //<title,url>
        private List<Tuple<string, string>> m_playlist = new List<Tuple<string, string>>();
        private readonly YoutubeClient m_youtube = new YoutubeClient();
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
                m_listener.Server.ReceiveBufferSize = m_listener.Server.SendBufferSize = Packet.PACKET_SIZE;
                m_listener.Start();
                Log("Start Server");

                while (true)
                {
                    var client = m_listener.AcceptTcpClient();
                    client.ReceiveBufferSize = client.SendBufferSize = Packet.PACKET_SIZE;
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


            try
            {
                while (true)
                {
                    byte[] buffer = Packet.NewRawPacket;
                    if (client.Client.Receive(buffer) == buffer.Length)
                    {
                        MainProc(buffer);
                    }
                }
            }
            catch (Exception e)
            {
                Log(e.ToString());
            }

            Console.WriteLine("disconected client:" + clientId);
            lock (m_clients_lock)
            {
                m_clients.Remove(clientId);
            }
        }

        public const string CMD_ADDLIST = "ADDLIST";
        public const string CMD_PLAY = "PLAY";
        public const string CMD_SKIP = "SKIP";
        public const string CMD_SHOWLIST = "SHOWLIST";

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
                    ProcSkip(packet);
                    break;
                case CMD_SHOWLIST:
                    ProcShowList(packet);
                    break;
            }
        }

        private void ProcShowList(Packet packet)
        {
            List<string> list = new List<string>();
            list.Add(CMD_SHOWLIST);
            lock (m_playlist_lock)
            {
                for (int i = 0; i < 10; i++)
                {
                    if (m_playlist.Count <= i) break;
                    list.Add(m_playlist[i].Item1);
                }
            }

            SendAll(new Packet(packet, list.ToArray()));
        }

        private void ProcSkip(Packet packet)
        {
            if (packet.Data.Length != 2) return;
            uint count = uint.Parse(packet.Data[1]);
            lock (m_playlist_lock)
            {
                for (int i = 0; i < count; i++)
                {
                    if (m_playlist.Count == 0) break;
                    m_playlist.RemoveAt(0);
                }
            }
            SendAll(new Packet(packet, CMD_SKIP));
        }

        private void ProcPlay(Packet packet)
        {
            try
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
                        url = m_playlist[0].Item2;
                        //m_playlist.RemoveAt(0);
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
            catch (Exception e)
            {
                Log(e.Message);
            }
        }

        private bool ProcAddList(Packet packet)
        {
            try
            {
                if (packet.Data.Length != 2) return false;

                {
                    string youtube_url = packet.Data[1];
                    List<Tuple<string, string>> direct_urls = new List<Tuple<string, string>>();
                    DateTime dateTime = DateTime.Now;

                    {
                        try
                        {
                            var stream_mani = m_youtube.Videos.Streams.GetManifestAsync(youtube_url).Result;
                            var stream_info = stream_mani.GetAudioOnlyStreams().GetWithHighestBitrate();
                            direct_urls.Add(new Tuple<string, string>(m_youtube.Videos.GetAsync(youtube_url).Result.Author.ChannelTitle, stream_info.Url));
                        }
                        catch
                        {
                            direct_urls.Clear();
                        }
                    }

                    if (direct_urls.Count == 0)
                    {
                        try
                        {
                            var vs = m_youtube.Playlists.GetVideosAsync(youtube_url);
                            foreach (var url in vs.GetAwaiter().GetResult().Select(_ => _.Url))
                            {
                                string direct_url = m_youtube.Videos.Streams.GetManifestAsync(url).Result.GetAudioOnlyStreams().GetWithHighestBitrate().Url;
                                string title = m_youtube.Videos.GetAsync(url).Result.Author.ChannelTitle;
                                direct_urls.Add(new Tuple<string, string>(title, direct_url));
                            }
                        }
                        catch
                        {
                            direct_urls.Clear();
                        }
                    }

                    if (direct_urls.Count() != 0)
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
            catch (Exception e)
            {
                Log(e.Message);
                return false;
            }
        }

        private void SendAll(byte[] buffer)
        {
            IEnumerable<Task> tasks = null;
            lock (m_clients_lock)
            {
                tasks = from c in m_clients select c.Value.Client.SendAsync(buffer, SocketFlags.None);
            }

            if (tasks == null) return;

            Task.WaitAll(tasks.ToArray());
        }
    }
}