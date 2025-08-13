using Discord.Interactions;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Data;
using TsDiscordBot.Core.HostedService;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.Commands;

public class R18CommandModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger<R18CommandModule> _logger;
    private readonly DatabaseService _databaseService;

    public R18CommandModule(ILogger<R18CommandModule> logger, DatabaseService databaseService)
    {
        _logger = logger;
        _databaseService = databaseService;
    }

    [SlashCommand("add-r18-word", "R18に該当するワードを登録します。")]
    public async Task AddR18Word(string word)
    {
        try
        {
            var guildId = Context.Guild.Id;

            _databaseService.Insert(R18TriggerWord.TableName, new R18TriggerWord
            {
                GuildId = guildId,
                Word = word
            });

            await RespondAsync($"🔞 R18ワードを登録しました: `{word}`");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add R18 word.");
            await RespondAsync("⚠️ R18ワードの登録に失敗しました。");
        }
    }

    [SlashCommand("remove-r18-word", "登録されているR18ワードを削除します。")]
    public async Task RemoveR18Word(string word)
    {
        try
        {
            var guildId = Context.Guild.Id;

            var matched = _databaseService.FindAll<R18TriggerWord>(R18TriggerWord.TableName)
                .Where(x => x.GuildId == guildId)
                .Where(x => x.Word.Equals(word, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (var item in matched)
            {
                _databaseService.Delete(R18TriggerWord.TableName, item.Id);
            }

            await RespondAsync($"🗑️ R18ワードを削除しました: `{word}`");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove R18 word.");
            await RespondAsync("⚠️ R18ワードの削除に失敗しました。");
        }
    }
}