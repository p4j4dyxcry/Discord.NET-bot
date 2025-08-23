using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using OpenAI.Images;

namespace TsDiscordBot.Core.Services;

public sealed class OpenAIImageOptions
{
    [Required] public string ApiKey { get; init; } = "";
    public string? Model { get; init; }
}

public sealed record GeneratedImageResult(
    Uri? Uri,
    ReadOnlyMemory<byte>? Bytes
)
{
    public bool HasUri => Uri is not null;
    public bool HasBytes => Bytes is not null && !Bytes.Value.IsEmpty;

    public string AsString() =>
        Uri?.ToString()
        ?? (HasBytes ? $"data:image/png;base64,{Convert.ToBase64String(Bytes!.Value.ToArray())}" : string.Empty);
}

public interface IOpenAIImageService
{
    Task<IReadOnlyList<GeneratedImageResult>> GenerateAsync(
        string prompt,
        int count = 1,
        int size = 256, // Obsolete
        CancellationToken ct = default);

    Task<IReadOnlyList<GeneratedImageResult>> EditAsync(
        Stream image,
        string prompt,
        int size = 1024,
        CancellationToken ct = default);
}

public sealed class OpenAIImageService : IOpenAIImageService
{
    private readonly ImageClient _client;

    // DI で ImageClient を渡す形を推奨。Options から作るオーバーロードも用意。
    public OpenAIImageService(ImageClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public static OpenAIImageService Create(OpenAIImageOptions opts)
    {
        string defaultModel = "gpt-image-1";

        if (opts is null) throw new ArgumentNullException(nameof(opts));
        if (string.IsNullOrWhiteSpace(opts.ApiKey))
            throw new InvalidOperationException("OpenAI API key is not configured.");

        var model = string.IsNullOrWhiteSpace(opts.Model) ? defaultModel : opts.Model;
        var client = new ImageClient(model, opts.ApiKey);
        return new OpenAIImageService(client);
    }

    public async Task<IReadOnlyList<GeneratedImageResult>> GenerateAsync(
        string prompt,
        int count = 1,
        int size = 1024,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt must not be empty.", nameof(prompt));
        // 実運用での上限は適宜調整（レート制限・コスト管理）
        count = Math.Clamp(count, 1, 3);

        try
        {
            var response = await _client.GenerateImagesAsync(prompt, count,
                new ImageGenerationOptions
                {
                    // "Standard" is no longer supported; using low quality.
#pragma warning disable OPENAI001
                    Quality = GeneratedImageQuality.Low,
#pragma warning restore OPENAI001
                },
                cancellationToken:ct).ConfigureAwait(false);

            if (response.Value.Count == 0)
                return Array.Empty<GeneratedImageResult>();

            var list = new List<GeneratedImageResult>(response.Value.Count);
            foreach (var img in response.Value)
            {
                if (img.ImageUri is not null)
                {
                    list.Add(new GeneratedImageResult(img.ImageUri, null));
                }
                else if (img.ImageBytes is not null)
                {
                    list.Add(new GeneratedImageResult(null, img.ImageBytes));
                }
            }
            return new ReadOnlyCollection<GeneratedImageResult>(list);
        }
        catch (OperationCanceledException)
        {
            // 上位でキャンセルとわかるようそのまま投げる
            throw;
        }
        catch (Exception ex)
        {
            // ここで握りつぶさず投げる：呼び出し側でログ/リトライ/ユーザー文言に変換
            throw new ImageGenerationException("Failed to generate image.", ex);
        }
    }

    public async Task<IReadOnlyList<GeneratedImageResult>> EditAsync(
        Stream image,
        string prompt,
        int size = 1024,
        CancellationToken ct = default)
    {
        if (image is null) throw new ArgumentNullException(nameof(image));
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt must not be empty.", nameof(prompt));

        try
        {
            var opts = new ImageEditOptions
            {
                Size = new GeneratedImageSize(size, size)
            };

            var response = await _client.GenerateImageEditsAsync(image, "image.png", prompt, 1, opts, ct).ConfigureAwait(false);

            if (response.Value.Count == 0)
                return Array.Empty<GeneratedImageResult>();

            var list = new List<GeneratedImageResult>(response.Value.Count);
            foreach (var img in response.Value)
            {
                if (img.ImageUri is not null)
                {
                    list.Add(new GeneratedImageResult(img.ImageUri, null));
                }
                else if (img.ImageBytes is not null)
                {
                    list.Add(new GeneratedImageResult(null, img.ImageBytes));
                }
            }

            return new ReadOnlyCollection<GeneratedImageResult>(list);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ImageGenerationException("Failed to edit image.", ex);
        }
    }
}

public sealed class ImageGenerationException : Exception
{
    public ImageGenerationException(string message, Exception inner) : base(message, inner) { }
}