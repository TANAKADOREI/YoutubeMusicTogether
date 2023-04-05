using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

    public class Client
    {
        private readonly YoutubeClient m_youtube;
        private TcpClient m_client;
        private Thread m_thread;
        private Action<string> Log;

        YoutubePlayer m_player;

        public Client(Action<string> Log, string ipAddress, int port)
        {
            m_player = new YoutubePlayer(MusicEnd);
            this.Log = Log;
            m_youtube = new YoutubeClient();
            m_client = new TcpClient();
            m_client.Connect(ipAddress, port);
            m_thread = new Thread(Recv);
            m_thread.Start();
        }

        private void Recv()
        {
            Log("ClientRECV Start");
            try
            {
                while (true)
                {
                    var buffer = Packet.NewRawPacket;
                    if (m_client.GetStream().Read(buffer, 0, buffer.Length) != Packet.PACKET_SIZE) throw new Exception();
                    Packet packet = buffer;

                    if (packet == null) throw new Exception();

                    switch (packet.Data[0])
                    {
                        case Server.CMD_ADDLIST:
                            RECV_CMD_AddList(packet);
                            break;
                        case Server.CMD_PLAY:
                            RECV_CMD_Play(packet);
                            break;
                        case Server.CMD_SKIP:
                            RECV_CMD_Skip(packet);
                            break;
                    }
                }
            }
            catch (Exception e)
            {

            }
            Log("ClientRECV Stop");
        }

        public void SendMessage(Packet packet)
        {
            byte[] data = packet;
            m_client.GetStream().Write(data, 0, data.Length);
        }

        private void RECV_CMD_Skip(Packet packet)
        {
            //확인
        }

        public void SEND_CMD_Skip(uint count = 0)
        {
            SendMessage(new Packet(Server.CMD_SKIP, count.ToString()));
        }

        private void MusicEnd(YoutubePlayer obj)
        {
            throw new NotImplementedException();
        }

        private void RECV_CMD_Play(Packet packet)
        {
            if (packet.Data.Length != 3) return;
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
            //누군가 플레이리스트에 추가함
        }

        public void SEND_CMD_AddList(string youtube_url)
        {
            SendMessage(new Packet(Server.CMD_ADDLIST, youtube_url));
        }

        public void Close()
        {
            m_client.Close();
        }
    }
}
