using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Constants;
using TsDiscordBot.Core.Framework;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.HostedService
{
    public class ImageReviseService : IHostedService
    {
        private readonly IMessageReceiver _client;
        private readonly ILogger<ImageReviseService> _logger;
        private readonly IOpenAIImageService _imageService;
        private readonly IUserCommandLimitService _limitService;
        private IDisposable? _subscription;

        public ImageReviseService(IMessageReceiver client, ILogger<ImageReviseService> logger, IOpenAIImageService imageService, IUserCommandLimitService limitService)
        {
            _client = client;
            _logger = logger;
            _imageService = imageService;
            _limitService = limitService;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _subscription = _client.OnReceivedSubscribe(
                OnMessageReceivedAsync,
                MessageConditions.NotFromBot.And(MessageConditions.NotDeleted),
                nameof(ImageReviseService));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _subscription?.Dispose();
            return Task.CompletedTask;
        }

        private Task RunProgressAsync(IMessageData? progressMessage, string original, Stopwatch stopwatch, CancellationToken token)
        {
            if (progressMessage is null)
            {
                return Task.CompletedTask;
            }

            return Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    StringBuilder builder = new(original);
                    builder.AppendLine($"{stopwatch.Elapsed.Seconds}秒経過中...");
                    await progressMessage.ModifyMessageAsync(msg => builder.ToString());
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
            return GetMessageWithPrompt("画像を修正してるよ！", prompt);
        }

        private static string GetFailedMessage(string prompt, int second)
        {
            return GetMessageWithPrompt($"画像の修正に失敗しちゃった。経過時間={second}秒", prompt);
        }

        private static string GetSucceedMessage(string prompt, int second)
        {
            return GetMessageWithPrompt($"画像の修正が完了したよ！経過時間={second}秒", prompt);
        }

        private static string GetMessageWithPrompt(string message, string prompt)
        {
            StringBuilder progressMessageBuilder = new(message);
            progressMessageBuilder.AppendLine($"\"{prompt}\"");
            return progressMessageBuilder.ToString();
        }

        private async Task OnMessageReceivedAsync(IMessageData message, CancellationToken token)
        {
            if (!message.IsReply)
                return;

            if (!message.Content.StartsWith("!revise "))
                return;

            var referenced = message.ReplySource;
            if (referenced is null)
                return;

            await referenced.CreateAttachmentSourceIfNotCachedAsync();

            var attachment = referenced.Attachments.FirstOrDefault(a => a.ContentType?.StartsWith("image/") == true);
            if (attachment is null)
                return;

            if (!_limitService.TryAdd(message.AuthorId, "image"))
            {
                await message.ReplyMessageAsync(ErrorMessages.CommandLimitExceeded);
                return;
            }

            var prompt = message.Content.Substring("!revise ".Length);

            Stopwatch stopWatch = Stopwatch.StartNew();
            var progressContent = GetProgressMessage(prompt);
            var progressMessage = await message.ReplyMessageAsync(progressContent);

            if (progressMessage is null)
            {
                return;
            }

            using var cts = new CancellationTokenSource();
            var progressTask = RunProgressAsync(progressMessage, progressContent, stopWatch, cts.Token);

            bool progressStopped = false;
            async Task StopProgressAsync()
            {
                if (progressStopped)
                {
                    return;
                }

                progressStopped = true;

                await cts.CancelAsync();
                await progressTask;
            }

            try
            {
                await using var stream = new MemoryStream(attachment.Bytes);

                var results = await _imageService.EditAsync(stream, prompt, 1024, cts.Token);
                if (results.Count == 0)
                {
                    await StopProgressAsync();
                    await progressMessage.ModifyMessageAsync(msg => GetFailedMessage(prompt, stopWatch.Elapsed.Seconds));
                    return;
                }

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
                    imageBytes = await HttpClientStatic.Default.GetByteArrayAsync(result.Uri, cts.Token);
                }
                else if (result.HasBytes)
                {
                    imageBytes = result.Bytes!.Value.ToArray();
                }
                else
                {
                    await StopProgressAsync();
                    await progressMessage.ModifyMessageAsync(msg => GetFailedMessage(prompt, stopWatch.Elapsed.Seconds));
                    return;
                }

                var filePath = Path.Combine(dir, $"revise_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.png");
                await File.WriteAllBytesAsync(filePath, imageBytes, cts.Token);

                await StopProgressAsync();
                await progressMessage.ModifyMessageAsync(msg => GetSucceedMessage(prompt, stopWatch.Elapsed.Seconds));

                await message.ReplyMessageAsync($"画像を修正したよ！\n\"{prompt}\"", filePath);

                await progressMessage.DeleteAsync();
            }
            finally
            {
                await StopProgressAsync();
            }
        }
    }
}

