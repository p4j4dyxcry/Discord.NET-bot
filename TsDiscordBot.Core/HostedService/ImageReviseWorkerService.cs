using System;
using System.Diagnostics;
using System.Text;
using System.Net.Http;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Constants;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.HostedService
{
    public class ImageReviseWorkerService : BackgroundService
    {
        private readonly ILogger<ImageReviseWorkerService> _logger;
        private readonly IOpenAIImageService _imageService;
        private readonly IUserCommandLimitService _limitService;
        private readonly SemaphoreSlim _semaphore = new(2);

        public ImageReviseWorkerService(ILogger<ImageReviseWorkerService> logger, IOpenAIImageService imageService, IUserCommandLimitService limitService)
        {
            _logger = logger;
            _imageService = imageService;
            _limitService = limitService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var reader = ImageReviseService.Queue.Reader;
            await foreach (var message in reader.ReadAllAsync(stoppingToken))
            {
                _ = ProcessMessageAsync(message, stoppingToken);
            }
        }

        private async Task ProcessMessageAsync(SocketMessage message, CancellationToken stoppingToken)
        {
            await _semaphore.WaitAsync(stoppingToken);
            try
            {
                if (message.Author.IsBot)
                    return;

                if (message.Reference?.MessageId.IsSpecified != true)
                    return;

                if (!message.Content.StartsWith("!revise "))
                    return;

                var referenced = await message.Channel.GetMessageAsync(message.Reference.MessageId.Value);
                if (referenced is null)
                    return;

                var attachment = referenced.Attachments.FirstOrDefault(a => a.ContentType?.StartsWith("image/") == true);
                if (attachment is null)
                    return;

                if (!_limitService.TryAdd(message.Author.Id, "image"))
                {
                    await message.Channel.SendMessageAsync(ErrorMessages.CommandLimitExceeded, messageReference: new MessageReference(message.Id));
                    return;
                }

                var prompt = message.Content.Substring("!revise ".Length);

                Stopwatch stopWatch = Stopwatch.StartNew();
                var progressContent = GetProgressMessage(prompt);
                var progressMessage = await message.Channel.SendMessageAsync(progressContent, messageReference: new MessageReference(message.Id));
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                var progressTask = RunProgressAsync(progressMessage, progressContent, stopWatch, cts.Token);

                try
                {
                    using var http = new HttpClient();
                    await using var stream = await http.GetStreamAsync(attachment.Url);

                    var results = await _imageService.EditAsync(stream, prompt, 1024, cts.Token);
                    if (results.Count == 0)
                    {
                        cts.Cancel();
                        await progressTask;
                        await progressMessage.ModifyAsync(msg => msg.Content = GetFailedMessage(prompt, stopWatch.Elapsed.Seconds));
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
                        imageBytes = await http.GetByteArrayAsync(result.Uri, cts.Token);
                    }
                    else if (result.HasBytes)
                    {
                        imageBytes = result.Bytes!.Value.ToArray();
                    }
                    else
                    {
                        cts.Cancel();
                        await progressTask;
                        await progressMessage.ModifyAsync(msg => msg.Content = GetFailedMessage(prompt, stopWatch.Elapsed.Seconds));
                        return;
                    }

                    var filePath = Path.Combine(dir, $"revise_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.png");
                    await File.WriteAllBytesAsync(filePath, imageBytes, cts.Token);

                    cts.Cancel();
                    await progressTask;
                    await progressMessage.ModifyAsync(msg => msg.Content = GetSucceedMessage(prompt, stopWatch.Elapsed.Seconds));

                    await message.Channel.SendFileAsync(filePath, text: $"画像を修正したよ！\n\"{prompt}\"", messageReference: new MessageReference(message.Id));
                }
                catch (Exception ex)
                {
                    cts.Cancel();
                    await progressTask;
                    _logger.LogError(ex, "Failed to revise image");
                    await progressMessage.ModifyAsync(msg => msg.Content = GetFailedMessage(prompt, stopWatch.Elapsed.Seconds));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process image revise request");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private static Task RunProgressAsync(IUserMessage progressMessage, string original, Stopwatch stopwatch, CancellationToken token)
        {
            return Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    StringBuilder builder = new(original);
                    builder.AppendLine($"{stopwatch.Elapsed.Seconds}秒経過中...");
                    await progressMessage.ModifyAsync(msg => msg.Content = builder.ToString());
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
    }
}
