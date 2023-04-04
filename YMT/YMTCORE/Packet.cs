using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace YMTCORE
{
    public class Packet
    {
        public const int PACKET_SIZE = 8192;
        public static byte[] NewRawPacket => new byte[PACKET_SIZE];
        public enum PacketType
        {
            Unknown = 0,
            Request,
            Response,
        }

        public PacketType Type;
        public DateTime Created;
        public string[] Data;
        public string IP;

        /// <summary>
        /// 요청 패킷 생성용
        /// </summary>
        public Packet(TcpClient socket,params string[] data)
        {
            IP = socket.GetRemoteIPv4String();
            Created = DateTime.UtcNow;
            Type = PacketType.Request;
            Data = data;
        }

        /// <summary>
        /// 요청에 의한 대답 생성용
        /// </summary>
        public Packet(TcpClient socket, Packet packet, params string[] data)
        {
            IP = socket.GetRemoteIPv4String();
            Created = packet.Created;
            Type = PacketType.Response;
            Data = data;
        }

        public static implicit operator string(Packet packet)
        {
            return JsonConvert.SerializeObject(packet);
        }

        public static implicit operator Packet(string packet)
        {
            return JsonConvert.DeserializeObject<Packet>(packet);
        }

        public static implicit operator byte[](Packet packet)
        {
            var temp = Encoding.UTF8.GetBytes((string)packet);
            if (temp.Length >= PACKET_SIZE) throw new Exception();
            var result = NewRawPacket;
            Array.Copy(result, temp, temp.Length);
            return result;
        }

        public static implicit operator Packet(byte[] packet)
        {
            if (packet.Length != PACKET_SIZE) throw new Exception();
            return (Packet)Encoding.UTF8.GetString(packet);
        }
    }
}
