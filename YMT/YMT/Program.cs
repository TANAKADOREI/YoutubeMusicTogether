using YMTCORE;

namespace YMT
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Server : S, Client : C");
            switch (Console.ReadLine().ToUpper())
            {
                case "S":
                    ProcServer();
                    break;
                case "C":
                    ProcClient();
                    break;
            }
        }

        private static void ProcClient()
        {
        port_re:
            Console.WriteLine("port(49154~65530) : ");
            int port = 0;

            if (!int.TryParse(Console.ReadLine(), out port))
            {
                goto port_re;
            }

            Client client = null;

            re_connect:
            try
            {
                Console.WriteLine("ip : ");
                client = new Client(Console.WriteLine, Console.ReadLine(), port);
            }
            catch(Exception e)
            {
                Console.WriteLine("404 error");
                goto re_connect;
            }

            while (true)
            {
                Console.WriteLine($"{Server.CMD_ADDLIST}");
                Console.WriteLine($"{Server.CMD_PLAY}");
                Console.WriteLine($"{Server.CMD_SKIP}");
                Console.WriteLine($"{Server.CMD_SHOWLIST}");
                switch(Console.ReadLine().ToUpper())
                {
                    case Server.CMD_ADDLIST:
                        Console.WriteLine("url:");
                        client.SEND_CMD_AddList( Console.ReadLine() );
                        break;
                    case Server.CMD_PLAY:
                        client.SEND_CMD_Play();
                        break;
                    case Server.CMD_SKIP:
                        client.SEND_CMD_Skip();
                        break;
                    case Server.CMD_SHOWLIST:
                        client.SEND_CMD_ShowList();
                        break;
                }
            }
        }

        private static void ProcServer()
        {
        port_re:
            Console.WriteLine("port(49154~65530) : ");
            int port = 0;

            if (!int.TryParse(Console.ReadLine(), out port))
            {
                goto port_re;
            }

            Server server = new Server(port, Console.WriteLine);
        }
    }
}