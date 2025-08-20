using System.IO;
using System.Linq;
using System.Net.Http;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.HostedService
{
    public class ImageReviseService : IHostedService
    {
        private readonly DiscordSocketClient _client;
        private readonly ILogger<ImageReviseService> _logger;
        private readonly IOpenAIImageService _imageService;

        public ImageReviseService(DiscordSocketClient client, ILogger<ImageReviseService> logger, IOpenAIImageService imageService)
        {
            _client = client;
            _logger = logger;
            _imageService = imageService;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _client.MessageReceived += OnMessageReceivedAsync;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _client.MessageReceived -= OnMessageReceivedAsync;
            return Task.CompletedTask;
        }

        private async Task OnMessageReceivedAsync(SocketMessage message)
        {
            try
            {
                if (message.Author.IsBot)
                    return;

                if (message.Reference?.MessageId.IsSpecified != true)
                    return;

                if (!message.Content.StartsWith("!revise "))
                    return;

                var referenced = await message.Channel.GetMessageAsync(message.Reference.MessageId.Value);
                if (referenced is null || referenced.Author.Id != _client.CurrentUser.Id)
                    return;

                var attachment = referenced.Attachments.FirstOrDefault(a => a.ContentType?.StartsWith("image/") == true);
                if (attachment is null)
                    return;

                var prompt = message.Content.Substring("!revise ".Length);

                using var http = new HttpClient();
                await using var stream = await http.GetStreamAsync(attachment.Url);

                var results = await _imageService.EditAsync(stream, prompt, 1024, CancellationToken.None);
                if (results.Count == 0)
                    return;

                var dir = Path.GetDirectoryName(Envs.LITEDB_PATH);
                if (string.IsNullOrWhiteSpace(dir))
                {
                    dir = ".";
                }
                Directory.CreateDirectory(dir);

                var result = results[0];
                byte[] imageBytes;
                if (result.HasUri)
                {
                    imageBytes = await http.GetByteArrayAsync(result.Uri);
                }
                else if (result.HasBytes)
                {
                    imageBytes = result.Bytes!.Value.ToArray();
                }
                else
                {
                    return;
                }

                var filePath = Path.Combine(dir, $"revise_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.png");
                await File.WriteAllBytesAsync(filePath, imageBytes);

                await message.Channel.SendFileAsync(filePath, text: $"画像を修正したよ！\n\"{prompt}\"", messageReference: new MessageReference(message.Id));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to revise image");
            }
        }
    }
}

