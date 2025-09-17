using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.Amuse;

public class PlayDiceService(int bet, DatabaseService databaseService)
    : PlayGameServiceBase(bet, databaseService)
{
    protected override string GameKind => "DI";
    protected override string InProgressMessage =>
        "現在サイコロ勝負をプレイ中です。5分後に再試行してください。";
    protected override string StartMessage => "サイコロ勝負のゲームを開始します。";
}

