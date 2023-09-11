using Newtonsoft.Json;

namespace Watcherr;

public static class Functions
{
    public static bool DryRun;

    public static T GetEnvironment<T>(string variable_name, T default_value)
    {
        var env = Environment.GetEnvironmentVariable(variable_name);
        if (string.IsNullOrEmpty(env)) { return default_value; }

        if (typeof(T) == typeof(int)) { return (T)(object)Convert.ToInt32(env); }

        return default_value;
    }

    public static async Task<T?> GetApiObject<T>(string url, string key, HttpMethod method, HttpClient http)
    {
        var response = await MakeRequest(url, key, method, http);
        if (response is null) { return default; }

        try
        {
            var obj = JsonConvert.DeserializeObject<T>(await response.Content.ReadAsStringAsync(), new JsonSerializerSettings() { Error = (s, a) => { Console.Error.WriteLine(a.ErrorContext.Error.Message); }});
            return obj;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync(ex.Message);
            return default;
        }   
    }

    public static async Task<HttpResponseMessage?> MakeRequest(string url, string key, HttpMethod method, HttpClient http)
    {
        if (Functions.DryRun && (method == HttpMethod.Post || method == HttpMethod.Delete || method == HttpMethod.Patch || method == HttpMethod.Put))
        {
            Console.WriteLine($"Not sending '{method.Method}' to '{url}' because DRYRUN is ON!");
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        }

        try
        {
            var request = new HttpRequestMessage(method, url);
            request.Headers.Add("X-Api-Key", key);
            
            var response = await http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return response;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync(ex.Message);
            return null;
        }
    }
}