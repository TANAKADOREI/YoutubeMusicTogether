using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace YMTCORE
{
    public static class Ex
    {
        public static string GetRemoteIPv4String(this TcpClient socket)
        {
            if (socket == null || !socket.Connected)
            {
                throw new ArgumentException("소켓이 연결되지 않았습니다.");
            }

            // 원격 엔드포인트를 가져옵니다.
            EndPoint remoteEndPoint = socket.Client.RemoteEndPoint;

            // 엔드포인트를 IPEndPoint로 형변환합니다.
            IPEndPoint ipEndPoint = remoteEndPoint as IPEndPoint;

            if (ipEndPoint == null || ipEndPoint.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ArgumentException("유효한 IPv4 엔드포인트가 아닙니다.");
            }

            // IPAddress 객체를 문자열로 변환하여 반환합니다.
            return ipEndPoint.Address.ToString();
        }
    }
}
