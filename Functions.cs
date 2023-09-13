using System.Text;
using Newtonsoft.Json;

namespace Watcherr;

public static class Functions
{
    public static HttpClientHandler Handler { get; private set; } = new();
    public static HttpClient Http { get; private set; } = new(Handler);

    public static bool DryRun;

    public static T GetEnvironment<T>(string variable_name, T default_value)
    {
        var env = Environment.GetEnvironmentVariable(variable_name);
        if (string.IsNullOrEmpty(env)) { return default_value; }

        if (typeof(T) == typeof(int)) { return (T)(object)Convert.ToInt32(env); }

        return default_value;
    }

    public static async Task<T?> GetApiObject<T>(string url, string key, HttpMethod method)
    {
        var response = await MakeRequest(url, key, method, Http);
        if (response is null) { return default; }

        try
        {
            var obj = JsonConvert.DeserializeObject<T>(await response.Content.ReadAsStringAsync(), new JsonSerializerSettings() { Error = (s, a) => { LogError(url, a.ErrorContext.Error.Message); }});
            return obj;
        }
        catch (Exception ex)
        {
            LogError(ex.Message);
            return default;
        }   
    }

    public static async Task<HttpResponseMessage?> MakeRequest(string url, string key, HttpMethod method, object? data = null, bool form_content = false)
    {
        if (Functions.DryRun && (method == HttpMethod.Post || method == HttpMethod.Delete || method == HttpMethod.Patch || method == HttpMethod.Put))
        {
            Log($"Not sending '{method.Method}' to '{url}' because DRYRUN is ON!");
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        }

        try
        {
            var request = new HttpRequestMessage(method, url);
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("X-Api-Key", key);
            
            Http.DefaultRequestHeaders.Add("X-Api-Key", key);
            
            if (data is not null)
            {
                var json = JsonConvert.SerializeObject(data);

                if (form_content)
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

    public static void Log(string text, string? api_url = null, bool error = false)
    {
        var msg = $"[{DateTimeOffset.Now.ToLocalTime()}]{(!string.IsNullOrEmpty(api_url) ? $" [{api_url}] " : "")}[{(error ? "ERR": "INF")}] - {text}";
        
        if (error) Console.Error.WriteLine(msg);
        else Console.WriteLine(msg);
    }

    public static void LogError(string text, string? api_url = null) => Log(text, api_url, error: true);
}