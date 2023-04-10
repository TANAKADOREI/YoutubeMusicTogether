using System.Diagnostics.CodeAnalysis;

namespace YMTUPDATER
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Wait...");
            Thread.Sleep(5000);

            string[] new_files = Directory.GetFiles(Path.Combine(Environment.CurrentDirectory, "UpdateTemp", "net6.0"));
            string dest_dir = Environment.CurrentDirectory;

            Console.ForegroundColor = ConsoleColor.Red;
            foreach (string file in new_files)
            {
                if (file.ToUpper().Contains("YMTUPDATER")) continue;
                Console.WriteLine($"overwirte : {file}->{Path.Combine(dest_dir, Path.GetFileName(file))}");
            }

            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine("agree? : Y");
            if (Console.ReadLine().ToUpper() != "Y") goto goto_done;

            foreach (string file in new_files)
            {
                try
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    if (file.ToUpper().Contains("YMTUPDATER")) continue;
                    Console.WriteLine($"overwirte : {file}->{Path.Combine(dest_dir, Path.GetFileName(file))}");
                    File.Copy(file, Path.Combine(dest_dir, Path.GetFileName(file)), true);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(ex.Message);
                }
            }


        goto_done:
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("done");
            Console.ReadLine();
        }
    }
}