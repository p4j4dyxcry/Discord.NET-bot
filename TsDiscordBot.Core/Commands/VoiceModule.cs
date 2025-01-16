using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.Interactions;
using Newtonsoft.Json;
using VoicevoxClientSharp;
using RunMode = Discord.Interactions.RunMode;

public class VoiceCommands : InteractionModuleBase<SocketInteractionContext>
{
    private static ConcurrentDictionary<ulong,IAudioClient> _audioClients = new();

    public IAudioClient? GetCurrentAudioClient()
    {
        var voiceChannel = (Context.User as IGuildUser)?.VoiceChannel;

        if (voiceChannel is null)
        {
            return null;
        }

        return _audioClients.GetValueOrDefault(voiceChannel.Id);
    }

    [SlashCommand("join","join to chat",runMode: RunMode.Async)]
    public async Task Join()
    {
        var voiceChannel = (Context.User as IGuildUser)?.VoiceChannel;

        if (voiceChannel is null)
        {
            await ReplyAsync("ボイスチャンネルに接続してください。");
            return;
        }
        _audioClients[voiceChannel.Id] = await voiceChannel.ConnectAsync();
        await ReplyAsync($"{voiceChannel.Name} に接続しました。");
    }

    [SlashCommand("leave","leave from chat")]
    public async Task Leave()
    {
        var voiceChannel = (Context.User as IGuildUser)?.VoiceChannel;
        var audioClient = GetCurrentAudioClient();

        if (voiceChannel is null)
        {
            return;
        }

        if (audioClient is not null)
        {
            await audioClient.StopAsync();
            _audioClients.Remove(voiceChannel.Id,out _);
            await ReplyAsync("切断しました。");
            return;
        }

        await ReplyAsync("現在接続していません。");
    }

    [SlashCommand("say","say something")]
    public async Task Say([Remainder] string text)
    {
        var audioClient = GetCurrentAudioClient();

        if (audioClient is null)
        {
            await ReplyAsync("まずボイスチャンネルに接続してください。");
            return;
        }
        await ReplyAsync("メッセージ送信中");
        await SpeakAsync(audioClient, text);
    }

    public async Task SpeakAsync(IAudioClient audioClient, string text)
    {
        using var synthesizer = new VoicevoxSynthesizer();
        int styleId = (await synthesizer.FindStyleIdByNameAsync(speakerName: "ずんだもん", styleName: "あまあま"))!.Value;
        await synthesizer.InitializeStyleAsync(styleId);
        SynthesisResult result = await synthesizer.SynthesizeSpeechAsync(styleId, text);

        // AudioClient を使って音声データをストリームとして送信
        var audioStream = new MemoryStream(result.Wav);

        var tempFilePath = Path.GetTempFileName(); // 一時ファイルを作成
        await using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write))
        {
            await audioStream.CopyToAsync(fileStream);
        }

        await SendAsync(audioClient, tempFilePath);
    }

    private async Task SendAsync(IAudioClient client, string path)
    {
        // Create FFmpeg using the previous example
        using (var ffmpeg = CreateStream(path)!)
        using (var output = ffmpeg.StandardOutput.BaseStream)
        using (var discord = client.CreatePCMStream(AudioApplication.Mixed))
        {
            try
            {
                await output.CopyToAsync(discord);
            }
            finally
            {
                await discord.FlushAsync();
            }
        }
    }

    private Process? CreateStream(string path) {
        return Process.Start(new ProcessStartInfo {
            FileName = "ffmpeg.exe",
            Arguments = $"-hide_banner -loglevel panic -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
            UseShellExecute = false,
            RedirectStandardOutput = true
        });
    }
}