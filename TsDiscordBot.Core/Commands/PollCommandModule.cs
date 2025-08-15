using System.Text;
using System.Linq;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace TsDiscordBot.Core.Commands;

public class PollCommandModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger _logger;

    public PollCommandModule(ILogger<PollCommandModule> logger)
    {
        _logger = logger;
    }

    [SlashCommand("poll", "質問と選択肢を指定して投票を開始します。")]
    public async Task CreatePoll(string question, string option1, string option2, string? option3 = null, string? option4 = null, string? option5 = null)
    {
        var options = new[] { option1, option2, option3, option4, option5 }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        if (options.Length < 2)
        {
            await RespondAsync("選択肢は2つ以上指定してください。", ephemeral: true);
            return;
        }

        var emojis = new[] { "1️⃣", "2️⃣", "3️⃣", "4️⃣", "5️⃣" };
        StringBuilder builder = new();
        builder.AppendLine(question);

        for (int i = 0; i < options.Length; i++)
        {
            builder.AppendLine($"{emojis[i]} {options[i]}");
        }

        var message = await Context.Channel.SendMessageAsync(builder.ToString());

        for (int i = 0; i < options.Length; i++)
        {
            await message.AddReactionAsync(new Emoji(emojis[i]));
        }

        await RespondAsync($"投票を開始しました。メッセージID: {message.Id}", ephemeral: true);
    }

    [SlashCommand("poll-result", "メッセージIDから投票結果を集計します。")]
    public async Task ShowPollResult(ulong messageId)
    {
        if (await Context.Channel.GetMessageAsync(messageId) is not IUserMessage message)
        {
            await RespondAsync("指定したメッセージが見つかりません。", ephemeral: true);
            return;
        }

        var emojis = new[] { "1️⃣", "2️⃣", "3️⃣", "4️⃣", "5️⃣" };
        var lines = message.Content.Split('\n');
        StringBuilder builder = new();
        builder.AppendLine($"結果: {lines.FirstOrDefault()}");

        for (int i = 1; i < lines.Length && i - 1 < emojis.Length; i++)
        {
            var emoji = new Emoji(emojis[i - 1]);
            var count = message.Reactions.TryGetValue(emoji, out var reaction)
                ? reaction.ReactionCount - 1
                : 0;
            builder.AppendLine($"{lines[i]} : {count}");
        }

        await RespondAsync(builder.ToString());
    }
}
