//THIS FILE NEEDS TO RESIDE IN THE HOST THAT YOU WANT TO WATCH FOR ACTIVITY.
//UNTIL I AUTOMATE THE INSTALLATION YOU'LL NEED TO ADD A SCHEDULED TASK TO RUN WHEN THE USER LOGS ON.
//SET PARAMETERS IN THE 
//
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

        //LOGGING METHOD FORMAT
        public static void Log(string logMessage, TextWriter w)
        {
            w.Write("\r\nLog Entry : ");
            w.WriteLine($"{DateTime.Now.ToLongTimeString()} {DateTime.Now.ToLongDateString()}");
            w.WriteLine("  :");
            w.WriteLine($"  :{logMessage}");
            w.WriteLine("-------------------------------");
        }
        //THIS PROCESS HAS TO RUN IN THE USERS ENVIRONMENT AND CAN'T BE RUN AS A SERVICE
        //SO THE DLLS BELOW HAVE BEEN IMPORTED SO YOU CAN HIDE THE COMMAND WINDOW AS QUICKLY AS POSSIBLE.
        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern bool FreeConsole();
        static void Main(string[] args)
        {
            //IF YOU WANT TO SEE THE OUTPUT WINDOW SET THE HIDEWINDOW CONFIG TO FALSE
            if(Settings.Default.HideWindow == true){
            IntPtr hWnd = FindWindow(null, Application.ExecutablePath);
            ShowWindow(hWnd, 0); // 0 = SW_HIDE
            FreeConsole();
        }
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
                        //DEFAULT ZABBIX SENDER LOCATION IS C:\zabbix\zabbix_sender.exe
                        process.StartInfo.FileName = Settings.Default.ZabbixSenderLocation;//SET THIS HERE OR AT THE SETTINGS FILE
                        process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                if(Settings.Default.UpperCase == true)
                    {
                        process.StartInfo.Arguments = "-z " + Settings.Default.ZabbixServerLocation + " -p 10051 -s " + MachineNameUpper + " -k idletime -o " + IdleSeconds;
                    }
                    else
                    {
                        process.StartInfo.Arguments = "-z " + Settings.Default.ZabbixServerLocation + " -p 10051 -s " + MachineNameLower + " -k idletime -o " + IdleSeconds;
                    }
                process.Start();
                process.WaitForExit();
                }catch(Exception ex){
                        using (StreamWriter w = File.AppendText(Settings.Default.ZabbixLogLocation))
                    {
                        Log("Error Generated. Details: " + ex.ToString(), w);
                    }
                }
            }
        }
            try
            {
                //
                //HERE THE WHILE LOOP RUNS THE PROGRAM, SendIdleTime method sends to Zabbix the Idletime
                //
                while (true)
                {
                    string IdleSeconds = Math.Ceiling(UserInput.IdleTime.TotalSeconds).ToString();
                    //SET THE DELAY HERE IN MS DEFAULT IS BETWEEN 2 AND 10 SECONDS
                    int delay = RandomNumber(2000, 10000);
                    //SendIdleTime(IdleSeconds);
                    Console.WriteLine(IdleSeconds);
                    Thread.Sleep(delay);
                }
            }catch(Exception ex)
            {
                using (StreamWriter w = File.AppendText(Settings.Default.ZabbixLogLocation))
                {
                    Log("Error Generated. Details: " + ex.ToString(), w);
                }
            }
        }

        //Below are the class definitions for the IdleTime
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
