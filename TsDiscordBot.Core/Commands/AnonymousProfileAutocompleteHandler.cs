using Discord;
using Discord.Interactions;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.Commands;

public class AnonymousProfileAutocompleteHandler : AutocompleteHandler
{
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction interaction, IParameterInfo parameter, IServiceProvider services)
    {
        var value = interaction.Data.Current.Value as string ?? string.Empty;
        var results = AnonymousProfileProvider.GetProfiles()
            .Where(p => p.Name.Contains(value))
            .Take(25)
            .Select(p => new AutocompleteResult(p.Name, p.Name));

        return Task.FromResult(AutocompletionResult.FromSuccess(results));
    }
}
