using System.Text;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Discord.Data;
using TsDiscordBot.Discord.Services;

namespace TsDiscordBot.Discord.Commands;

public class MemoryCommandModule: InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger _logger;
    private readonly DatabaseService _databaseService;
    private readonly OpenAIService _openAiService;

    public MemoryCommandModule(ILogger<MemoryCommandModule> logger, DatabaseService databaseService, OpenAIService openAiService)
    {
        _logger = logger;
        _databaseService = databaseService;
        _openAiService = openAiService;
    }

    [SlashCommand("add-memory", "つむぎちゃんに長期的に物事を覚えこませます。")]
    public async Task AddLongTermMemory(string content)
    {
        var guildId = Context.Guild.Id;
        var author = Context.User.Username;

        if (Context.User is SocketGuildUser guildUser)
        {
            author = guildUser.DisplayName;
        }

        var memory = new LongTermMemory
        {
            GuildId = guildId,
            Author = author,
            Content = content,
        };

        _databaseService.Insert(LongTermMemory.TableName,memory);

        await RespondAsync($"ID = 「{memory.Id}」:「{content}」を覚えたよ！");
    }

    [SlashCommand("remove-memory", "つむぎちゃんの記憶を消去します。")]
    public async Task RemoveLongTermMemory(int id)
    {
        var memory = _databaseService.FindAll<LongTermMemory>(LongTermMemory.TableName)
            .FirstOrDefault(x => x.Id == id);

        if (memory is not null)
        {
            _databaseService.Delete(LongTermMemory.TableName, memory.Id);
            await RespondAsync($"「{memory.Content}」を忘れたよ！");
        }
        else
        {
            await RespondAsync($"無効なId「{id}」が入力されました。");
        }

    }

    [SlashCommand("show-memories", "つむぎちゃんが覚えていること一覧を表示させる")]
    public async Task ShowMemories()
    {
        var guildId = Context.Guild.Id;
        LongTermMemory[] memories =
            _databaseService.FindAll<LongTermMemory>(LongTermMemory.TableName)
                .Where(x => x.GuildId == guildId)
                .ToArray();

        StringBuilder builder = new();

        foreach (var memory in memories)
        {
            builder.AppendLine($"ID = {memory.Id}: {memory.Content} by {memory.Author}");
        }

        if (builder.Length == 0)
        {
            await RespondAsync("何も覚えていないよ！");
        }
        else
        {
            await RespondAsync(builder.ToString());
        }
    }
}
