using Mutagen.Bethesda;
using Mutagen.Bethesda.Json;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using Newtonsoft.Json;
using Noggog;
using System.Data;
using System.Diagnostics;
using System.Reflection;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Mutagen.Bethesda.Environments;
using System.IO.Hashing;

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
    static HashSet<VariantData> lines = [];
    public static APIConfig APIInfo = new();
    static readonly HttpClient client = new();
    static string EDFP = string.Empty;
    static string mp3 = string.Empty;
    static string wav = string.Empty;
    static string lip = string.Empty;
    static string xwm = string.Empty;
    static string fuz = string.Empty;
    static string voice_data = string.Empty;
    static string voice_sound = string.Empty;
    static string vdp = string.Empty;
    static JsonSerializerSettings settings = new();
    static string CKTools = string.Empty;
    static List<VoiceDataExport> voices_in_esp = [];
    static FileStream log = File.Open("LogFile.txt", FileMode.Create);
    public static void Main(string[] args)
    {
        settings.AddMutagenConverters();
        settings.Formatting = Formatting.Indented;
        settings.NullValueHandling = NullValueHandling.Ignore;
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
            using Stream? stream = asm.GetManifestResourceStream("VoiceGen.Data.BmlFuzEncode.exe");
            if (stream == null)
            {
                Console.WriteLine($"Missing embedded Resouce");
                return;
            }
            using Stream FO = new FileStream("BmlFuzEncode.exe", FileMode.Create);
            stream.CopyTo(FO);
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
            APIInfo = conf ?? new();
        }
        Patch(ge);
        Console.WriteLine("Generation complete, press enter to exit!");
        var rk = Console.ReadKey();
        while (rk.Key != ConsoleKey.Enter) rk = Console.ReadKey();
    }

    static void LoadDataFromFiles(string folder)
    {
        foreach (var directories in Directory.EnumerateDirectories(folder))
        {
            var dfi = new DirectoryInfo(directories);
            Log($"Loading files for {dfi.Name}", LogMode.NORMAL);
            foreach (var file in dfi.GetFiles())
            {
                var vdata = JsonConvert.DeserializeObject<HashSet<VariantData>>(File.ReadAllText(file.FullName));
                if (vdata != null)
                {
                    lines.Add(vdata.Where(x => x.frag_hash != 0 && x.guid != string.Empty));
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
        var voice_directory = Path.Join(state.DataFolderPath, "Sound", "VPC", APIInfo.voice_id);
        voice_sound = Path.Join(voice_directory, "Voice");
        if (!Directory.Exists(voice_sound))
            Directory.CreateDirectory(voice_sound);
        voice_data = Path.Join(voice_directory, "Data");
        if (Directory.Exists(voice_data))
        {
            LoadDataFromFiles(voice_data);
        }
        if (File.Exists(Path.Join(voice_data, "index.json")))
        {
            var fdat = JsonConvert.DeserializeObject<HashSet<VariantData>>(File.ReadAllText(Path.Join(voice_data, "index.json")), settings);
            if (fdat != null)
                lines.Add(fdat);
        }
        lines = lines.DistinctBy(x => x.guid).ToHashSet();
        var vgroot = Path.Join(EDFP, "VGOutput", APIInfo.voice_id);
        Directory.CreateDirectory(vgroot);
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
        voices_in_esp = [];
        vdp = Path.Join(state.DataFolderPath, "SKSE", "VPC", $"{APIInfo.esp_name}.json");
        if (File.Exists(vdp))
        {
            voices_in_esp = JsonConvert.DeserializeObject<List<VoiceDataExport>>(File.ReadAllText(vdp))!;
            if (!voices_in_esp.Where(x => x.voice_id == APIInfo.voice_id).Any())
            {
                var vde = new VoiceDataExport()
                {
                    voice_id = APIInfo.voice_id,
                    friendly_name = APIInfo.VoiceConfiguration.voice,
                    voice_sample_id = APIInfo.VoiceConfiguration.voice
                };
                voices_in_esp.Add(vde);
            }
        }
        Console.CancelKeyPress += CancelPressHandler;
        foreach (var (Name, FormKey, Responses, EDID) in state.LoadOrder.PriorityOrder.WinningOverrides<IDialogTopicGetter>().Select(x => ($"{x.Name}".CleanString(), x.FormKey, x.Responses, $"{x.EditorID}")))
        {
            try
            {
                var cs = Responses.Where(x => x.Prompt != null).Where(x => x.Prompt != null).Select(x => x.Prompt!.ToString()!.CleanString()).Concat([Name]).DistinctBy(x => XxHash3.HashToUInt64(Encoding.UTF8.GetBytes(x.CleanString().RemovePunct().ToLower()))).Where(x => !x.DoSkip() && x != EDID).ToHashSet();
                if (cs.Count == 0) continue;
                Log($"Generating {cs.Count()} lines for form {FormKey}", LogMode.NORMAL);
                HashSet<VariantData> line = new();
                GenVints(cs, FormKey, line);
                if (line.Count > 0)
                {
                    foreach (var lin in line)
                    {
                        var out_path = Path.Join(voice_sound, $"{lin.guid}.fuz");
                        var gen_path = Path.Join(fuz, $"{lin.guid}.fuz");
                        if (!File.Exists(out_path))
                        {
                            File.Copy(gen_path, out_path, true);
                        }
                        else if (!File.Exists(out_path) && File.Exists(gen_path))
                        {
                            File.Copy(out_path, gen_path, true);
                        }
                        else
                        {
                            Log($"Skipping {lin.guid} - both out_path and gen_path exist", LogMode.NORMAL);
                            continue;
                        }
                    }
                    if (!Directory.Exists(Path.Join(voice_data, FormKey.ModKey.ToString()))) Directory.CreateDirectory(Path.Join(voice_data, FormKey.ModKey.ToString()));
                    WriteFile(FormKey, line);
                }
            }
            catch (Exception ex)
            {
                Log($"{ex.Message}", LogMode.NORMAL);
                break;
            }
        }
        DumpFiles();
    }
    public static void CancelPressHandler(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        DumpFiles();
        Environment.Exit(0);
    }
    public static void DumpFiles()
    {
        if (voices_in_esp.Count > 0)
        {
            File.WriteAllText(vdp, JsonConvert.SerializeObject(voices_in_esp, settings));
        }
        if (lines.Count > 0)
        {
            File.WriteAllText(Path.Join(voice_data, "index.json"), JsonConvert.SerializeObject(lines, settings));
        }
    }
    static void WriteFile(FormKey vint, IEnumerable<VariantData> variants)
    {
        if (variants.Count() == 0) return;
        var jso = Path.Join(voice_data, vint.ModKey.ToString(), $"{vint.IDString()}.json");
        File.WriteAllText(jso, JsonConvert.SerializeObject(variants, settings));
    }
    static void GenVints(IEnumerable<string> vints, FormKey formKey, HashSet<VariantData> line)
    {
        foreach (var Name in vints)
        {
            if (!Name.Contains('<') && !Name.Contains('>') && !(Name.StartsWith('(') && Name.EndsWith(')')) && !(Name.StartsWith('[') && !Name.EndsWith(']')) && !(Name.EndsWith('*') && Name.StartsWith('*')) && !Name.Contains('_') && !Name.StartsWith('$'))
            {
                var fh = XxHash3.HashToUInt64(Encoding.UTF8.GetBytes(Name.Normalize().RemovePunct().ToLower()));
                if (lines.Count(x => x.frag_hash == fh) >= APIInfo.iterations)
                {
                    line.Add(lines.Where(x => x.frag_hash == fh).AsEnumerable());
                    Log($"Skipping (VINT MAIN): {Name}", LogMode.NORMAL);
                    continue;
                }
                Log($"Lower: {Name.Normalize().RemovePunct().ToLower()}", LogMode.NORMAL);
                Log($"Hash: {fh}", LogMode.NORMAL);
                var dat = Generate(Name);
                if (dat != null)
                {
                    VariantData vd = new()
                    {
                        guid = dat.Value.guid,
                        length_ms = dat.Value.length_ms,
                        frag_hash = fh,
                    };
                    line.Add(vd);
                    lines.Add(vd);
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
                        var fh = XxHash3.HashToUInt64(Encoding.UTF8.GetBytes(tline.Normalize().RemovePunct().ToLower()));
                        if (tline.DoSkip()) { Log($"Skipping (EINVAL): {tline}", LogMode.NORMAL); continue; }
                        else if (lines.Count(x => x.frag_hash == fh) >= APIInfo.iterations)
                        {
                            line.Add(lines.Where(x => x.frag_hash == fh).AsEnumerable());
                            Log($"Skipping (VINT REPL): {tline}", LogMode.NORMAL);
                            continue;
                        }
                        Log($"Lower: {tline.Normalize().RemovePunct().ToLower()}", LogMode.NORMAL);
                        Log($"Hash: {fh}", LogMode.NORMAL);
                        var ld = Generate(tline);
                        if (ld != null)
                        {
                            var vd = new VariantData()
                            {
                                guid = ld.Value.guid,
                                length_ms = ld.Value.length_ms,
                                frag_hash = fh,
                            };
                            line.Add(vd);
                            lines.Add(vd);
                        }
                    }
                }
            }
        }
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
        while (lines.Any(x => x.guid == $"{guid}") || File.Exists(Path.Join(EDFP, "VGOutput", "fuz", $"{guid}.fuz")))
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
                length_ms = wavname.GetWavLengthInMilliseconds(),
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
