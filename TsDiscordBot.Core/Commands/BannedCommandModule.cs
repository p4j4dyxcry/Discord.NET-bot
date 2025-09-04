using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using System.Text;
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

    [SlashCommand("remove-banned-words", "カンマまたは改行区切りで禁止ワードを削除します。")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task RemoveBannedWords(string words)
    {
        try
        {
            var guildId = Context.Guild.Id;

            var wordList = words
                .Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var removed = new List<string>();
            foreach (var w in wordList)
            {
                var matched = _databaseService.FindAll<BannedTriggerWord>(BannedTriggerWord.TableName)
                    .Where(x => x.GuildId == guildId)
                    .Where(x => x.Word.Equals(w, StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                if (matched.Length > 0)
                {
                    foreach (var item in matched)
                    {
                        _databaseService.Delete(BannedTriggerWord.TableName, item.Id);
                    }
                    removed.Add(w);
                }
            }

            if (removed.Count == 0)
            {
                await RespondAsync("指定された禁止ワードは見つかりませんでした。");
            }
            else
            {
                var joined = string.Join(", ", removed.Select(x => $"`{x}`"));
                await RespondAsync($"🗑️ 禁止ワードを削除しました: {joined}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove banned words.");
            await RespondAsync(ErrorMessages.BannedWordRemoveFailed);
        }
    }

    [SlashCommand("export-banned-words", "登録されている禁止ワードをCSV形式で出力します。")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task ExportBannedWords()
    {
        try
        {
            var guildId = Context.Guild.Id;

            var words = _databaseService.FindAll<BannedTriggerWord>(BannedTriggerWord.TableName)
                .Where(x => x.GuildId == guildId)
                .Select(x => x.Word)
                .OrderBy(x => x)
                .ToArray();

            if (words.Length == 0)
            {
                await RespondAsync("禁止ワードは登録されていません。");
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Word");
            foreach (var w in words)
            {
                sb.AppendLine(w);
            }

            await RespondWithFileAsync(new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString())), "banned_words.csv", "📄 禁止ワード一覧です。");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export banned words.");
            await RespondAsync("⚠️ 禁止ワードの出力に失敗しました。");
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

    [SlashCommand("set-banned-text-enabled", "禁止テキスト機能を有効/無効にします。")]
    public async Task SetBannedTextEnabled(bool enabled)
    {
        try
        {
            var guildId = Context.Guild.Id;

            var setting = _databaseService.FindAll<BannedTextSetting>(BannedTextSetting.TableName)
                .FirstOrDefault(x => x.GuildId == guildId);

            if (setting is null)
            {
                _databaseService.Insert(BannedTextSetting.TableName, new BannedTextSetting
                {
                    GuildId = guildId,
                    IsEnabled = enabled
                });
            }
            else
            {
                setting.IsEnabled = enabled;
                _databaseService.Update(BannedTextSetting.TableName, setting);
            }

            await RespondAsync(enabled
                ? "禁止テキスト機能を有効にしました。"
                : "禁止テキスト機能を無効にしました。");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set banned text enabled.");
            await RespondAsync("⚠️ 禁止テキスト機能の設定に失敗しました。");
        }
    }
}