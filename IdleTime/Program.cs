using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
namespace IdleTime
{
    class Program
    {
        //public static string MachineName { get; private set; }

        public static void Log(string logMessage, TextWriter w)
        {
            w.Write("\r\nLog Entry : ");
            w.WriteLine($"{DateTime.Now.ToLongTimeString()} {DateTime.Now.ToLongDateString()}");
            w.WriteLine("  :");
            w.WriteLine($"  :{logMessage}");
            w.WriteLine("-------------------------------");
        }
        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern bool FreeConsole();
        static void Main(string[] args)
        {
            //IntPtr hWnd = FindWindow(null, Application.ExecutablePath);
            //ShowWindow(hWnd, 0); // 0 = SW_HIDE
            //FreeConsole();
            int RandomNumber(int min, int max){
            Random random = new Random();
            return random.Next(min, max);
        }
        string MachineNameLower = '"' + Environment.MachineName.ToLower() + '"';
        string MachineNameUpper = '"' + Environment.MachineName + '"';

            void  SendIdleTime(string IdleSeconds)
        {
            using (Process process = new Process())
            {
                try{

                process.StartInfo.FileName = @"C:\zabbix\zabbix_sender.exe";
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                if(Settings1.Default.UpperCase == true)
                    {
                        process.StartInfo.Arguments = "-z 192.168.101.233 -p 10051 -s " + MachineNameUpper + " -k idletime -o " + IdleSeconds;
                    }
                    else
                    {
                        process.StartInfo.Arguments = "-z 192.168.101.233 -p 10051 -s " + MachineNameLower + " -k idletime -o " + IdleSeconds;
                    }
                process.Start();
                process.WaitForExit();
                }catch(Exception ex){
                                        using (StreamWriter w = File.AppendText(@"C:\zabbix\IdleTime.log"))
                    {
                        Log("Error Generated. Details: " + ex.ToString(), w);
                    }
                }
            }
        }
            try
            {
                while (true)
                {
                    string IdleSeconds = Math.Ceiling(UserInput.IdleTime.TotalSeconds).ToString();
                    int delay = RandomNumber(2000, 10000);
                    //SendIdleTime(IdleSeconds);
                    Console.WriteLine(IdleSeconds);
                    Thread.Sleep(delay);
                }
            }catch(Exception ex)
            {
                using (StreamWriter w = File.AppendText(@"C:\zabbix\IdleTime.log"))
                {
                    Log("Error Generated. Details: " + ex.ToString(), w);
                }
            }
        }


        public static class UserInput
        {

            [DllImport("user32.dll", SetLastError = false)]
            private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

            [StructLayout(LayoutKind.Sequential)]


            private struct LASTINPUTINFO
            {
                public uint cbSize;
                public int dwTime;
            }

            public static DateTime LastInput
            {
                get
                {
                    DateTime bootTime = DateTime.UtcNow.AddMilliseconds(-Environment.TickCount);
                    DateTime lastInput = bootTime.AddMilliseconds(LastInputTicks);
                    return lastInput;
                }
            }

            public static TimeSpan IdleTime
            {
                get
                {
                    return DateTime.UtcNow.Subtract(LastInput);
                }
            }

            public static uint GetIdleTime()
            {
                LASTINPUTINFO lastInPut = new LASTINPUTINFO();
                lastInPut.cbSize = (uint)Marshal.SizeOf(lastInPut);
                GetLastInputInfo(ref lastInPut);

                return (uint)Environment.TickCount - (uint)lastInPut.dwTime;
            }
            public static int LastInputTicks
            {
                get
                {
                    LASTINPUTINFO lii = new LASTINPUTINFO();
                    lii.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
                    GetLastInputInfo(ref lii);
                    return lii.dwTime;
                }
            }
        }
    }
}
