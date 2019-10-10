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
        //LOGGING METHOD FORMAT
        private static void Log(string logMessage, TextWriter w)
        {
            w.Write("\r\nLog Entry : ");
            w.WriteLine($"{DateTime.Now.ToLongTimeString()} {DateTime.Now.ToLongDateString()}");
            w.WriteLine("  :");
            w.WriteLine($"  :{logMessage}");
            w.WriteLine("-------------------------------");
        }
        //THIS PROCESS HAS TO RUN IN THE USERS ENVIRONMENT AND CAN'T BE RUN AS A SERVICE
        //SO THE DLLS BELOW HAVE BEEN IMPORTED SO YOU CAN HIDE THE COMMAND WINDOW AS QUICKLY AS POSSIBLE.
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

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

            string MachineName = '"' + Environment.MachineName + '"';
            string ZabbixServerIp = Settings.Default.ZabbixServerLocation;//SET IP ADDRESS IN THE SETTINGS FILE
            string ZabbixSenderExeLocation = Settings.Default.ZabbixSenderLocation;//SET LOCATION IN SETTINGS FILE DEFAULT LOCATION IS C:\zabbix\zabbix_sender.exe

            void  SendToZabbix(string IdleSeconds)
        {
            using (Process process = new Process())
            {
                try{
                    //WHEN SENDING THE VALUE OF THE IDLETIME YOU NEED
                    //1. ZABBIX SENDER LOCATION THAT CAN BE SET IN THE SETTINGS FILE
                    //2. PORT NUMBER IS SET AT 10051
                    //3. THE MACHINE'S HOSTNAME AS REGISTERED IN THE ZABBIX SERVER, CASE SENSITIVE
                    //4. THE KEYNAME AS DEFINED IN ZABBIX FOR idletime, CASE SENSITIVE
                    //5. THE VALUE OF IDLETIME WE GET FROM the LastInteraction.
                    process.StartInfo.FileName = ZabbixSenderExeLocation;
                    process.StartInfo.Arguments = "-z " + ZabbixServerIp + " -p 10051 -s " + MachineName + " -k idletime -o " + IdleSeconds;
                    process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;   
                    process.Start();
                    process.WaitForExit();
                }
                    catch (Exception ex){
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
                    string IdleSeconds = Math.Ceiling(LastInteraction.IdleTime.TotalSeconds).ToString();
                    int delay = RandomNumber(2000, 10000);//SET THE DELAY HERE IN MS DEFAULT IS BETWEEN 2 AND 10 SECONDS
                    SendToZabbix(IdleSeconds);
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
    }
}
