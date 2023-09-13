using Newtonsoft.Json;
using static Watcherr.Functions;

namespace Watcherr.Classes.API;

public class API 
{
    public string Name { get; private set; }
    public string URL { get; private set; }
    public string Key { get; private set; }

    public bool Stalled_Remove;
    public bool Stalled_RemoveFromClient;
    public bool Stalled_BlocklistRelease;
    public int Stalled_RemovePercentThreshold;
    public bool Unmonitored_RemoveFromLibrary;
    public bool Unmonitored_DeleteFiles;

    public bool IsOK { get; private set; } = true;

    public Queue? Queue { get; private set; }
    public SystemStatus? SystemStatus { get; private set; }

    public AppTypes AppType { get; private set; }

    public const string API_SUFFIX = "/api/v3";

    public enum AppTypes 
    {
        Unknown,
        Radarr,
        Sonarr
    }

    public API(string name)
    {
        Name = name;

        URL = GetEnvironmentVariable("URL", true);
        Key = GetEnvironmentVariable("KEY", true);

        int.TryParse(GetEnvironmentVariable("STALLED_REMOVE_PERCENT_THRESHOLD", default_value: "2"), out Stalled_RemovePercentThreshold);
        bool.TryParse(GetEnvironmentVariable("STALLED_REMOVE", default_value: "false"), out Stalled_Remove);
        bool.TryParse(GetEnvironmentVariable("STALLED_REMOVE_FROM_CLIENT", default_value: "true"), out Stalled_RemoveFromClient);
        bool.TryParse(GetEnvironmentVariable("STALLED_BLACKLIST_RELEASE", default_value: "false"), out Stalled_BlocklistRelease);
        bool.TryParse(GetEnvironmentVariable("UNMONITORED_REMOVE", default_value: "false"), out Unmonitored_RemoveFromLibrary);
        bool.TryParse(GetEnvironmentVariable("UNMONITORED_DELETE_FILES", default_value: "false"), out Unmonitored_DeleteFiles);
    }

    private string GetEnvironmentVariable(string envname, bool required = true, string default_value = "")
    {
        var env = Environment.GetEnvironmentVariable($"{Name.ToUpper()}_{envname.ToUpper()}") ?? "";
        if (string.IsNullOrEmpty(env)) 
        { 
            Console.Error.WriteLine($"Environment variable {Name.ToUpper()}_{envname.ToUpper()} not set!{(!string.IsNullOrEmpty(default_value) ? $" Setting default value '{default_value}.'": "")}"); 
            
            if (!string.IsNullOrEmpty(default_value)) { return default_value; }
            IsOK = !required; 
        }

        if (int.TryParse(env, out int i))
        {
            env = i switch
            {
                < 1 => "false",
                _ => "true"
            };
        }

        return env;
    }

    public async Task GetInitialInfo()
    {
        Console.WriteLine($"Getting initial info for '{URL}'");
        SystemStatus = await Functions.GetApiObject<SystemStatus>($"{URL}{API_SUFFIX}/system/status", Key, HttpMethod.Get);

        if (SystemStatus is null || string.IsNullOrEmpty(SystemStatus?.Version)) 
        { 
            await Console.Error.WriteLineAsync($"Some problem contanting '{URL}'. Marking it as failed until Watcherr restart!");
            IsOK = false; 
            return;
        }
        if (Enum.TryParse(SystemStatus!.AppName, true, out AppTypes type))
        {
            AppType = type;
        }

        Console.WriteLine($"Found '{SystemStatus.AppName}' version '{SystemStatus.Version}' at '{URL}'");
    }

    public async Task DeleteUnmonitored()
    {
        if (!Unmonitored_RemoveFromLibrary) { return; }
        Console.WriteLine($"Searching for unmonitored shows at '{URL}'");

        var api_app = AppType switch
        {
            AppTypes.Radarr => "movie",
            AppTypes.Sonarr => "series",
            _ => ""
        };

        var info = await Functions.GetApiObject<List<MovieSeriesObject>>($"{URL}{API_SUFFIX}/{api_app}", Key, HttpMethod.Get);
        if (info is null) { return; }

        foreach (var unmonitored in info.Where(x => !x.Monitored))
        {
            Console.WriteLine($"Removing unmonitored show from '{URL}': ID {unmonitored.ID} Title '{unmonitored.Title}'");
            await Functions.MakeRequest($"{URL}{API_SUFFIX}/{api_app}/{unmonitored.ID}?deleteFiles={Unmonitored_DeleteFiles}", Key, HttpMethod.Delete);
        }
    }

    public async Task DeleteStalled()
    {
        if (!Stalled_Remove) { return; }

        await GetQueue();
        if (Queue is null || !Queue.Records.Any()) { return; }

        foreach (var stalled in Queue.Records.Where(x => x is not null &&
        (x!.Status?.Equals("warning", StringComparison.OrdinalIgnoreCase) ?? false) && (x!.ErrorMessage?.Contains("stalled", StringComparison.OrdinalIgnoreCase) ?? false)
            || ((x!.Status?.Equals("queued", StringComparison.OrdinalIgnoreCase) ?? false) && x!.Size == 0)))
        {
            await DeleteFromQueue(stalled);
        }

        // var stalled = Queue.Records.Where(x => x is not null &&
        //     (x!.Status?.Equals("warning", StringComparison.OrdinalIgnoreCase) ?? false) && (x!.ErrorMessage?.Contains("stalled", StringComparison.OrdinalIgnoreCase) ?? false)
        //      || ((x!.Status?.Equals("queued", StringComparison.OrdinalIgnoreCase) ?? false) && x!.Size == 0)).Select(x => x.ID);

        // await DeleteFromQueueBulk(stalled);
    }

    public async Task GetQueue()
    {
        Console.WriteLine($"Getting Queue for '{URL}'");
        Queue = await Functions.GetApiObject<Queue>($"{URL}{API_SUFFIX}/queue", Key, HttpMethod.Get);
    }

    public async Task DeleteFromQueueBulk(IEnumerable<int> Ids)
    {
        var data = new { Ids };
        await Functions.MakeRequest($"{URL}{API_SUFFIX}/queue/bulk?removeFromClient={Stalled_RemoveFromClient}&blocklist={Stalled_BlocklistRelease}", Key, HttpMethod.Delete, data);
    }

    public async Task DeleteFromQueue(Record record)
    {
        if (record.PercentDownloaded > Stalled_RemovePercentThreshold) { return; }       

        Console.WriteLine($"Removing stalled download from '{URL}': ID '{record.ID}' Title '{record.Title ?? "<unknown>"}' Downloaded '{record.PercentDownloaded}%' Threshold '{Stalled_RemovePercentThreshold}%'");
        await Functions.MakeRequest($"{URL}{API_SUFFIX}/queue/{record.ID}?removeFromClient={Stalled_RemoveFromClient}&blocklist={Stalled_BlocklistRelease}", Key, HttpMethod.Delete);
    }
}

public class SystemStatus
{
    [JsonProperty("appName")]
    public string? AppName { get; private set; }
    [JsonProperty("version")]
    public string? Version { get; private set; }
}

public class MovieSeriesObject
{
    public int ID { get; set; }
    public string? Title { get; set; }
    
    [JsonProperty("monitored")]
    public bool Monitored { get; set; }
}

public class Queue
{
    [JsonProperty("records")]
    public List<Record> Records { get; set; } = new();
}

public class Record
{
    public int ID { get; set; }
 
    public string? Title { get; set; }

    public long Size { get; set; }
    public long SizeLeft { get; set; }

    public decimal PercentDownloaded => Convert.ToDecimal(Size > 0 ? ((Size - SizeLeft) / Size) * 100 : 0);

    [JsonProperty("status")]
    public string? Status { get; set; }

    [JsonProperty("trackedDownloadStatus")]
    public string? DownloadStatus { get; set; }

    [JsonProperty("trackedDownloadState")]
    public string? DownloadState { get; set; }

    [JsonProperty("errorMessage")]
    public string? ErrorMessage { get; set; }
}

public class MovieSeriesInfo
{
    [JsonProperty("id")]
    public int ID { get; set; }
    [JsonProperty("title")]
    public string? Title { get; set; }
}