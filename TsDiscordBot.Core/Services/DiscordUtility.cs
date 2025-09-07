using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace TsDiscordBot.Core.Services
{
    public class DiscordUtility
    {
        public static async Task<IReadOnlyList<(string FileName, string ContentType, byte[] Data)>> CorrectAttachmentsAsync(SocketMessage message, ILogger? logger)
        {
            List<(string FileName, string ContentType, byte[] Data)> attachments = new();
            if (message.Attachments.Any())
            {
                attachments = new List<(string, string, byte[])>();
                using var http = new HttpClient();
                foreach (var a in message.Attachments)
                {
                    try
                    {
                        var data = await http.GetByteArrayAsync(a.Url);
                        attachments.Add((a.Filename, a.ContentType, data));
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Failed to download attachment {Url}", a.Url);
                    }
                }
            }

            return attachments;
        }
    }
}