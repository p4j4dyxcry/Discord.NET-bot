using System.ClientModel;
using System.Text.Json;

namespace TsDiscordBot.Core.Utility;

public static class OpenAIErrorHelper
{
    public static string? TryGetErrorCode(ClientResultException? ex)
    {
        if (ex is null)
        {
            return null;
        }

        try
        {
            var response = ex.GetRawResponse();
            if (response?.ContentStream is Stream stream)
            {
                using var reader = new StreamReader(stream);
                var body = reader.ReadToEnd();
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("error", out var error) &&
                    error.TryGetProperty("code", out var code))
                {
                    return code.GetString();
                }
            }
        }
        catch
        {
            // Ignore failures when parsing error response.
        }

        return null;
    }
}
