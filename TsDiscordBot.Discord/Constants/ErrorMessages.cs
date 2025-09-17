namespace TsDiscordBot.Discord.Constants;

public static class ErrorMessages
{
    public const string CommandLimitExceeded = "このコマンドは1時間に3回まで利用できます。";
    public const string CommandLimitExceededLong = "このコマンドは8時間に1回まで利用できます。";
    public const string ReferenceFileNotImage = "参照ファイルは画像ではありません。";
    public const string ImageGenerationFailed = "画像生成に失敗しました。";
    public const string BannedWordAddFailed = "⚠️ 禁止ワードの登録に失敗しました。";
    public const string BannedWordRemoveFailed = "⚠️ 禁止ワードの削除に失敗しました。";
    public const string InsufficientQuota = "@tsunetama token を使いきったみたいだだからチャージしてね！";
    public const string ContentPolicyViolationQuestion = "ごめんね、その質問には答えられないの。";
    public const string ContentPolicyViolationImage = "ごめんね、その画像は作成できないの。";
}
