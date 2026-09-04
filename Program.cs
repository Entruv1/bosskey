namespace BossKey;

internal static class Program
{
    private const string MutexName = @"Local\BossKey.BossKeyApp.SingleInstance";
    private const string ShowEventName = MutexName + ".Show";

    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var mutex = new Mutex(true, MutexName, out bool firstInstance);
        if (!firstInstance)
        {
            SignalExistingInstance();
            return;
        }

        using var showRequest = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        Application.Run(new MainForm(showRequest));
    }

    private static void SignalExistingInstance()
    {
        // 旧实例可能还在启动中，事件句柄尚未建立，稍等重试几次。
        for (int i = 0; i < 40; i++)
        {
            try
            {
                using var showEvent = EventWaitHandle.OpenExisting(ShowEventName);
                showEvent.Set();
                return;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                Thread.Sleep(50);
            }
        }
    }
}
