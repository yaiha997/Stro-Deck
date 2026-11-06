using System;
using System.Threading;
using System.Windows.Forms;

namespace StreamDeckPC
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            bool created;
            using (Mutex m = new Mutex(true, "StreamDeckPC_single_instance", out created))
            {
                if (!created)
                {
                    try
                    {
                        using (EventWaitHandle show = EventWaitHandle.OpenExisting("StreamDeckPC_show"))
                        {
                            show.Set();
                        }
                    }
                    catch { }
                    return;
                }

                using (EventWaitHandle show = new EventWaitHandle(false, EventResetMode.AutoReset, "StreamDeckPC_show"))
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
                    {
                        try
                        {
                            System.IO.File.AppendAllText(
                                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log"),
                                DateTime.Now.ToString("u") + "  " + e.Exception + Environment.NewLine);
                        }
                        catch { }
                    };
                    Application.Run(new MainForm(show));
                }
            }
        }
    }
}
