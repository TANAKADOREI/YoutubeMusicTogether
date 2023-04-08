using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YMTCORE
{
    public static class DebugLogger
    {
        public static Action<string> SubLogger;
        public static void Log(string msg)
        {
            string log = $"==================[{DateTime.Now}]==================\n" +
                $"{msg}\n{new StackTrace()}======================================================";
#if RELEASE
#else
            Debug.WriteLine(log);
#endif
            SubLogger?.Invoke(log);
        }
    }
}
