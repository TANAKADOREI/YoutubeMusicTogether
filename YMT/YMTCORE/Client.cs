using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace YMTCORE
{
    public class Client
    {
        private readonly YoutubeClient m_youtube;
        private TcpClient m_client;
        private Thread m_thread;
        private object m_lock = new object();
        private HashSet<string> m_server_client_counter = new HashSet<string>();//나 빼고
        private Dictionary<DateTime, List<Packet>> m_packets = new Dictionary<DateTime, List<Packet>>();

        public Client(string ipAddress, int port)
        {
            m_client = new TcpClient();
            m_client.Connect(ipAddress, port);
            m_thread = new Thread(Recv);
            m_thread.Start();
        }

        private void Recv()
        {
            try
            {
                while (true)
                {
                    var buffer = Packet.NewRawPacket;
                    if (m_client.GetStream().Read(buffer, 0, buffer.Length) != Packet.PACKET_SIZE) throw new Exception();
                    Packet packet = buffer;

                    switch (packet.Data[0])
                    {
                        case "hi":
                            lock (m_lock)
                            {
                                m_server_client_counter.Add(packet.Data[1]);
                            }
                            break;
                        case "bye":
                            lock (m_lock)
                            {
                                m_server_client_counter.Remove(packet.Data[1]);
                            }
                            break;
                        case nameof(CMD_AddList):
                            RECV_CMD_AddList(packet);
                            break;
                        default:
                            lock (m_lock)
                            {
                                if (m_packets.ContainsKey(packet.Created))
                                {
                                    m_packets[packet.Created].Add(packet);
                                }
                                else
                                {
                                    m_packets.Add(packet.Created, new List<Packet>(new Packet[] { packet }));
                                }
                            }
                            break;
                    }
                }
            }
            catch (Exception e)
            {

            }
        }

        public void SendMessage(Packet packet)
        {
            byte[] data = packet;
            m_client.GetStream().Write(data, 0, data.Length);
        }

        private void CMD_Play()
        {

        }

        private void RECV_CMD_Play()
        {

        }

        public void SEND_CMD_Play()
        {

        }

        private void CMD_AddList(string direct_url)
        {
            //라디오에 넣기
        }

        private void RECV_CMD_AddList(Packet packet)
        {
            CMD_AddList(packet.Data[1]);
        }

        public void SEND_CMD_AddList(string youtube_url)
        {
            string direct_url = null;
            DateTime dateTime = DateTime.Now;

            {
                var streamManifest = m_youtube.Videos.Streams.GetManifestAsync(youtube_url).Result;
                var audioStreamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
                direct_url = audioStreamInfo.Url;
            }

            if (direct_url == null) throw new Exception();

            {
                var packet = new Packet(m_client,nameof(CMD_AddList), direct_url);
                dateTime = packet.Created;
                SendMessage(packet);
            }

            WaitAll(dateTime);

            CMD_AddList(direct_url);
        }

        private void WaitAll(DateTime dateTime)
        {
            while (true)
            {
                lock (m_lock)
                {
                    if(m_packets.ContainsKey(dateTime))
                    {
                        //서버에 있는 사람들중 존재하는 사람의 패킷만 일단 골라냄 없는 사람을 대기할순 없으니까
                        var r = from packet in m_packets[dateTime] where m_server_client_counter.Contains(packet.IP) select packet;
                        if(r != null && r.Count() == m_server_client_counter.Count)
                        {
                            //모두 응답을 받음
                            return;
                        }
                    }
                }
                Thread.Sleep(100);
            }
        }

        public void Close()
        {
            m_client.Close();
        }
    }
}
