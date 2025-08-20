using System.IO;
using System.Net.Http;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Services;
using TsDiscordBot.Core;

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

    private Task RunProgressAsync(CancellationToken token)
    {
        return Task.Run(async () =>
        {
            var seconds = 0;
            while (!token.IsCancellationRequested)
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = $"{seconds}秒経過中...");
                seconds++;
                try
                {
                    await Task.Delay(1000, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        });
    }

    [SlashCommand("image", "説明文から画像を生成します")]
    public async Task GenerateImage(string description)
    {
        if (!_limitService.TryAdd(Context.User.Id, "image"))
        {
            await RespondAsync("このコマンドは1時間に3回まで利用できます。", ephemeral: true);
            return;
        }

        await DeferAsync();
        using var cts = new CancellationTokenSource();
        var progressTask = RunProgressAsync(cts.Token);

        try
        {
            var results = await _imageService.GenerateAsync(description, 1, 256);
            if (results.Count == 0)
            {
                cts.Cancel();
                await progressTask;
                await ModifyOriginalResponseAsync(msg => msg.Content = "画像生成に失敗しました。");
                await FollowupAsync("画像生成に失敗しました。");
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
                cts.Cancel();
                await progressTask;
                await ModifyOriginalResponseAsync(msg => msg.Content = "画像生成に失敗しました。");
                await FollowupAsync("画像生成に失敗しました。");
                return;
            }

            var dataDir = Path.GetDirectoryName(Envs.APP_DATA_PATH) ?? ".";
            Directory.CreateDirectory(dataDir);
            var filePath = Path.Combine(dataDir, $"generated_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.png");
            await File.WriteAllBytesAsync(filePath, imageBytes);

            cts.Cancel();
            await progressTask;
            await ModifyOriginalResponseAsync(msg => msg.Content = "画像ができたよ！");
            await FollowupWithFileAsync(filePath);
        }
        catch (Exception ex)
        {
            cts.Cancel();
            await progressTask;
            _logger.LogError(ex, "Failed to generate image");
            await ModifyOriginalResponseAsync(msg => msg.Content = "画像生成中にエラーが発生しました。");
            await FollowupAsync("画像生成中にエラーが発生しました。");
        }
    }

    [SlashCommand("image-detail", "詳細を指定して画像を生成します")]
    public async Task GenerateImageDetail(
        string description,
        [MinValue(1), MaxValue(3)] int count = 1)
    {
        if (!_limitService.TryAdd(Context.User.Id, "image-detail", 1, TimeSpan.FromHours(8)))
        {
            await RespondAsync("このコマンドは8時間に1回まで利用できます。", ephemeral: true);
            return;
        }

        await DeferAsync();
        using var cts = new CancellationTokenSource();
        var progressTask = RunProgressAsync(cts.Token);

        try
        {
            count = Math.Clamp(count, 1, 3);
            var results = await _imageService.GenerateAsync(description, count, 1024);
            if (results.Count == 0)
            {
                cts.Cancel();
                await progressTask;
                await ModifyOriginalResponseAsync(msg => msg.Content = "画像生成に失敗しました。");
                await FollowupAsync("画像生成に失敗しました。");
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
                    imageBytes = await http.GetByteArrayAsync(result.Uri);
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
                cts.Cancel();
                await progressTask;
                await ModifyOriginalResponseAsync(msg => msg.Content = "画像生成に失敗しました。");
                await FollowupAsync("画像生成に失敗しました。");
                return;
            }

            cts.Cancel();
            await progressTask;
            await ModifyOriginalResponseAsync(msg => msg.Content = "画像ができたよ！");
            await FollowupWithFilesAsync(attachments);
        }
        catch (Exception ex)
        {
            cts.Cancel();
            await progressTask;
            _logger.LogError(ex, "Failed to generate image");
            await ModifyOriginalResponseAsync(msg => msg.Content = "画像生成中にエラーが発生しました。");
            await FollowupAsync("画像生成中にエラーが発生しました。");
        }
    }
}

