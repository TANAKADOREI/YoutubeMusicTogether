using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Net;
using System.Text;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;
using NAudio.Dmo;
using AngleSharp.Dom;
using YoutubeExplode.Videos;
using System.Collections.Generic;

namespace YMTCORE
{
    public class Server
    {
        private TcpListener m_listener;

        private object m_clients_lock = new object();
        private Dictionary<string, TcpClient> m_clients = new Dictionary<string, TcpClient>();

        private object m_playlist_lock = new object();
        //<title,url>
        private List<YVideoInfo> m_playlist = new List<YVideoInfo>();
        private Random m_playlist_rand = new Random();
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

        public const string RECV_CMD_CMD_CANCELED = "CMD_CANCELED";
        public const string CMD_ADDLIST = "ADDLIST";
        public const string CMD_PLAY = "PLAY";
        public const string CMD_SKIP = "SKIP";
        public const string CMD_SHOWLIST = "SHOWLIST";
        public const string CMD_SHUFFLE = "SHUFFLE";

        private void MainProc(Packet packet)
        {
            if (packet == null) return;

            void AlreadyProc()
            {
                SendAll(new Packet(packet, RECV_CMD_CMD_CANCELED, $"The same operation ({packet.Data[0]}) is already in progress"));
            }

            switch (packet.Data[0])
            {
                case CMD_ADDLIST:
                    if (Monitor.TryEnter(ProcAddListLock))
                    {
                        ProcAddList(packet);
                        Monitor.Exit(ProcAddListLock);
                    }
                    else
                    {
                        AlreadyProc();
                    }
                    break;

                case CMD_PLAY:
                    ProcPlay(packet);
                    break;

                case CMD_SKIP:
                    if (Monitor.TryEnter(ProcSkipLock))
                    {
                        ProcSkip(packet);
                        Monitor.Exit(ProcSkipLock);
                    }
                    else
                    {
                        AlreadyProc();
                    }
                    break;

                case CMD_SHOWLIST:
                    if (Monitor.TryEnter(ProcShowListLock))
                    {
                        ProcShowList(packet);
                        Monitor.Exit(ProcShowListLock);
                    }
                    else
                    {
                        AlreadyProc();
                    }
                    break;

                case CMD_SHUFFLE:
                    if (Monitor.TryEnter(ProcShuffleLock))
                    {
                        ProcShuffle(packet);
                        Monitor.Exit(ProcShuffleLock);
                    }
                    else
                    {
                        AlreadyProc();
                    }
                    break;
            }
        }

        private object ProcShuffleLock = new object();
        private void ProcShuffle(Packet packet)
        {
            try
            {
                void proc()
                {
                    try
                    {
                        lock (m_playlist_lock)
                        {
                            for (int i = 0; i < m_playlist.Count; i++)
                            {
                                int target_index = m_playlist_rand.Next(0, m_playlist.Count);
                                var temp = m_playlist[i];
                                m_playlist[i] = m_playlist[target_index];
                                m_playlist[target_index] = temp;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log(e.Message);
                    }
                }

                Task.Run(proc);
            }
            catch (Exception e)
            {
                Log(e.Message);
            }
        }

        public const int TITLE_MAX_LENGTH = 50;
        private object ProcShowListLock = new object();
        private void ProcShowList(Packet packet)
        {
            List<string> list = new List<string>();
            list.Add(CMD_SHOWLIST);
            lock (m_playlist_lock)
            {
                list.Add(m_playlist.Count.ToString());
                for (int i = 0; i < 50; i++)
                {
                    if (m_playlist.Count <= i) break;
                    list.Add(m_playlist[i].GetTitle());
                }
            }

            SendAll(new Packet(packet, list.ToArray()));
        }

        private object ProcSkipLock = new object();
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

        //병렬 지원
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
                        url = m_playlist[0].GetDirectURL();
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

        class YVideoInfo
        {
            private string m_url;
            private Video m_vd;

            public YVideoInfo(string url, Video vd)
            {
                m_url = url;
                m_vd = vd;
            }

            public string GetTitle()
            {
                string raw = $"{m_vd.Author.ChannelTitle}:{m_vd.Title}";
                var temp = raw.Trim();
                return temp.Substring(0, temp.Length >= TITLE_MAX_LENGTH ? TITLE_MAX_LENGTH : temp.Length).Trim();
            }

            public string GetDirectURL()
            {
                YoutubeClient m_youtube = new YoutubeClient();
                var stream_mani = m_youtube.Videos.Streams.GetManifestAsync(m_url).Result;
                var stream_info = stream_mani.GetAudioOnlyStreams().GetWithHighestBitrate();
                return stream_info.Url;
            }
        }

        private object ProcAddListLock = new object();
        private void ProcAddList(Packet packet)
        {
            try
            {
                if (packet.Data.Length != 2) return;
                {
                    string youtube_url = packet.Data[1];
                    DateTime dateTime = DateTime.Now;

                    bool TryParseVideo()
                    {
                        try
                        {
                            YoutubeClient m_youtube = new YoutubeClient();
                            var info = new YVideoInfo(youtube_url, m_youtube.Videos.GetAsync(youtube_url).Result);

                            lock (m_playlist_lock)
                            {
                                m_playlist.Add(info);
                            }

                            return true;
                        }
                        catch (Exception e)
                        {
                            Log(e.Message);
                            return false;
                        }
                        finally
                        {
                            GC.Collect();
                        }
                    }

                    bool TryParsePlaylist()
                    {
                        try
                        {
                            YoutubeClient m_youtube = new YoutubeClient();
                            var vs = m_youtube.Playlists.GetVideosAsync(youtube_url);
                            foreach (var url in vs.GetAwaiter().GetResult().Select(_ => _.Url))
                            {
                                var info = new YVideoInfo(url, m_youtube.Videos.GetAsync(url).Result);

                                lock (m_playlist_lock)
                                {
                                    m_playlist.Add(info);
                                }
                            }
                            return true;
                        }
                        catch (Exception e)
                        {
                            Log(e.Message);
                            return false;
                        }
                        finally
                        {
                            GC.Collect();
                        }
                    }

                    void Proc()
                    {
                        if (!TryParseVideo())
                        {
                            if (!TryParsePlaylist())
                            {
                                //유튜브 링크가 아님
                                SendAll(new Packet(packet, CMD_ADDLIST, "Not a URL to YouTube"));
                            }
                        }

                        SendAll(new Packet(packet, CMD_ADDLIST, $"Queue updated"));
                    }

                    Task.Run(Proc);
                }
            }
            catch (Exception e)
            {
                Log(e.Message);
                SendAll(new Packet(packet, CMD_ADDLIST, "Not a URL to YouTube"));
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