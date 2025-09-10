using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;

namespace TsDiscordBot.Core.Services;

public class RandTopicService
{
    private readonly Dictionary<string, string> _topics;

    public RandTopicService()
    {
        _topics = new Dictionary<string, string>();
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("rand_topics.json"));
        if (resourceName is null)
        {
            return;
        }
        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return;
        }
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        var list = JsonSerializer.Deserialize<List<Topic>>(json);
        if (list is null)
        {
            return;
        }
        foreach (var t in list)
        {
            _topics[t.Date] = t.Text;
        }
    }

    public string? GetTopic(DateTime dateJst)
    {
        var key = $"{dateJst.Month}/{dateJst.Day}";
        return _topics.TryGetValue(key, out var text) ? text : null;
    }

    private class Topic
    {
        public string Date { get; set; } = "";
        public string Text { get; set; } = "";
    }
}
