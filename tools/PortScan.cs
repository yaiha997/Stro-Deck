using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

class PortScan
{
    static int done;
    static readonly List<int> open = new List<int>();
    static readonly object gate = new object();

    static void Main(string[] args)
    {
        if (args.Length < 1) { Console.WriteLine("usage: PortScan <ip> [from] [to]"); return; }
        string host = args[0];
        int from = args.Length > 1 ? int.Parse(args[1]) : 1;
        int to = args.Length > 2 ? int.Parse(args[2]) : 65535;
        IPAddress ip;
        if (!IPAddress.TryParse(host, out ip)) { Console.WriteLine("bad ip"); return; }

        int total = to - from + 1;
        Semaphore slim = new Semaphore(400, 400);
        DateTime start = DateTime.UtcNow;
        for (int p = from; p <= to; p++)
        {
            int port = p;
            slim.WaitOne();
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    try
                    {
                        IAsyncResult ar = s.BeginConnect(ip, port, null, null);
                        if (ar.AsyncWaitHandle.WaitOne(350, false) && s.Connected)
                        {
                            s.EndConnect(ar);
                            lock (gate) { open.Add(port); Console.WriteLine("OPEN " + port); }
                        }
                    }
                    catch { }
                    finally { s.Close(); }
                }
                finally
                {
                    Interlocked.Increment(ref done);
                    slim.Release();
                }
            });
        }
        while (Thread.VolatileRead(ref done) < total) Thread.Sleep(100);
        Console.WriteLine("done in " + (int)(DateTime.UtcNow - start).TotalSeconds + "s, open: " + open.Count);
    }
}
