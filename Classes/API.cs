namespace Watcherr.Classes.API;

public class API 
{
    public string Name { get; private set; }
    public string URL { get; private set; }
    public string Key { get; private set; }

    public bool IsOK { get; private set; } = true;

    public API(string name)
    {
        Name = name;

        URL = GetEnvironmentVariable("URL");
        Key = GetEnvironmentVariable("KEY");
    }

    private string GetEnvironmentVariable(string envname)
    {
        var env = Environment.GetEnvironmentVariable($"{Name.ToUpper()}_{envname.ToUpper()}") ?? "";
        if (string.IsNullOrEmpty(env)) { Console.Error.WriteLine($"Environment variable {Name.ToUpper()}_{envname.ToUpper()} not set!"); IsOK = false; }

        return env;
    }
}