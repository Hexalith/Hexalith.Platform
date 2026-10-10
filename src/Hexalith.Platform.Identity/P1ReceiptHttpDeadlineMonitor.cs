namespace Hexalith.Platform.Identity;

/// <summary>One process-wide boottime poller with a fixed active-deadline capacity.</summary>
internal static class P1ReceiptHttpDeadlineMonitor
{
    internal const int MaxActive = 128;
    private static readonly object Gate = new();
    private static readonly HashSet<P1ReceiptHttpDeadline> Active = [];
    private static readonly AutoResetEvent Changed = new(false);
    private static readonly Thread Worker = StartWorker();

    internal static int ActiveCount
    {
        get
        {
            lock (Gate)
            {
                return Active.Count;
            }
        }
    }

    internal static bool TryRegister(P1ReceiptHttpDeadline deadline)
    {
        _ = Worker;
        lock (Gate)
        {
            if (Active.Count >= MaxActive)
            {
                return false;
            }

            Active.Add(deadline);
        }

        Changed.Set();
        return true;
    }

    internal static void Unregister(P1ReceiptHttpDeadline deadline)
    {
        lock (Gate)
        {
            Active.Remove(deadline);
        }
    }

    private static Thread StartWorker()
    {
        var thread = new Thread(Run) { IsBackground = true, Name = "P1 boottime deadline monitor" };
        thread.Start();
        return thread;
    }

    private static void Run()
    {
        while (true)
        {
            P1ReceiptHttpDeadline[] deadlines;
            lock (Gate)
            {
                deadlines = [.. Active];
            }

            if (deadlines.Length == 0)
            {
                Changed.WaitOne();
                continue;
            }

            foreach (P1ReceiptHttpDeadline deadline in deadlines)
            {
                try
                {
                    deadline.Poll();
                }
                catch (ObjectDisposedException)
                {
                    // A completed request may dispose after the monitor snapshot.
                }
            }

            Changed.WaitOne(20);
        }
    }
}
