internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var application = new Application();
        System.Windows.Forms.Application.Run(application);
    }
}
