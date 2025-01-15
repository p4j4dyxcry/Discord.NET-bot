using Discord.Interactions;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Data;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.Commands;

public class ReactionCommandModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger _logger;
    private readonly DatabaseService _databaseService;

    public ReactionCommandModule(ILogger<ReactionCommandModule> logger, DatabaseService databaseService)
    {
        _logger = logger;
        _databaseService = databaseService;
    }

    [SlashCommand("add-trigger-reaction", "Register a reaction for a specific word")]
    public async Task AddReaction(string triggerWord, string reaction)
    {
        try
        {
            var guildId = Context.Guild.Id;

            _databaseService.Insert(TriggerReactionPost.TableName,
                new TriggerReactionPost
            {
                GuildId = guildId,
                TriggerWord = triggerWord,
                Reaction = reaction
            });

            await RespondAsync($"Reaction registered! Word: `{triggerWord}`, Reaction: {reaction}");
        }
        catch(Exception e)
        {
            _logger.LogError(e,"Failed to add trigger reaction.");
        }
    }

    [SlashCommand("remove-trigger-reaction", "Remove a reaction for a specific word")]
    public async Task RemoveReaction(string triggerWord)
    {
        try
        {
            var guildId = Context.Guild.Id;

            // TODO optimize query to improve performance
            TriggerReactionPost[] deleteList = _databaseService.FindAll<TriggerReactionPost>(TriggerReactionPost.TableName)
                .Where(x => x.GuildId == guildId)
                .Where(x => x.TriggerWord == triggerWord)
                .ToArray();

            foreach (TriggerReactionPost item in deleteList)
            {
                _databaseService.Delete(TriggerReactionPost.TableName,item.Id);
            }

            await RespondAsync($"Deleted registered! Word: `{triggerWord}`");
        }
        catch(Exception e)
        {
            _logger.LogError(e,"Failed to add trigger reaction.");
        }
    }

}