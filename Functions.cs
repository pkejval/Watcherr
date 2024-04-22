using System.Text;
using Newtonsoft.Json;

namespace Watcherr;

public static class Functions
{
    private static HttpClientHandler Handler { get; } = new();
    private static HttpClient Http { get; } = new(Handler);
    
    public static T GetEnvironment<T>(string variableName, T defaultValue)
    {
        var env = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrEmpty(env)) { return defaultValue; }

        if (typeof(T) == typeof(int)) { return (T)(object)Convert.ToInt32(env); }

        return defaultValue;
    }

    public static async Task<T?> GetApiObject<T>(string url, string key, HttpMethod method)
    {
        var response = await MakeRequest(url, key, method, Http);
        if (response is null) { return default; }

        try
        {
            var obj = JsonConvert.DeserializeObject<T>(await response.Content.ReadAsStringAsync(), new JsonSerializerSettings { Error = (_, a) => { LogError(url, a.ErrorContext.Error.Message); }});
            return obj;
        }
        catch (Exception ex)
        {
            LogError(ex.Message);
            return default;
        }   
    }

    public static async Task<HttpResponseMessage?> MakeRequest(string url, string key, HttpMethod method, object? data = null, bool formContent = false)
    {
        try
        {
            var request = new HttpRequestMessage(method, url);
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("X-Api-Key", key);
                        
            if (data is not null)
            {
                var json = JsonConvert.SerializeObject(data);

                if (formContent)
                {
                    var kvp = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    request.Content = new FormUrlEncodedContent(kvp!);
                }
                else
                {
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }
            }

            var response = await Http.SendAsync(request);
            return response;
        }
        catch (Exception ex)
        {
            LogError(ex.Message);
            return null;
        }
    }

    public static void Log(string text, string? apiUrl = null, bool error = false)
    {
        var msg = $"[{DateTimeOffset.Now.ToLocalTime()}]{(!string.IsNullOrEmpty(apiUrl) ? $" [{apiUrl}] " : "")}[{(error ? "ERR": "INF")}] - {text}";
        
        if (error) Console.Error.WriteLine(msg);
        else Console.WriteLine(msg);
    }

    public static void LogError(string text, string? apiUrl = null) 
        => Log(text, apiUrl, error: true);
}