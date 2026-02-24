namespace SynPatcher;

public enum LogMode
{
    NONE, NORMAL, DEBUG
}

public class APIConfig
{
    public string lang = "en";
    public string voice = "Glinda";
    public int seed = 123;
    public string api_server = "http://localhost:8000";
    public LogMode log_mode = LogMode.NORMAL;
    public int print_every_gen = 1000;
    public Dictionary<string, HashSet<string>> replacementLists = new(){
        {"Alias=Player", []},
        {"Alias=CurrentRelic", ["Silver Arrow", "Leviathan Diamond"]}
    };
}

struct Request(string t, APIConfig apiinfo)
{
    public string text = t;
    public string lang = apiinfo.lang;
    public string voice = apiinfo.voice;
    public int seed = apiinfo.seed;
}
