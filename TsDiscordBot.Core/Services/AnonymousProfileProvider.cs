namespace TsDiscordBot.Core.Services;

public record AnonymousProfile(string Name, string? AvatarUrl);

public static class AnonymousProfileProvider
{
    private static readonly string s_assetBaseUri = "https://raw.githubusercontent.com/p4j4dyxcry/Discord.NET-bot/refs/heads/main/assets/";

    private static string GetAssetUri(string filename) => s_assetBaseUri + filename;

    private static readonly AnonymousProfile[] Profiles =
    [
        new("クリス", GetAssetUri("01.png")),
        new("ジェシカ", GetAssetUri("02.png")),
        new("マイク", GetAssetUri("07.png")),
        new("サンドラ", GetAssetUri("05.png")),
        new("ローラ", GetAssetUri("03.png")),
        new("ショーン", GetAssetUri("04.png")),
        new("ジェイ", GetAssetUri("17.png")),
        new("ゲイル", GetAssetUri("10.png")),
        new("ペネロペ", GetAssetUri("34.png")),
        new("ロディ", GetAssetUri("35.png")),
        new("メリル", GetAssetUri("26.png")),
        new("メアリー", GetAssetUri("19.png")),
        new("カミラ", GetAssetUri("24.png")),
        new("ヒュー", GetAssetUri("11.png")),
        new("フェイ", GetAssetUri("12.png")),
        new("チャン", GetAssetUri("25.png")),
        new("アンナ", GetAssetUri("08.png")),
        new("ミカ", GetAssetUri("36.png")),
        new("フレディ", GetAssetUri("09.png")),
        new("エリック", GetAssetUri("27.png")),
        new("ビル", GetAssetUri("20.png")),
        new("フランク", GetAssetUri("06.png")),
        new("アーニー", GetAssetUri("14.png")),
        new("ソフィア", GetAssetUri("16.png")),
        new("トーマス", GetAssetUri("22.png")),
        new("リリアン", GetAssetUri("13.png")),
        new("ポール", GetAssetUri("32.png")),
        new("バニラ", GetAssetUri("21.png")),
        new("スーザン", GetAssetUri("18.png")),
        new("エマ", GetAssetUri("23.png")),
        new("マリアンヌ", GetAssetUri("15.png")),
        new("ニック", GetAssetUri("28.png")),
        new("モーガン", GetAssetUri("31.png")),
        new("ケイト", GetAssetUri("29.png")),
        new("セリーヌ", GetAssetUri("33.png")),
        new("ウィル", GetAssetUri("30.png")),
    ];

    public static AnonymousProfile GetProfile(ulong userId)
    {
        var index = (int)(userId & 0xff);
        return Profiles[index % Profiles.Length];
    }

    public static IEnumerable<AnonymousProfile> GetProfiles() => Profiles;

    public static AnonymousProfile? GetProfileByName(string name)
    {
        return Profiles.FirstOrDefault(p => p.Name == name);
    }

    public static string GetDiscriminator(ulong userId)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        var discriminator = (uint)(userId + (uint)today.Day) % 10000;
        return discriminator.ToString("D4");
    }
}

