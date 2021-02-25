using System;
using Topshelf;
using Topshelf.Logging;

namespace OneSignalService
{
    class Program
    {
        static void Main(string[] args)
        {
            HostFactory.Run(configure =>
            {
                configure.UseLog4Net();

                configure.Service<OneSignal>(service =>
                {
                    service.ConstructUsing(p => new OneSignal());
                    service.WhenStarted(p => p.Start());
                    service.WhenStopped(p => p.Stop());
                });

                configure.SetServiceName($"OneSignal Service_v{Properties.Settings.Default.Version}");
                configure.SetDisplayName("OneSignal Service");
                configure.SetDescription($"OnSignal module for dajiang");

                // 啟動類型
                configure.StartAutomatically();        // 自動

                // 執行身分
                configure.RunAsLocalSystem();
                
                // 復原選項
                configure.EnableServiceRecovery(sr =>
                {
                    // 第一次失敗時（延遲 30 秒後重新啟動服務）
                    sr.RestartService(TimeSpan.FromSeconds(30));
                });
            });
        }
    }
}
