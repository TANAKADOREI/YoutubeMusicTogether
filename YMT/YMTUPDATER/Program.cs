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

            foreach (string file in new_files)
            {
                if (file.Contains("YMT_Console")) continue;
                Console.WriteLine($"overwirte : {file}->{Path.Combine(dest_dir, Path.GetFileName(file))}");
            }

            Console.WriteLine("agree? : Y");
            if (Console.ReadLine() != "Y") return;

            foreach (string file in new_files)
            {
                if (file.Contains("YMT_Console")) continue;
                Console.WriteLine($"overwirte : {file}->{Path.Combine(dest_dir, Path.GetFileName(file))}");
                File.Copy(file, Path.Combine(dest_dir, Path.GetFileName(file)), true);
            }
        }
    }
}