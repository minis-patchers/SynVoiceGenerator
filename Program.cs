using Mutagen.Bethesda;
using Mutagen.Bethesda.Json;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Plugins.Cache;
using Newtonsoft.Json;
using Noggog;
using System.Data;
using System.Diagnostics;
using System.Reflection;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Mutagen.Bethesda.Environments;

namespace SynPatcher;

public static class Program
{
    public static IEnumerable<string> GenerateTemplatedCartesianProduct(
        this Dictionary<string, HashSet<string>> data,
        string template)
    {
        IEnumerable<Dictionary<string, string>> combinations = [[]];
        foreach (var entry in data)
        {
            combinations = combinations.SelectMany(
                existingCombination => entry.Value.Select(
                    value =>
                    {
                        var newCombination = new Dictionary<string, string>(existingCombination);
                        newCombination[entry.Key] = value;
                        return newCombination;
                    }));
        }
        foreach (var combination in combinations)
        {
            string formattedString = template;
            foreach (var kvp in combination)
            {
                formattedString = formattedString.Replace($"<{kvp.Key}>", kvp.Value);
            }
            yield return formattedString;
        }
    }
    public static async Task DownloadFileAsync(string fileUrl, string destinationPath)
    {
        try
        {
            HttpClient dlclient = new();
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            var resp = await dlclient.GetStreamAsync(fileUrl);
            await resp.CopyToAsync(fileStream);
            Log($"Downloaded: {destinationPath}", LogMode.NORMAL);
        }
        catch (HttpRequestException ex)
        {
            Log($"HTTP error: {ex.Message}", LogMode.NORMAL);
        }
        catch (IOException ex)
        {
            Log($"File I/O error: {ex.Message}", LogMode.NORMAL);
        }
    }
    static HashSet<LineTracker> lines = [];
    public static APIConfig APIInfo = new();
    static readonly HttpClient client = new();
    static string EDFP = string.Empty;
    static string mp3 = string.Empty;
    static string wav = string.Empty;
    static string lip = string.Empty;
    static string xwm = string.Empty;
    static string fuz = string.Empty;
    static JsonSerializerSettings settings = new();
    static string CKTools = string.Empty;
    static FileStream log = File.Open("LogFile.txt", FileMode.Create);
    public static void Main(string[] args)
    {
        settings.AddMutagenConverters();
        settings.Formatting = Formatting.Indented;
        Console.WriteLine("Creating Game Env");
        var env = Mutagen.Bethesda.Environments.GameEnvironmentBuilder.Create(GameRelease.SkyrimSE);
        var ge = env.Build();
        if (ge == null)
        {
            Log($"Unable to build Game Environment", LogMode.NORMAL);
            Console.WriteLine("Errored: press enter to exit!");
            var ek = Console.ReadKey();
            while (ek.Key != ConsoleKey.Enter) { ek = Console.ReadKey(); }
            return;
        }
        Console.WriteLine("GE Built");
        var asm = Assembly.GetExecutingAssembly();
        EDFP = AppDomain.CurrentDomain.BaseDirectory;
        if (!File.Exists(Path.Join(EDFP, "BmlFuzEncode.exe")))
        {
            using (Stream? stream = asm.GetManifestResourceStream("VoiceGen.Data.BmlFuzEncode.exe"))
            {
                if (stream == null)
                {
                    Console.WriteLine($"Missing embedded Resouce");
                    return;
                }
                using (Stream FO = new FileStream("BmlFuzEncode.exe", FileMode.Create))
                {
                    stream.CopyTo(FO);
                }
            }
        }
        Directory.CreateDirectory(Path.Join(EDFP, "tmp"));
        if (!File.Exists(Path.Join(EDFP, "ffmpeg.exe")))
        {
            if (!File.Exists(Path.Join(EDFP, "tmp", "FFMPEG.zip")))
            {
                DownloadFileAsync("https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-n8.1-latest-win64-gpl-8.1.zip", Path.Join(EDFP, "tmp", "FFMPEG.zip")).Wait();
                using var zip = ZipFile.OpenRead(Path.Join(EDFP, "tmp", "FFMPEG.zip"));
                var ent = zip.Entries.Where(x => x.Name == "ffmpeg.exe").First();
                ent.ExtractToFile(Path.Join(EDFP, "ffmpeg.exe"));
            }
        }
        if (!File.Exists(Path.Join(EDFP, "FaceFXWrapper.exe")))
        {
            DownloadFileAsync("https://github.com/Nukem9/FaceFXWrapper/releases/download/0.41/FaceFXWrapper.0.41.zip", Path.Join(EDFP, "tmp", "facefx.zip")).Wait();
            using var zip = ZipFile.OpenRead(Path.Join(EDFP, "tmp", "facefx.zip"));
            var ffxw = zip.Entries.Where(x => x.Name == "FaceFXWrapper.exe").First();
            ffxw.ExtractToFile(Path.Join(EDFP, "FaceFXWrapper.exe"));
        }
        if (!File.Exists("Config.json"))
        {
            File.WriteAllText("Config.json", JsonConvert.SerializeObject(APIInfo, settings));
        }
        else
        {
            var conf = JsonConvert.DeserializeObject<APIConfig>(File.ReadAllText("Config.json"));
            APIInfo = conf != null ? conf : new();
        }
        Patch(ge);
        Console.WriteLine("Completed press enter to exit!");
        var rk = Console.ReadKey();
        while (rk.Key != ConsoleKey.Enter) { rk = Console.ReadKey(); }
    }

    static void LoadDataFromFiles(IGameEnvironment ge, string folder)
    {
        foreach (var directories in Directory.EnumerateDirectories(folder))
        {
            var dfi = new DirectoryInfo(directories);
            Log($"{dfi.Name}, Loading files for this directory", LogMode.DEBUG);
            if (ge.LoadOrder.ModExists(dfi.Name))
            {
                Log($"Loading files for {dfi.Name}", LogMode.NORMAL);
                foreach (var file in dfi.GetFiles())
                {
                    FormKey key = FormKey.Factory($"{Path.GetFileNameWithoutExtension(file.Name)}:{dfi.Name}");
                    if (ge.LinkCache.TryResolve<IDialogTopicGetter>(key, out var ve))
                    {
                        var lin = lines.Where(x => x.forms.Any(x => ge.LinkCache.TryResolve<IDialogTopicGetter>(x, out var re) && $"{re.Name}".CleanString() == $"{ve.Name}".CleanString())).FirstOrDefault(new LineTracker());
                        Log($"Attemping to load file for {key}", LogMode.DEBUG);
                        var vdata = JsonConvert.DeserializeObject<HashSet<VariantData>>(File.ReadAllText(file.FullName));
                        if (vdata != null)
                        {
                            if (lin.forms.Any())
                            {
                                Log($"Merging line with {lin.forms.First()}", LogMode.DEBUG);
                            }
                            if (!lin.forms.Contains(key))
                            {
                                lin.forms.Add(key);
                            }
                            lin.variants.Add(vdata);
                            lin.variants = lin.variants.DistinctBy(x => x.guid).ToHashSet();
                            if (!lines.Contains(lin))
                            {
                                lines.Add(lin);
                            }
                        }
                        else
                        {
                            Log($"Error loading file {file}", LogMode.NORMAL);
                        }
                    }
                }
            }
        }
    }
    static void Patch(IGameEnvironment state)
    {
        var parent = Directory.GetParent(state.DataFolderPath);
        CKTools = Path.Join(parent?.FullName, "Tools");
        if (!File.Exists("FonixData.cdf"))
        {
            File.Copy(Path.Join(CKTools, "LipGen", "LipGenerator", "FonixData.cdf"), "FonixData.cdf");
        }
        if (!File.Exists(Path.Join(EDFP, "xWMAEncode.exe")))
        {
            File.Copy(Path.Join(CKTools, "Audio", "xWMAEncode.exe"), Path.Join(EDFP, "xWMAEncode.exe"));
        }
        var voice_directory = Path.Join(state.DataFolderPath, "Sound", "VPC", "DefaultVoice");
        var voice_sound = Path.Join(voice_directory, "Voice");
        if (!Directory.Exists(voice_sound))
            Directory.CreateDirectory(voice_sound);
        var voice_data = Path.Join(voice_directory, "Data");
        if (Directory.Exists(voice_data))
        {
            LoadDataFromFiles(state, voice_data);
        }
        var vgroot = Path.Join(EDFP, "VGOutput");
        mp3 = Path.Join(vgroot, "mp3");
        wav = Path.Join(vgroot, "wav");
        lip = Path.Join(vgroot, "lip");
        xwm = Path.Join(vgroot, "xwm");
        fuz = Path.Join(vgroot, "fuz");
        Directory.CreateDirectory(mp3);
        Directory.CreateDirectory(wav);
        Directory.CreateDirectory(lip);
        Directory.CreateDirectory(xwm);
        Directory.CreateDirectory(fuz);
        client.BaseAddress = new Uri(APIInfo.api_server);
        client.Timeout = TimeSpan.FromMinutes(5);
        foreach (var (Name, FormKey, Responses, EDID) in state.LoadOrder.PriorityOrder.WinningOverrides<IDialogTopicGetter>().Where(x => $"{x.Name}" != x.EditorID && x.Category == DialogTopic.CategoryEnum.Topic).Where(x => !$"{$"{x.Name}".CleanString()}".IsNullOrEmpty()).Select(x => ($"{x.Name}".CleanString(), x.FormKey, x.Responses, $"{x.EditorID}")))
        {
            var line = lines.Where(x => x.forms.Any(x => state.LinkCache.TryResolve<IDialogTopicGetter>(x, out var re) && $"{re.Name}".CleanString() == Name)).FirstOrDefault(new LineTracker());
            try
            {
                if (!Name.Skip())
                    ProcLine(Name, FormKey, state.LinkCache, EDID, line);
                if (Responses.Any(x => x.Prompt != null))
                {
                    var cs = Responses.Where(x => x.Prompt != null && !x.Prompt.ToString()!.IsNullOrEmpty()).Select(x => x.Prompt!.ToString()!.CleanString()).Where(x => !x.IsNullOrEmpty() && x != Name && !x.Skip()).Distinct();
                    Log($"Generating {cs.Count()} prompts", LogMode.NORMAL);
                    GenVints(cs.AsEnumerable(), Name, FormKey, state.LinkCache, EDID, line);
                    Log($"Generated Prompts", LogMode.NORMAL);
                }
                if (!line.variants.Any()) continue;
                else if (!lines.Contains(line)) lines.Add(line);
                if (!Directory.Exists(Path.Join(voice_data, FormKey.ModKey.ToString())))
                    Directory.CreateDirectory(Path.Join(voice_data, FormKey.ModKey.ToString()));
                foreach (var vd in line.variants)
                {
                    var fp = Path.Join(voice_sound, $"{vd.guid}.fuz");
                    var ep = Path.Join(EDFP, "VGOutput", "fuz", $"{vd.guid}.fuz");
                    if (File.Exists(fp) && !File.Exists(ep))
                    {
                        File.Copy(fp, ep);
                    }
                    if (!File.Exists(fp))
                    {
                        File.Copy(ep, fp, true);
                    }
                    if (!File.Exists(fp) && !File.Exists(ep))
                    {
                        Log($"WARN FILE DOES NOT EXISTS {vd.guid}.fuz", LogMode.NORMAL);
                    }
                }
                foreach (var vint in line.forms)
                {
                    var jso = Path.Join(voice_data, vint.ModKey.ToString(), $"{vint.IDString()}.json");
                    if (line.variants.Count > 0)
                    {
                        Log($"Writing file for {vint}", LogMode.DEBUG);
                        File.WriteAllText(jso, JsonConvert.SerializeObject(line.variants, settings));
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"{ex.Message}", LogMode.NORMAL);
                break;
            }
        }
    }
    static void GenVints(IEnumerable<string> vints, string OName, FormKey formKey, ILinkCache linkCache, string EDID, LineTracker line)
    {
        foreach (var Name in vints)
        {
            if (EDID == Name) { continue; }
            if (!Name.Contains('<') && !Name.Contains('>') && !(Name.StartsWith('(') && Name.EndsWith(')')) && !(Name.StartsWith('[') && !Name.EndsWith(']')) && !(Name.EndsWith('*') && Name.StartsWith('*')) && !Name.Contains('_') && !Name.StartsWith('$'))
            {
                var vinc = OName.GetWordDifferences(Name);
                if (Name.Skip()) { Log($"Skipping (EINVAL): {Name}", LogMode.NORMAL); continue; }
                else if (!vinc.Any() && line.variants.Where(x => x.reg_frags == null).Count() >= APIInfo.iterations) { Log($"Skipping: {Name}", LogMode.NORMAL); continue; }
                else if (vinc.Any() && line.variants.Where(x => x.reg_frags != null && x.reg_frags.VerifyString(Name)).Count() >= APIInfo.iterations) { Log($"Skipping (VINT MAIN): {Name}", LogMode.NORMAL); continue; }
                else
                {
                    Console.WriteLine($"Genning for FormKey: {formKey}");
                }
                var dat = Generate(Name);
                if (dat != null)
                {
                    VariantData vd = new()
                    {
                        guid = dat.Value.guid,
                        splen = dat.Value.splen,
                        reg_frags = vinc,
                    };
                    if (!vd.reg_frags.Any())
                    {
                        vd.reg_frags = null;
                    }
                    line.variants.Add(vd);
                    if (!line.forms.Contains(formKey))
                    {
                        line.forms.Add(formKey);
                    }
                }
            }
            //One of many different possible type variant data.
            else
            {
                var cont = APIInfo.replacementLists.Where(x => Name.Contains($"<{x.Key}>")).ToDictionary();
                foreach (var tline in cont.GenerateTemplatedCartesianProduct(Name))
                {
                    Log($"{tline}", LogMode.NORMAL);
                    if (!tline.Contains('<') && !tline.Contains('>'))
                    {
                        var vinc = OName.GetWordDifferences(tline);
                        if (tline.Skip()) { Log($"Skipping (EINVAL): {tline}", LogMode.NORMAL); continue; }
                        else if (line.variants.Where(x => x.reg_frags != null && x.reg_frags.VerifyString(tline)).Count() >= APIInfo.iterations) { Log($"Skipping (VINT REPL): {tline}", LogMode.NORMAL); continue; }
                        var ld = Generate(tline);
                        if (ld != null)
                        {
                            line.variants.Add(new VariantData()
                            {
                                guid = ld.Value.guid,
                                splen = ld.Value.splen,
                                reg_frags = vinc,
                            });
                            if (!line.forms.Contains(formKey))
                            {
                                line.forms.Add(formKey);
                            }
                        }
                    }
                }
            }
        }
    }
    static void ProcLine(string Name, FormKey FormKey, ILinkCache linkCache, string EDID, LineTracker line)
    {
        GenVints([Name], Name, FormKey, linkCache, EDID, line);
    }

    static void Log(string lt, LogMode md, bool toFile = true)
    {
        if (APIInfo.log_mode == LogMode.NONE) return;
        if (APIInfo.log_mode >= md)
        {
            if (toFile)
            {
                log.Write(Encoding.UTF8.GetBytes(lt + "\n"));
                log.Flush();
            }
            Console.WriteLine(lt);
        }
    }
    static LineData? Generate(string text)
    {
        var guid = Guid.NewGuid().ToString().ToUpper();
        while (lines.Any(x => x.variants.Any(x => x.guid == $"{guid}")) || File.Exists(Path.Join(EDFP, "VGOutput", "fuz", $"{guid}.fuz")))
        {
            Log("Regenerating identical guid", LogMode.DEBUG);
            guid = Guid.NewGuid().ToString().ToUpper();
        }
        var mp3name = Path.Join(mp3, $"{guid}.mp3");
        var wavname = Path.Join(wav, $"{guid}.wav");
        var rwavnam = Path.Join(wav, $"{guid}.resamp.wav");
        var lipname = Path.Join(lip, $"{guid}.lip");
        var xwmname = Path.Join(xwm, $"{guid}.xwm");
        var fuzname = Path.Join(fuz, $"{guid}.fuz");
        if (!File.Exists(mp3name) && !File.Exists(fuzname))
        {
            Log($"Generating: {text}", LogMode.NORMAL);
            StringContent stringContent = new(JsonConvert.SerializeObject(new Request(text, APIInfo)), Encoding.UTF8, "application/json");
            var cli = client.PostAsync($"/v1/audio/speech", stringContent);
            cli.Wait();
            if (cli.Result.IsSuccessStatusCode)
            {
                var file = File.OpenWrite(wavname);
                cli.Result.Content.CopyTo(file, null, CancellationToken.None);
                file.Close();
            }
            else
            {
                Log($"error: {cli.Result.StatusCode}", LogMode.NORMAL);
                throw new Exception(cli.Result.Content.ToString());
            }
        }
        if (!File.Exists(fuzname) && File.Exists(wavname))
        {

            var p = new ProcessStartInfo($"{EDFP}/ffmpeg.exe")
            {
                Arguments = $"-i \"{wavname}\" -ac 1 \"{mp3name}\""
            };
            var d = Process.Start(p);
            if (!d!.WaitForExit(20000)) d.Kill();
            LineData ret = new()
            {
                guid = guid,
                splen = Exts.GetMp3Duration(mp3name),
            };
            p.FileName = $"{EDFP}/FaceFXWrapper.exe";
            p.Arguments = $"Skyrim USEnglish FonixData.cdf \"{wavname}\" \"{rwavnam}\" \"{lipname}\" \"{text.Replace("\"", "\\\"")}\"";
            d = Process.Start(p);
            if (!d!.WaitForExit(20000)) d.Kill();
            p.FileName = $"{EDFP}/xWMAEncode.exe";
            p.Arguments = $"\"{wavname}\" \"{xwmname}\"";
            d = Process.Start(p);
            if (!d!.WaitForExit(20000)) d.Kill();
            p.FileName = $"{EDFP}/BmlFuzEncode.exe";
            p.Arguments = $"\"{fuzname}\" \"{xwmname}\" \"{lipname}\"";
            d = Process.Start(p);
            if (!d!.WaitForExit(20000)) d.Kill();
            Log($"Generated {text}", LogMode.NORMAL);
            return ret;
        }
        else
        {
            Log($"Skipping {text}", LogMode.NORMAL);
        }
        return null;
    }
}

public static partial class REG
{
    [GeneratedRegex("<([^=]*)=([^>]*)>")]
    private static partial Regex QuestAlias();
    public static Regex TextAliases => QuestAlias();
    [GeneratedRegex("(\\s?\\(.*\\)\\s?)")]
    private static partial Regex Hidden();
    public static Regex HiddenFN => Hidden();
    [GeneratedRegex("(\\s?\\[.*\\]\\s?)")]
    private static partial Regex Hidden2();
    public static Regex HiddenFN2 => Hidden2();
}
