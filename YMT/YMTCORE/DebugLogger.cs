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
            Debug.WriteLine(log);
            SubLogger?.Invoke(log);
#else
            SubLogger?.Invoke(msg);
#endif
        }
    }
}
