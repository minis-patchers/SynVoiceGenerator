namespace SynPatcher;

public enum LogMode
{
    NONE, NORMAL, DEBUG
}
public class ElevenLabs
{
    public string lang = "en";
    public bool dry_run = true;
    public LogMode log_mode = LogMode.NORMAL;
    public Dictionary<string, HashSet<string>> replacementLists = new(){
        {"Alias=Player", ["Vrel"]}
    };
}

struct Request(string t, ElevenLabs apiinfo)
{
    public string text = t;
    public string lang = apiinfo.lang;
}
