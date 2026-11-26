using System;
using Velopack;

namespace Erapkey
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // Must run first: handles install/uninstall/update hooks (and exits for them)
            // and applies an update downloaded during the previous session.
            VelopackApp.Build()
                // Browsers must not keep starting a host that is gone.
                .OnBeforeUninstallFastCallback(_ => Services.NativeMessagingRegistration.Unregister())
                .Run();

            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
    }
}
