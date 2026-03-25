using Uno.UI.Hosting;

namespace DevTools.Uno.Sample;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var exit = args.Any(arg => arg == "--exit");
        App.InitializeLogging();

        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App(exit))
            .UseX11()
            .UseLinuxFrameBuffer()
            .UseMacOS()
            .UseWin32()
            .Build();

        host.Run();
    }
}
