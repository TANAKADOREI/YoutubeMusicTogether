using YMTCORE;

namespace YMT
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Server : S, Client : C");
            switch (Console.ReadLine())
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
                Console.WriteLine("");
                switch(Console.ReadLine())
                {
                    case "AddList":
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