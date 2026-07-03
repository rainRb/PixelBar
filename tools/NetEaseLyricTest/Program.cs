using PixelBar_App.Services.Lyrics;

var failures = 0;

if (args.Length > 0 && File.Exists(args[0]))
{
    TestRealFile(args[0]);
    return failures;
}

failures += Assert("YRC LRC-style line", TestYrcLrcStyle());
failures += Assert("YRC JSON line", TestYrcJsonLine());
failures += Assert("LRC parser", TestStandardLrc());
failures += Assert("Cache JSON file (prefer YRC)", TestSampleCacheFile());
failures += Assert("Escaped newline in JSON", TestEscapedNewlineJson());
failures += Assert("GetLineAt timing", TestGetLineAt());
failures += Assert("Filter BetterNCM disclaimer", TestFilterDisclaimer());
failures += Assert("Mixed YRC + LRC in cache", TestMixedYrcAndLrc());
failures += Assert("Title content disambiguation", TestTitleContentDisambiguation());
failures += Assert("Copyright disclaimer filtered", TestCopyrightDisclaimerFiltered());
failures += Assert("InfLink Genre NCM id", TestInfLinkGenreSongId());
failures += Assert("Numeric song id from cache JSON", TestNumericSongIdFromContent());

Console.WriteLine();
ScanLocalCache();

Console.WriteLine();
if (failures == 0)
{
    Console.WriteLine("全部测试通过。");
    return 0;
}

Console.WriteLine($"失败 {failures} 项。");
return 1;

static void TestRealFile(string path)
{
    Console.WriteLine($"--- 解析真实文件 ---");
    Console.WriteLine(path);
    var doc = NetEaseLyricCacheParser.TryParseFile(path);
    if (doc is null)
    {
        Console.WriteLine("[FAIL] 解析失败");
        return;
    }

    Console.WriteLine($"[OK] {doc.Lines.Count} 行");
    foreach (var seconds in new[] { 14.0, 20.0, 60.0, 120.0 })
    {
        var sample = TimeSpan.FromSeconds(seconds);
        Console.WriteLine($"  @{seconds:0}s -> {doc.GetLineAt(sample) ?? "(无)"}");
    }
}

static int Assert(string name, bool ok)
{
    Console.WriteLine(ok ? $"[PASS] {name}" : $"[FAIL] {name}");
    return ok ? 0 : 1;
}

static bool TestYrcLrcStyle()
{
    const string yrc = "[12000,1900](12000,360,0)初(12360,150,0)め(12510,120,0)て";
    var doc = YrcParser.Parse(yrc);
    return doc is not null
        && doc.Lines.Count == 1
        && doc.Lines[0].Text == "初めて"
        && doc.Lines[0].Time == TimeSpan.FromSeconds(12);
}

static bool TestYrcJsonLine()
{
    const string yrc = "{\"t\":5000,\"c\":[{\"tx\":\"你好\",\"tr\":[0,500]},{\"tx\":\"世界\",\"tr\":[500,800]}]}";
    var doc = YrcParser.Parse(yrc);
    return doc is not null
        && doc.Lines.Count == 1
        && doc.Lines[0].Text == "你好世界"
        && doc.Lines[0].Time == TimeSpan.FromSeconds(5);
}

static bool TestStandardLrc()
{
    const string lrc = """
        [ti:测试]
        [ar:歌手A]
        [00:01.00]开场
        [00:05.50]副歌
        """;
    var doc = LrcParser.Parse(lrc);
    return doc.Lines.Count == 2
        && doc.Title == "测试"
        && doc.Artist == "歌手A"
        && doc.Lines[1].Text == "副歌";
}

static bool TestSampleCacheFile()
{
    var samplePath = Path.Combine(AppContext.BaseDirectory, "samples", "5242612.json");
    if (!File.Exists(samplePath))
        return false;

    var doc = NetEaseLyricCacheParser.TryParseFile(samplePath);
    if (doc is null || doc.Lines.Count < 2)
        return false;

    // YRC 优先：第二句应在 18.5s 附近
    var line = doc.GetLineAt(TimeSpan.FromSeconds(19));
    return line == "第二句歌词";
}

static bool TestEscapedNewlineJson()
{
    const string json = """{"lrc":{"lyric":"[00:10.00]上行\\n[00:20.00]下行\\n"}}""";
    var doc = NetEaseLyricCacheParser.TryParseContent(json);
    return doc is not null
        && doc.Lines.Count == 2
        && doc.Lines[1].Text == "下行";
}

static bool TestGetLineAt()
{
    var doc = new LrcDocument
    {
        Lines =
        [
            new LyricLine(TimeSpan.FromSeconds(10), "A"),
            new LyricLine(TimeSpan.FromSeconds(20), "B"),
        ],
    };

    return doc.GetLineAt(TimeSpan.FromSeconds(15)) == "A"
        && doc.GetLineAt(TimeSpan.FromSeconds(20)) == "B"
        && doc.GetLineAt(TimeSpan.FromSeconds(9), userTimingOffsetMs: -1000) == "A";
}

static bool TestFilterDisclaimer()
{
    const string disclaimer = "所以如果你是从任何地方发现有任何人在售卖本工具，请立刻要求退款并举报商家！";
    var doc = new LrcDocument
    {
        Lines =
        [
            new LyricLine(TimeSpan.FromSeconds(0), "第一句"),
            new LyricLine(TimeSpan.FromSeconds(170), disclaimer),
            new LyricLine(TimeSpan.FromSeconds(175), "真正的歌词"),
        ],
    };

    return doc.GetSingableLineAt(TimeSpan.FromSeconds(170)) == "第一句";
}

static bool TestMixedYrcAndLrc()
{
    const string lyric = """
        {"t":0,"c":[{"tx":"作词: "},{"tx":"陈零九"}]}
        {"t":1000,"c":[{"tx":"作曲: "},{"tx":"陈零九"}]}
        [00:23.107]城市的燈像遠方的星球閃爍
        [03:13.562]把所有夢都燃燒成星河
        """;
    var doc = NetEaseLyricCacheParser.TryParseContent($"{{\"lrc\":{{\"lyric\":\"{lyric.Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n")}\"}}}}");
    return doc is not null
        && doc.Lines.Count >= 4
        && doc.GetSingableLineAt(TimeSpan.FromSeconds(195)) == "把所有夢都燃燒成星河";
}

static bool TestTitleContentDisambiguation()
{
    var wrong = new LrcDocument
    {
        Title = "一个人想着一个人",
        Lines = [new LyricLine(TimeSpan.FromSeconds(150), "爱怎会甘愿停止")],
    };
    var right = new LrcDocument
    {
        Title = "一个人想着一个人",
        Lines =
        [
            new LyricLine(TimeSpan.FromSeconds(85), "我一个人的冒险 一个人的座位"),
            new LyricLine(TimeSpan.FromSeconds(89), "一个人想着一个人"),
        ],
    };

    var index = new[]
    {
        new IndexedLyricEntry("a", "wrong", DateTime.UtcNow.AddMinutes(-1), wrong.Title, wrong.Artist, wrong),
        new IndexedLyricEntry("b", "right", DateTime.UtcNow, right.Title, right.Artist, right),
    };

    var picked = LyricMatchHelper.PickBestMatch(index, "一个人想着一个人", "张芸京");
    return picked == right;
}

static bool TestCopyrightDisclaimerFiltered()
{
    const string disclaimer = "「未经著作权人许可 不得翻唱 翻录或使用」";
    return LyricMetadataFilter.IsMetadataLine(disclaimer);
}

static bool TestInfLinkGenreSongId()
{
    return InfLinkSmtcParser.TryParseSongId("NCM-5242612") == "5242612"
        && InfLinkSmtcParser.TryParseSongIdFromSession(["NCM-5242612"], "续写", "毛不易", null, null) == "5242612"
        && InfLinkSmtcParser.IsNumericSongId("5242612")
        && !InfLinkSmtcParser.IsNumericSongId("a1b2c3d4e5f6789012345678901234ab");
}

static bool TestNumericSongIdFromContent()
{
    const string json = """{"musicId":5242612,"lrc":{"lyric":"[00:10.00]测试"}}""";
    return NetEaseLyricCacheParser.TryParseNumericSongIdFromContent(json) == "5242612";
}

static void ScanLocalCache()
{
    Console.WriteLine("--- 本机网易云歌词缓存扫描 ---");
    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    var cloudMusicRoot = Path.Combine(localAppData, "Netease", "CloudMusic");
    var expected = new[]
    {
        Path.Combine(cloudMusicRoot, "Temp"),
        Path.Combine(cloudMusicRoot, "webdata", "lyric"),
        Path.Combine(cloudMusicRoot, "Cache", "Lyric"),
        Path.Combine(cloudMusicRoot, "Cache", "LyricNew"),
    };

    var total = 0;
    foreach (var dir in expected)
    {
        if (!Directory.Exists(dir))
        {
            Console.WriteLine($"[缺失] {dir}");
            continue;
        }

        var files = Directory.EnumerateFiles(dir)
            .Where(f => !f.EndsWith(".dat", StringComparison.OrdinalIgnoreCase)
                && !f.EndsWith(".dat-journal", StringComparison.OrdinalIgnoreCase)
                && !f.EndsWith("index.dat", StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .ToList();
        Console.WriteLine($"[存在] {dir}（{files.Count} 个样本）");
        foreach (var file in files)
        {
            total++;
            var doc = NetEaseLyricCacheParser.TryParseFile(file);
            var preview = doc?.GetLineAt(TimeSpan.Zero) ?? "(解析失败)";
            Console.WriteLine($"  {Path.GetFileName(file)} -> {doc?.Lines.Count ?? 0} 行, 首行: {preview}");
        }
    }

    if (!Directory.Exists(cloudMusicRoot))
        Console.WriteLine($"[缺失] 网易云根目录: {cloudMusicRoot}");

    if (total == 0)
        Console.WriteLine("未找到可解析的本地缓存。请用网易云 PC 版完整播放一首歌（部分版本需先下载）后再运行本工具。");
}
