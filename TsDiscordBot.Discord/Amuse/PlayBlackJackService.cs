using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.Amuse;

public class PlayBlackJackService(int bet, DatabaseService databaseService)
    : PlayGameServiceBase(bet, databaseService)
{
    protected override string GameKind => "BJ";
    protected override string InProgressMessage =>
        "現在ブラックジャックをプレイ中です。5分後に再試行してください。";
    protected override string StartMessage => "ブラックジャックのゲームを開始します。";
}

