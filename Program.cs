using Watcherr;
using Watcherr.Classes.API;

internal class Program
{
    public static int INTERVAL { get; private set; }
    public static HttpClient? Http { get; private set; }
    public static List<API> APIs = new List<API>()
    {
        new API("RADARR"),
        new API("SONARR")
    };

    static async Task Main(string[] args)
    {
        if (int.TryParse(Environment.GetEnvironmentVariable("INTERVAL"), out int i))
        {
            INTERVAL = i;
        }
        else { INTERVAL = 600; }

        Http = new HttpClient();

        var timer = new PeriodicTimer(TimeSpan.FromSeconds(INTERVAL));

        while (await timer.WaitForNextTickAsync())
        {
            
        }
    }
}

