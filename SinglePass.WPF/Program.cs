using System;
using Velopack;

namespace SinglePass.WPF
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // Must run first: handles install/uninstall/update hooks (and exits for them)
            // and applies an update downloaded during the previous session.
            VelopackApp.Build().Run();

            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
    }
}
