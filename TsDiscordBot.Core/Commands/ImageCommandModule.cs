using System.IO;
using System.Net.Http;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Services;
using TsDiscordBot.Core;

namespace TsDiscordBot.Core.Commands;

public class ImageCommandModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger _logger;
    private readonly IOpenAIImageService _imageService;

    public ImageCommandModule(ILogger<ImageCommandModule> logger, IOpenAIImageService imageService)
    {
        _logger = logger;
        _imageService = imageService;
    }

    [SlashCommand("image", "説明文から画像を生成します")]
    public async Task GenerateImage(string description)
    {
        await DeferAsync();
        await ModifyOriginalResponseAsync(msg => msg.Content = "生成中です...");

        try
        {
            var results = await _imageService.GenerateAsync(description, 1, 1024);
            if (results.Count == 0)
            {
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
                await FollowupAsync("画像生成に失敗しました。");
                return;
            }

            var dir = Path.GetDirectoryName(Envs.LITEDB_PATH);
            if (string.IsNullOrWhiteSpace(dir))
            {
                dir = ".";
            }
            Directory.CreateDirectory(dir);
            var filePath = Path.Combine(dir, $"generated_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.png");
            await File.WriteAllBytesAsync(filePath, imageBytes);

            await FollowupWithFileAsync(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate image");
            await FollowupAsync("画像生成中にエラーが発生しました。");
        }
    }
}

