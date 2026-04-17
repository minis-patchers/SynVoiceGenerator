namespace SynPatcher;

public enum LogMode
{
    NONE, NORMAL, DEBUG
}

public class APIConfig
{
    public VoiceSettings VoiceConfiguration = new();
    public string api_server = "http://localhost:8000";
    public int iterations = 1;
    public LogMode log_mode = LogMode.NORMAL;
    public Dictionary<string, HashSet<string>> replacementLists = new(){
        {"Alias=Player", []},
        {"Alias=CurrentRelic", ["Silver Arrow", "Leviathan Diamond"]}
    };
}

public class VoiceSettings
{
    public string lang = "English";
    public string voice = "Ranni";
    public float temperature = 0.4f;
    public int top_k = 50;
    public float top_p = 0.8f;
    public float rep_penalty = 1.1f;
}

struct Request(string t, APIConfig apiinfo)
{
    public string input = t;
    public string lang = apiinfo.VoiceConfiguration.lang;
    public string voice = apiinfo.VoiceConfiguration.voice;
    public float temperature = apiinfo.VoiceConfiguration.temperature;
    public int top_k = apiinfo.VoiceConfiguration.top_k;
    public float top_p = apiinfo.VoiceConfiguration.top_p;
    public float rep_penalty = apiinfo.VoiceConfiguration.rep_penalty;
}
