using System.Text;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Services;
using Poll = TsDiscordBot.Core.Data.Poll;

namespace TsDiscordBot.Core.Commands;

public class PollCommandModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger _logger;
    private readonly DatabaseService _databaseService;

    public PollCommandModule(ILogger<PollCommandModule> logger, DatabaseService databaseService)
    {
        _logger = logger;
        _databaseService = databaseService;
    }

    [SlashCommand("poll", "質問と選択肢を指定して投票を開始します。")]
    public async Task CreatePoll(string question, string option1, string option2, string? option3 = null, string? option4 = null, string? option5 = null)
    {
        var options = new[] { option1, option2, option3, option4, option5 }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        if (options.Length < 2)
        {
            await RespondAsync("選択肢は2つ以上指定してね！", ephemeral: true);
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

        _databaseService.Insert(Poll.TableName, new Poll
        {
            ChannelId = Context.Channel.Id,
            MessageId = message.Id
        });

        await RespondAsync($"投票を開始しました。メッセージID: {message.Id}", ephemeral: true);
    }

    [SlashCommand("poll-result", "保存された投票の結果を集計します。")]
    public async Task ShowPollResult([MinValue(1)] int order = 1)
    {
        try
        {
            var polls = _databaseService.FindAll<Poll>(Poll.TableName)
                .OrderBy(p => p.Id)
                .ToList();

            if (!polls.Any())
            {
                await RespondAsync("集計する投票がありません。", ephemeral: true);
                return;
            }

            if (order < 1 || order > polls.Count)
            {
                await RespondAsync("指定した番号の投票が見つかりません。", ephemeral: true);
                return;
            }

            int index = order - 1;

            while (index < polls.Count)
            {
                var poll = polls[index];
                var channel = await Context.Client.GetChannelAsync(poll.ChannelId) as IMessageChannel;

                if (channel is null)
                {
                    _databaseService.Delete(Poll.TableName, poll.Id);
                    polls.RemoveAt(index);
                    continue;
                }

                if (await channel.GetMessageAsync(poll.MessageId) is not IUserMessage message)
                {
                    _databaseService.Delete(Poll.TableName, poll.Id);
                    polls.RemoveAt(index);
                    continue;
                }

                var emojis = new[] { "1️⃣", "2️⃣", "3️⃣", "4️⃣", "5️⃣" };
                var lines = message.Content.Split('\n');
                StringBuilder builder = new();
                builder.AppendLine($"結果発表！！: {lines.FirstOrDefault()}");

                for (int i = 1; i < lines.Length && i - 1 < emojis.Length; i++)
                {
                    var emoji = new Emoji(emojis[i - 1]);
                    var count = message.Reactions.TryGetValue(emoji, out var reaction)
                        ? reaction.ReactionCount - 1
                        : 0;
                    builder.AppendLine($"{lines[i]} : {count}");
                }

                await RespondAsync(builder.ToString());
                _databaseService.Delete(Poll.TableName, poll.Id);
                return;
            }

            await RespondAsync("集計する投票がありません。", ephemeral: true);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to show poll result.");
        }
    }
}
