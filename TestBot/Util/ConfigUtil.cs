using Microsoft.Extensions.Configuration;

namespace TestBot;

public class ConfigUtil
{
    private static IConfiguration config;
    
    private static ConfigUtil? instance;

    private ConfigUtil() { }

    static ConfigUtil() {
        config = new ConfigurationBuilder()
            .AddJsonFile("bot.json")
            .AddEnvironmentVariables()
            .Build();
    }

    public static T GetProperty<T>(ConfigKey<T> key)
    {
        return config.GetValue<T>(key.Value);
    }

    public static ConfigUtil getInstance()
    {
        return instance ??= new ConfigUtil();
    }
    
     
}

public class ConfigKey<T>
{
    public ConfigKey(string value) { Value = value; }
        
    public string Value { get; }
    
    public override string ToString()
    {
        return "Value: " + Value;
    }
}

public abstract class ConfigKey
{
    public static ConfigKey<string> DB_HOSTNAME => new("Database:Hostname");
    public static ConfigKey<string> DB_USERNAME => new("Database:User");
    public static ConfigKey<string> DB_PASSWORD => new("Database:Password");
    public static ConfigKey<string> DB_DATABASE => new("Database:Database");
    public static ConfigKey<string> LOG_LEVEL => new("Logging:Level");
    public static ConfigKey<bool> LOG_DATABASE => new("Logging:Database");
    public static ConfigKey<string> BOT_TOKEN => new("Bot:Token");
}

