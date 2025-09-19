namespace SynPatcher;
public enum LogMode
{
    NONE, NORMAL, DEBUG
}
public class ElevenLabs
{
    public string key = "";
    public string voice_id = "";
    public string model_id = "eleven_flash_v2_5";
    public VoiceSettings voice_settings = new();
    public bool dry_run = true;
    public LogMode log_mode = LogMode.NORMAL;
    public Dictionary<string, HashSet<string>> replacementLists = new(){
        {"Alias=Player", ["Vrel"]}
    };
}

struct Request(string t, ElevenLabs apiinfo)
{
    public string text = t;
    public VoiceSettings voice_settings = apiinfo.voice_settings;
    public string model_id = apiinfo.model_id;
}
public class VoiceSettings
{
    public VoiceSettings()
    {
        stability = 0.5;
        similarity_boost = 0.75;
        style = 0.5;
        use_speaker_boost = false;
    }
    public double speed = 1.0;
    public double stability;
    public double similarity_boost;
    public double style;
    public bool use_speaker_boost;
}