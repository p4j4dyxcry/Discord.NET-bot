using Discord.Interactions;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Data;
using TsDiscordBot.Core.HostedService;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.Commands;

public class BannedCommandModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger<BannedCommandModule> _logger;
    private readonly DatabaseService _databaseService;

    public BannedCommandModule(ILogger<BannedCommandModule> logger, DatabaseService databaseService)
    {
        _logger = logger;
        _databaseService = databaseService;
    }

    [SlashCommand("add-banned-word", "禁止に該当するワードを登録します。")]
    public async Task AddBannedWord(string word)
    {
        try
        {
            var guildId = Context.Guild.Id;

            _databaseService.Insert(BannedTriggerWord.TableName, new BannedTriggerWord
            {
                GuildId = guildId,
                Word = word
            });

            await RespondAsync($"🚫 禁止ワードを登録しました: `{word}`");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add banned word.");
            await RespondAsync("⚠️ 禁止ワードの登録に失敗しました。");
        }
    }

    [SlashCommand("remove-banned-word", "登録されている禁止ワードを削除します。")]
    public async Task RemoveBannedWord(string word)
    {
        try
        {
            var guildId = Context.Guild.Id;

            var matched = _databaseService.FindAll<BannedTriggerWord>(BannedTriggerWord.TableName)
                .Where(x => x.GuildId == guildId)
                .Where(x => x.Word.Equals(word, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (var item in matched)
            {
                _databaseService.Delete(BannedTriggerWord.TableName, item.Id);
            }

            await RespondAsync($"🗑️ 禁止ワードを削除しました: `{word}`");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove banned word.");
            await RespondAsync("⚠️ 禁止ワードの削除に失敗しました。");
        }
    }
}