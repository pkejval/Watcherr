using Watcherr.Classes.API;
using static Watcherr.Functions;

internal class Program
{
    public static int INTERVAL { get; private set; }
    public static List<API> APIs = new();

    static async Task Main(string[] args)
    {
        try
        {  
            if (File.Exists(".env"))
            {
                foreach (var line in await File.ReadAllLinesAsync(".env"))
                {
                    var split_line = line.Split('=', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (split_line.Length != 2) { continue; }
                    Environment.SetEnvironmentVariable(split_line[0].ToUpper(), split_line[1]);
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to load/parse .env file!\n{ex.Message}");
        }
        
        var APIs_definition = Environment.GetEnvironmentVariable("APIS") ?? "";

        foreach (var a in APIs_definition.Split(new char[] {' ', ',', ';'}, StringSplitOptions.RemoveEmptyEntries))
        {
            APIs.Add(new API(a.ToUpper()));
        }

        if (!APIs.Any())
        {
            LogError("No APIs are defined in environment variable APIS. Exiting!");
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

    static async Task DoWork()
    {
        foreach (var failed in APIs.Where(x => !x.IsOK))
        {
            await failed.GetInitialInfo();
        }

        foreach (var api in APIs.Where(x => x.IsOK))
        {
            await api.DeleteUnmonitored();
            await api.DeleteStalled();
        }
    }
}

