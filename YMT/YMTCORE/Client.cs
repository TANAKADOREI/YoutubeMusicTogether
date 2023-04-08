using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;

namespace YMTCORE
{
    //명령 전송하기전 실패 했을 경우
    public class FailCommand : Exception
    {

    }

    public class Client : IDisposable
    {
        private TcpClient m_client;
        private Thread m_thread;

        YoutubePlayer m_player;

        public Client(string ipAddress, int port)
        {
            m_player = new YoutubePlayer(MusicEnd);
            m_client = new TcpClient();
            m_client.SendBufferSize = m_client.ReceiveBufferSize = Packet.PACKET_SIZE;
            m_client.Connect(ipAddress, port);
            m_thread = new Thread(Recv);
            m_thread.Start();
        }

        private void Recv()
        {
            DebugLogger.SubLogger?.Invoke("ClientRECV Start");
            try
            {
                while (true)
                {
                    var buffer = Packet.NewRawPacket;
                    if (m_client.Client.Receive(buffer) != Packet.PACKET_SIZE) return;
                    Packet packet = buffer;

                    if (packet == null) throw new Exception();

                    DebugLogger.Log($"Recv->{packet.Print()}");

                    switch (packet.Data[0])
                    {
                        case Server.CMD_ADDLIST:
                            RECV_CMD_AddList(packet);
                            break;
                        case Server.CMD_PLAY:
                            RECV_CMD_Play(packet);
                            break;
                        case Server.CMD_SHOWLIST:
                            RECV_CMD_ShowList(packet);
                            break;
                        case Server.CMD_SHUFFLE:
                            RECV_CMD_Shuffle(packet);
                            break;
                        case Server.RECV_CMD_CMD_CANCELED:
                            RECV_CMD_CANCELED(packet);
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                DebugLogger.SubLogger?.Invoke(e.Message);
            }
            DebugLogger.SubLogger?.Invoke("ClientRECV Stop");
        }

        private void RECV_CMD_CANCELED(Packet packet)
        {
            DebugLogger.SubLogger?.Invoke(packet.Data[1]);
        }

        public void SendMessage(Packet packet)
        {
            DebugLogger.Log($"Send->{packet.Print()}");
            byte[] data = packet;
            m_client.Client.Send(data, SocketFlags.None);
        }

        private void RECV_CMD_ShowList(Packet packet)
        {
            StringBuilder builder = new StringBuilder();
            if (packet.Data.Length > 2)
            {
                builder.AppendLine($"======<playlist({packet.Data[1]})>======");
                for (int i = 2; i < packet.Data.Length; i++)
                {
                    builder.Append(i - 1);
                    builder.Append(':');
                    builder.Append(packet.Data[i]);
                    if (packet.Data[i].Length == Server.TITLE_MAX_LENGTH) builder.Append("...");
                    builder.AppendLine();
                }
                builder.AppendLine("...");
                builder.AppendLine("======================");
            }

            DebugLogger.SubLogger?.Invoke(builder.ToString());
        }

        public void SEND_CMD_Shuffle()
        {
            SendMessage(new Packet(Server.CMD_SHUFFLE));
        }

        private void RECV_CMD_Shuffle(Packet packet)
        {
            //pass
        }

        public void SEND_CMD_ShowList()
        {
            SendMessage(new Packet(Server.CMD_SHOWLIST));
        }

        public void SEND_CMD_Skip(uint count = 1)
        {
            SendMessage(new Packet(Server.SEND_CMD_SKIP, count.ToString()));
        }

        private void MusicEnd(YoutubePlayer obj, bool clear, string url)
        {
            SendMessage(new Packet(Server.SEND_CMD_MUSICEND, url));
        }

        private void RECV_CMD_Play(Packet packet)
        {
            if (packet.Data.Length != 3)
            {
                //null
                m_player.Stop(true);//콜백을 하지 않음
                return;
            }

            var fire_time = DateTime.Parse(packet.Data[2]);
            var url = packet.Data[1];

            //시간 동기화
            Task.Run(() =>
            {
                var interval = fire_time - DateTime.UtcNow;

                if (interval.Ticks >= 0)
                {
                    Task.Delay(interval);
                }

                if (!m_player.IsReady())
                {
                    //플레이 도중
                    m_player.Stop(true);
                }

                m_player.Play(url);

                interval = DateTime.UtcNow - fire_time;
                m_player.Seek(interval.Duration());
            });
        }

        public void SEND_CMD_Play()
        {
            SendMessage(new Packet(Server.CMD_PLAY));
        }

        private void RECV_CMD_AddList(Packet packet)
        {
            try
            {
                DebugLogger.SubLogger?.Invoke(packet.Data[1]);
            }
            catch (Exception e)
            {
                DebugLogger.SubLogger?.Invoke(e.Message);
            }
        }

        public void SEND_CMD_AddList(string youtube_url)
        {
            SendMessage(new Packet(Server.CMD_ADDLIST, youtube_url));
        }

        public void Dispose()
        {
            m_player.Dispose();
            m_client.Close();
        }

        public Action<bool> Volume => m_player.SetVolume;
    }
}
