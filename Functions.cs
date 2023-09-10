namespace Watcherr;

public static class Functions
{
    public static T GetEnvironment<T>(string variable_name, T default_value)
    {
        var env = Environment.GetEnvironmentVariable(variable_name);
        if (string.IsNullOrEmpty(env)) { return default_value; }

        if (typeof(T) == typeof(int)) { return (T)(object)Convert.ToInt32(env); }

        return default_value;
    }
}