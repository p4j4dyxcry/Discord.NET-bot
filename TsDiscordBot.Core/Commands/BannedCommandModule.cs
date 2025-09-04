using Discord.Interactions;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Constants;
using TsDiscordBot.Core.Data;
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
            await RespondAsync(ErrorMessages.BannedWordAddFailed);
        }
    }

    [SlashCommand("add-banned-words", "カンマまたは改行区切りで禁止ワードを登録します。")]
    public async Task AddBannedWords(string words)
    {
        try
        {
            var guildId = Context.Guild.Id;

            var wordList = words
                .Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToArray();

            foreach (var w in wordList)
            {
                _databaseService.Insert(BannedTriggerWord.TableName, new BannedTriggerWord
                {
                    GuildId = guildId,
                    Word = w
                });
            }

            var joined = string.Join(", ", wordList.Select(x => $"`{x}`"));
            await RespondAsync($"🚫 禁止ワードを登録しました: {joined}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add banned words.");
            await RespondAsync(ErrorMessages.BannedWordAddFailed);
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
            await RespondAsync(ErrorMessages.BannedWordRemoveFailed);
        }
    }

    [SlashCommand("set-banned-text-mode", "禁止テキストの処理モードを設定します。(hide/delete)")]
    public async Task SetBannedTextMode(string mode)
    {
        try
        {
            var guildId = Context.Guild.Id;

            var normalized = mode.Equals("delete", StringComparison.OrdinalIgnoreCase)
                ? BannedTextMode.Delete
                : BannedTextMode.Hide;

            var setting = _databaseService.FindAll<BannedTextSetting>(BannedTextSetting.TableName)
                .FirstOrDefault(x => x.GuildId == guildId);

            if (setting is null)
            {
                _databaseService.Insert(BannedTextSetting.TableName, new BannedTextSetting
                {
                    GuildId = guildId,
                    Mode = normalized
                });
            }
            else
            {
                setting.Mode = normalized;
                _databaseService.Update(BannedTextSetting.TableName, setting);
            }

            await RespondAsync($"禁止テキストモードを `{normalized.ToString().ToLower()}` に設定しました。");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set banned text mode.");
            await RespondAsync("⚠️ 禁止テキストモードの設定に失敗しました。");
        }
    }
}