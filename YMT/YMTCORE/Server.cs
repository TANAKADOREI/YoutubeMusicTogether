using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Net;
using System.Text;

namespace YMTCORE
{
    public class Server
    {
        private TcpListener m_listener;
        private ConcurrentDictionary<string, TcpClient> m_clients;
        private Action<string> Log;
        private Thread m_thread;

        public Server(int port, Action<string> Log)
        {
            this.Log = Log;
            m_listener = new TcpListener(IPAddress.Any, port);
            m_clients = new ConcurrentDictionary<string, TcpClient>();
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
            catch(Exception ex)
            {
                Log(ex.Message);
            }

            Log("Destroy Server");
        }

        public void Destroy()
        {
            foreach (var clientId in m_clients)
            {
                clientId.Value.Close();
                clientId.Value.Dispose();
            }
            m_listener.Stop();
            m_thread.Join();
        }

        private void HandleClient(object state)
        {
            var client = (TcpClient)state;
            var clientId = Guid.NewGuid().ToString();
            m_clients.TryAdd(clientId, client);
            Console.WriteLine("connected client:"+ clientId);

            using (var stream = client.GetStream())
            {
                byte[] buffer = Packet.NewRawPacket;
                while (stream.Read(buffer, 0, buffer.Length) != buffer.Length)
                {
                    foreach (var c in m_clients)
                    {
                        if (c.Key != clientId)
                        {
                            var temp = m_clients[c.Key];
                            if (temp.Connected)
                            {
                                temp.GetStream().Write(buffer, 0, buffer.Length);
                            }
                        }
                    }
                }
            }

            Console.WriteLine("disconected client:"+ clientId);
            m_clients.TryRemove(clientId, out _);
        }
    }
}