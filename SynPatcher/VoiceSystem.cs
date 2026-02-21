namespace SynPatcher;

public enum LogMode
{
    NONE, NORMAL, DEBUG
}

public class APIConfig
{
    public string lang = "en";
    public string voice = "Glinda";
    public string voice_run_id = "";
    public LogMode log_mode = LogMode.NORMAL;
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
    public string salt = apiinfo.voice_run_id;
}
