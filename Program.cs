using Watcherr.Classes.API;
using static Watcherr.Functions;

internal class Program
{
    private static int INTERVAL { get; set; }
    private static readonly List<API> APIs = new();

    private static async Task Main(string[] args)
    {
        try
        {  
            if (File.Exists(".env"))
            {
                foreach (var line in await File.ReadAllLinesAsync(".env"))
                {
                    var splitLine = line.Split('=', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (splitLine.Length != 2) { continue; }
                    Environment.SetEnvironmentVariable(splitLine[0].ToUpper(), splitLine[1]);
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to load/parse .env file!\n{ex.Message}");
        }
        
        var APIs_definition = Environment.GetEnvironmentVariable("APIS") ?? "";

        foreach (var a in APIs_definition.Split(new[] {' ', ',', ';'}, StringSplitOptions.RemoveEmptyEntries))
        {
            APIs.Add(new API(a.ToUpper()));
        }

        if (!APIs.Any())
        {
            LogError("No APIs are defined in environment variable APIS. Exiting!");
            Environment.Exit(2);
        }

        INTERVAL = int.TryParse(Environment.GetEnvironmentVariable("INTERVAL"), out var i) 
            ? i 
            : 600;

        var timer = new PeriodicTimer(TimeSpan.FromSeconds(INTERVAL));

        await DoWork();

        while (await timer.WaitForNextTickAsync())
        {
            await DoWork();
        }
    }

    private static async Task DoWork()
    {
        var tasks = APIs.Where(x => !x.IsOK)
            .Select(failed => failed.GetInitialInfo())
            .ToList();

        await Task.WhenAll(tasks);

        foreach (var api in APIs.Where(x => x.IsOK))
        {
            tasks.Add(api.DeleteUnmonitored());
            tasks.Add(api.DeleteStalled());
        }

        await Task.WhenAll(tasks);
    }
}

