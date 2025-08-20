using System.Diagnostics;
using System.Text;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.Commands;

public class ImageCommandModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger _logger;
    private readonly IOpenAIImageService _imageService;
    private readonly IUserCommandLimitService _limitService;

    public ImageCommandModule(ILogger<ImageCommandModule> logger, IOpenAIImageService imageService, IUserCommandLimitService limitService)
    {
        _logger = logger;
        _imageService = imageService;
        _limitService = limitService;
    }

    private Task RunProgressAsync(string original, Stopwatch stopwatch, CancellationToken token)
    {
        return Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                StringBuilder builder = new(original);
                builder.AppendLine($"{stopwatch.Elapsed.Seconds}秒経過中...");

                await ModifyOriginalResponseAsync(msg => msg.Content = builder.ToString());
                try
                {
                    await Task.Delay(1000, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }, token);
    }

    private static string GetProgressMessage(string prompt)
    {
        return GetMessageWithPrompt("画像を作ってるよ！", prompt);
    }

    private static string GetFailedMessage(string prompt, int second)
    {
        return GetMessageWithPrompt($"画像生成に失敗しちゃった。経過時間={second}秒", prompt);
    }
    private static string GetSucceedMessage(string prompt, int second)
    {
        return GetMessageWithPrompt($"画像の作成が完了したよ！経過時間={second}秒", prompt);
    }

    private static string GetMessageWithPrompt(string message, string prompt)
    {
        StringBuilder progressMessageBuilder = new(message);
        progressMessageBuilder.AppendLine($"\"{prompt}\"");
        return progressMessageBuilder.ToString();
    }

    [SlashCommand("image", "説明文から画像を生成します")]
    public async Task GenerateImage(string description)
    {
        Stopwatch stopWatch = Stopwatch.StartNew();

        if (!_limitService.TryAdd(Context.User.Id, "image"))
        {
            await RespondAsync("このコマンドは1時間に3回まで利用できます。", ephemeral: true);
            return;
        }

        await DeferAsync();
        using var cts = new CancellationTokenSource();
        var progressTask = RunProgressAsync(GetProgressMessage(description), stopWatch, cts.Token);

        try
        {
            var results = await _imageService.GenerateAsync(description, 1, 256, cts.Token);
            if (results.Count == 0)
            {
                await cts.CancelAsync();
                await progressTask;
                await ModifyOriginalResponseAsync(msg => msg.Content = GetFailedMessage(description,stopWatch.Elapsed.Seconds));
                await FollowupAsync(GetFailedMessage(description,stopWatch.Elapsed.Seconds));
                return;
            }

            var result = results[0];
            byte[] imageBytes;

            if (result.HasUri)
            {
                using var http = new HttpClient();
                imageBytes = await http.GetByteArrayAsync(result.Uri);
            }
            else if (result.HasBytes)
            {
                imageBytes = result.Bytes!.Value.ToArray();
            }
            else
            {
                await cts.CancelAsync();
                await progressTask;
                await ModifyOriginalResponseAsync(msg => msg.Content = GetFailedMessage(description,stopWatch.Elapsed.Seconds));
                await FollowupAsync(GetFailedMessage(description,stopWatch.Elapsed.Seconds));
                return;
            }

            var dataDir = Path.GetDirectoryName(Envs.APP_DATA_PATH) ?? ".";
            Directory.CreateDirectory(dataDir);
            var filePath = Path.Combine(dataDir, $"generated_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.png");
            await File.WriteAllBytesAsync(filePath, imageBytes);

            cts.Cancel();
            await progressTask;
            await ModifyOriginalResponseAsync(msg => msg.Content = GetSucceedMessage(description,stopWatch.Elapsed.Seconds));
            await FollowupWithFileAsync(filePath);
        }
        catch (Exception ex)
        {
            cts.Cancel();
            await progressTask;
            _logger.LogError(ex, "Failed to generate image");
            await ModifyOriginalResponseAsync(msg => msg.Content = GetFailedMessage(description,stopWatch.Elapsed.Seconds));
            await FollowupAsync(GetFailedMessage(description,stopWatch.Elapsed.Seconds));
        }
    }

    [SlashCommand("image-detail", "詳細を指定して画像を生成します")]
    public async Task GenerateImageDetail(
        string description,
        [MinValue(1), MaxValue(3)] int count = 1)
    {
        Stopwatch stopWatch = Stopwatch.StartNew();

        if (!_limitService.TryAdd(Context.User.Id, "image-detail", 1, TimeSpan.FromHours(8)))
        {
            await RespondAsync("このコマンドは8時間に1回まで利用できます。", ephemeral: true);
            return;
        }

        await DeferAsync();
        using var cts = new CancellationTokenSource();
        var progressTask = RunProgressAsync(GetProgressMessage(description), stopWatch, cts.Token);

        try
        {
            count = Math.Clamp(count, 1, 3);
            var results = await _imageService.GenerateAsync(description, count, 1024, cts.Token);
            if (results.Count == 0)
            {
                cts.Cancel();
                await progressTask;
                await ModifyOriginalResponseAsync(msg => msg.Content = GetFailedMessage(description,stopWatch.Elapsed.Seconds));
                await FollowupAsync(GetFailedMessage(description,stopWatch.Elapsed.Seconds));
                return;
            }

            var dir = Path.GetDirectoryName(Envs.LITEDB_PATH);
            if (string.IsNullOrWhiteSpace(dir))
            {
                dir = ".";
            }
            Directory.CreateDirectory(dir);

            var attachments = new List<FileAttachment>();
            int index = 0;
            foreach (var result in results)
            {
                byte[] imageBytes;
                if (result.HasUri)
                {
                    using var http = new HttpClient();
                    imageBytes = await http.GetByteArrayAsync(result.Uri, cts.Token);
                }
                else if (result.HasBytes)
                {
                    imageBytes = result.Bytes!.Value.ToArray();
                }
                else
                {
                    continue;
                }

                var filePath = Path.Combine(dir, $"generated_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{index}.png");
                await File.WriteAllBytesAsync(filePath, imageBytes);
                attachments.Add(new FileAttachment(filePath));
                index++;
            }

            if (attachments.Count == 0)
            {
                await cts.CancelAsync();
                await progressTask;
                await ModifyOriginalResponseAsync(msg => msg.Content = GetFailedMessage(description,stopWatch.Elapsed.Seconds));
                await FollowupAsync("画像生成に失敗しました。");
                return;
            }

            await cts.CancelAsync();
            await progressTask;
            await ModifyOriginalResponseAsync(msg => msg.Content = GetSucceedMessage(description,stopWatch.Elapsed.Seconds));
            await FollowupWithFilesAsync(attachments);
        }
        catch (Exception ex)
        {
            await cts.CancelAsync();
            await progressTask;
            _logger.LogError(ex, "Failed to generate image");
            await ModifyOriginalResponseAsync(msg => msg.Content = GetFailedMessage(description,stopWatch.Elapsed.Seconds));
            await FollowupAsync(GetFailedMessage(description,stopWatch.Elapsed.Seconds));
        }
    }
}

