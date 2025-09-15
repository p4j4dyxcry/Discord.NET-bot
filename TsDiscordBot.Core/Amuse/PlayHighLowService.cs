using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.Amuse;

public class PlayHighLowService(int bet, DatabaseService databaseService)
    : PlayGameServiceBase(bet, databaseService)
{
    protected override string GameKind => "HL";
    protected override string InProgressMessage =>
        "現在ハイ＆ローをプレイ中です。5分後に再試行してください。";
    protected override string StartMessage => "ハイ＆ローのゲームを開始します。";
}

