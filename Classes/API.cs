using Newtonsoft.Json;

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

    public bool IsOK { get; private set; }

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

        URL = GetEnvironmentVariable("URL");
        Key = GetEnvironmentVariable("KEY");

        int.TryParse(GetEnvironmentVariable("STALLED_REMOVE_PERCENT_THRESHOLD", defaultValue: "2"), out Stalled_RemovePercentThreshold);
        bool.TryParse(GetEnvironmentVariable("STALLED_REMOVE", defaultValue: "false"), out Stalled_Remove);
        bool.TryParse(GetEnvironmentVariable("STALLED_REMOVE_FROM_CLIENT", defaultValue: "true"), out Stalled_RemoveFromClient);
        bool.TryParse(GetEnvironmentVariable("STALLED_BLOCKLIST_RELEASE", defaultValue: "false"), out Stalled_BlocklistRelease);
        bool.TryParse(GetEnvironmentVariable("UNMONITORED_REMOVE", defaultValue: "false"), out Unmonitored_RemoveFromLibrary);
        bool.TryParse(GetEnvironmentVariable("UNMONITORED_DELETE_FILES", defaultValue: "false"), out Unmonitored_DeleteFiles);
    }

    private string GetEnvironmentVariable(string envname, bool required = true, string defaultValue = "")
    {
        var env = Environment.GetEnvironmentVariable($"{Name.ToUpper()}_{envname.ToUpper()}") ?? "";
        if (string.IsNullOrEmpty(env)) 
        { 
            LogError($"Environment variable {Name.ToUpper()}_{envname.ToUpper()} not set!{(!string.IsNullOrEmpty(defaultValue) ? $" Setting default value '{defaultValue}.'": "")}"); 
            
            if (!string.IsNullOrEmpty(defaultValue)) { return defaultValue; }
            IsOK = !required; 
        }

        if (int.TryParse(env, out var i))
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
        Log("Getting initial info about instance");
        SystemStatus = await Functions.GetApiObject<SystemStatus>($"{URL}{API_SUFFIX}/system/status", Key, HttpMethod.Get);

        if (SystemStatus is null || string.IsNullOrEmpty(SystemStatus?.Version)) 
        { 
            LogError("Some problem with connecting to instance. Marking it as failed until next interval!");
            IsOK = false; 
            return;
        }

        if (Enum.TryParse(SystemStatus!.AppName, true, out AppTypes type))
        {
            AppType = type;
        }

        IsOK = true;
        Log($"Found '{SystemStatus.AppName}' version '{SystemStatus.Version}'.");
    }

    public async Task DeleteUnmonitored()
    {
        if (!Unmonitored_RemoveFromLibrary) { return; }
        Log("Searching for unmonitored shows...");

        var apiApp = AppType switch
        {
            AppTypes.Radarr => "movie",
            AppTypes.Sonarr => "series",
            _ => ""
        };

        var info = await Functions.GetApiObject<List<MovieSeriesObject>>($"{URL}{API_SUFFIX}/{apiApp}", Key, HttpMethod.Get);
        if (info is null) { return; }
        
        Log($"Found {info.Count(x => !x.Monitored)} unmonitored shows");
        foreach (var unmonitored in info.Where(x => !x.Monitored))
        {
            Log($"Removing unmonitored show: ID '{unmonitored.ID}' Title '{unmonitored.Title}'");
            await Functions.MakeRequest($"{URL}{API_SUFFIX}/{apiApp}/{unmonitored.ID}?deleteFiles={Unmonitored_DeleteFiles}", Key, HttpMethod.Delete);
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

    private async Task GetQueue()
    {
        Log("Getting Queue");
        Queue = await Functions.GetApiObject<Queue>($"{URL}{API_SUFFIX}/queue", Key, HttpMethod.Get);
        Log($"Found {Queue?.Records.Count ?? 0} records in queue");
    }

    public async Task DeleteFromQueueBulk(IEnumerable<int> ids)
    {
        var data = new { Ids = ids };
        await Functions.MakeRequest($"{URL}{API_SUFFIX}/queue/bulk?removeFromClient={Stalled_RemoveFromClient}&blocklist={Stalled_BlocklistRelease}", Key, HttpMethod.Delete, data);
    }

    private async Task DeleteFromQueue(Record record)
    {
        if (record.PercentDownloaded > Stalled_RemovePercentThreshold) { return; }       

        Log($"Removing stalled download ID '{record.ID}' Title '{record.Title ?? "<unknown>"}' Downloaded '{record.PercentDownloaded}%' Threshold '{Stalled_RemovePercentThreshold}%'");
        await Functions.MakeRequest($"{URL}{API_SUFFIX}/queue/{record.ID}?removeFromClient={Stalled_RemoveFromClient}&blocklist={Stalled_BlocklistRelease}", Key, HttpMethod.Delete);
        await SearchMonitored(record);
    }

    private async Task SearchMonitored(Record record)
    {
        Log($"Searching for monitored show ID '{record.ID}' Title '{record.Title ?? "<unknown>"}'");
        await SearchMonitored(record.ID, false);
    }

    private async Task SearchMonitored(int id, bool log = true)
    {
        if (log)
            Log($"Searching for monitored show '{id}'");

        await Functions.MakeRequest($"{URL}{API_SUFFIX}/command", Key, HttpMethod.Post,
            new { name = "SeriesSearch", seriesID = id });
    }
    
    private void Log(string text) => Functions.Log(text, URL);
    private void LogError(string text) => Functions.Log(text, URL);
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

    public double PercentDownloaded => Size > 0 ? ((Size - (double)SizeLeft) / Size) * 100.0 : 0.0;

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