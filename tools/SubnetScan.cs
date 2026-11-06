using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

class SubnetScan
{
    static string prefix;
    static int port, timeout, next;
    static readonly object gate = new object();

    static void Main(string[] args)
    {
        prefix = args[0];              // e.g. 192.0.2. (documentation-only range)
        port = int.Parse(args[1]);
        timeout = args.Length > 2 ? int.Parse(args[2]) : 400;
        Thread[] ts = new Thread[64];
        for (int i = 0; i < ts.Length; i++) { ts[i] = new Thread(Work); ts[i].IsBackground = true; ts[i].Start(); }
        foreach (Thread t in ts) t.Join();
    }

    static void Work()
    {
        while (true)
        {
            int h;
            lock (gate) { h = next++; }
            if (h > 254) return;
            string ip = prefix + h;
            try
            {
                Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    IAsyncResult ar = s.BeginConnect(IPAddress.Parse(ip), port, null, null);
                    if (ar.AsyncWaitHandle.WaitOne(timeout, false) && s.Connected)
                    {
                        s.EndConnect(ar);
                        Console.WriteLine("OPEN " + ip + ":" + port);
                    }
                }
                catch { }
                finally { try { s.Close(); } catch { } }
            }
            catch { }
        }
    }
}
