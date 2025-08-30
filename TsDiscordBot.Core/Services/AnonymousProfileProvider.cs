namespace TsDiscordBot.Core.Services;

public record AnonymousProfile(string Name, string? AvatarUrl);

public static class AnonymousProfileProvider
{
    private static readonly AnonymousProfile[] Profiles =
    [
        new("クリス", null),
        new("ジェシカ", null),
        new("マイク", null),
        new("サンドラ", null),
        new("ローラ", null),
        new("ショーン", null),
        new("ジェイ", null),
        new("ゲイル", null),
        new("ペネロペ", null),
        new("マイク", null),
        new("ロディ", null),
        new("メリル", null),
        new("メアリー", null),
        new("カミラ", null),
        new("ヒュー", null),
        new("フェイ", null),
        new("チャン", null),
        new("アンナ", null),
        new("ミカ", null),
        new("フレディ", null),
        new("エリック", null),
        new("ビル", null),
        new("フランク", null),
        new("アーニー", null),
        new("ソフィア", null),
        new("トーマス", null),
        new("リリアン", null),
        new("ポール", null),
        new("バニラ", null),
        new("スーザン", null),
        new("エマ", null),
        new("マリアンヌ", null),
        new("ニック", null),
        new("モーガン", null),
        new("ケイト", null),
        new("セリーヌ", null),
        new("ウィル", null),
    ];

    public static AnonymousProfile GetProfile(ulong userId)
    {
        var index = (int)(userId & 0xff);
        return Profiles[index % Profiles.Length];
    }

    public static string GetDiscriminator(ulong userId)
    {
        return (userId % 10000).ToString("D4");
    }
}

