using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

class FastScan
{
    static readonly object gate = new object();
    static int next;
    static int from, to, timeout;
    static IPAddress ip;
    static List<int> open = new List<int>();

    static void Main(string[] args)
    {
        if (args.Length < 1) { Console.WriteLine("usage: FastScan <ip> [from] [to] [timeoutMs]"); return; }
        ip = IPAddress.Parse(args[0]);
        from = args.Length > 1 ? int.Parse(args[1]) : 1;
        to = args.Length > 2 ? int.Parse(args[2]) : 65535;
        timeout = args.Length > 3 ? int.Parse(args[3]) : 250;
        int threads = 300;
        DateTime start = DateTime.UtcNow;
        Thread[] ts = new Thread[threads];
        for (int i = 0; i < threads; i++)
        {
            ts[i] = new Thread(Work);
            ts[i].IsBackground = true;
            ts[i].Start();
        }
        foreach (Thread t in ts) t.Join();
        Console.WriteLine("done " + (int)(DateTime.UtcNow - start).TotalSeconds + "s; open: " + string.Join(",", open));
    }

    static void Work()
    {
        while (true)
        {
            int p;
            lock (gate) { p = next++; }
            if (p > to) return;
            try
            {
                Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    IAsyncResult ar = s.BeginConnect(ip, p, null, null);
                    if (ar.AsyncWaitHandle.WaitOne(timeout, false) && s.Connected)
                    {
                        s.EndConnect(ar);
                        lock (gate) { open.Add(p); Console.WriteLine("OPEN " + p); }
                    }
                }
                catch { }
                finally { try { s.Close(); } catch { } }
            }
            catch { }
        }
    }
}
