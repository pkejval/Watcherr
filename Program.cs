using System.Text.Json;
using Watcherr;
using Watcherr.Classes.API;

internal class Program
{
    public static int INTERVAL { get; private set; }
    public static List<API> APIs = new();

    static async Task Main(string[] args)
    {
#if DEBUG
        //Functions.DryRun = true;

        if (File.Exists(".env"))
        {
            foreach (var line in await File.ReadAllLinesAsync(".env"))
            {
                var split_line = line.Split('=', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (split_line.Length != 2) { continue; }
                Environment.SetEnvironmentVariable(split_line[0].ToUpper(), split_line[1]);
            }
        }
#endif
        var APIs_definition = Environment.GetEnvironmentVariable("APIS") ?? "";

        foreach (var a in APIs_definition.Split(new char[] {' ', ',', ';'}, StringSplitOptions.RemoveEmptyEntries))
        {
            APIs.Add(new API(a.ToUpper()));
        }

        await GetInitialInfo();

        if (!APIs.Any())
        {
            Console.Error.WriteLine("No APIs defined in environment variable APIS. Exiting!");
            Environment.Exit(2);
        }

        if (int.TryParse(Environment.GetEnvironmentVariable("INTERVAL"), out int i))
        {
            INTERVAL = i;
        }
        else { INTERVAL = 600; }

        var timer = new PeriodicTimer(TimeSpan.FromSeconds(INTERVAL));

        await DoWork();

        while (await timer.WaitForNextTickAsync())
        {
            await DoWork();
        }
    }

    static async Task GetInitialInfo()
    {
        foreach (var api in APIs)
        {
            await api.GetInitialInfo();
        }
    }

    static async Task DoWork()
    {
        foreach (var api in APIs.Where(x => x.IsOK))
        {
            await api.DeleteUnmonitored();
            await api.DeleteStalled();
        }
    }
}

