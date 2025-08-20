using System.Text;
using Microsoft.Extensions.Configuration;
using OpenAI.Images;

namespace TsDiscordBot.Core.Services;

public class OpenAIImageService
{
    private readonly ImageClient _client;

    public OpenAIImageService(IConfiguration config)
    {
        var apiKey = Envs.OPENAI_API_KEY;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = config["open_ai_api_key"];
        }
        _client = new ImageClient("https://api.openai.com/v1", apiKey);
    }

    public async Task<string> GenerateImageAsync(string prompt, int n, int size)
    {
        try
        {
            var options = new ImageGenerationOptions
            {
                Size = new GeneratedImageSize(size, size)
            };

            var result = await _client.GenerateImagesAsync(prompt, n, options);
            if (result.Value.Count == 0)
            {
                return "No image generated.";
            }

            StringBuilder sb = new();
            for (int i = 0; i < result.Value.Count; i++)
            {
                var img = result.Value[i];
                if (img.ImageUri is not null)
                {
                    sb.AppendLine(img.ImageUri.ToString());
                }
                else if (img.ImageBytes is not null)
                {
                    sb.AppendLine(Convert.ToBase64String(img.ImageBytes.ToArray()));
                }
            }
            return sb.ToString().Trim();
        }
        catch (Exception e)
        {
            return $"Failed to generate image: {e.Message}";
        }
    }
}
