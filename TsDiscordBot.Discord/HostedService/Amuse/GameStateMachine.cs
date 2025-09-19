using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using TsDiscordBot.Core.Database;
using TsDiscordBot.Core.Game;
using TsDiscordBot.Discord.Amuse;
using TsDiscordBot.Discord.HostedService.Amuse.BlackJackState;
using TsDiscordBot.Discord.Services;
using TsDiscordBot.Discord.Utility;

namespace TsDiscordBot.Discord.HostedService.Amuse
{

    public enum GameProgress
    {
        Invalid,
        InProgress,
        Exit,
    }

    public class GameStateMachine
    {
        public AmusePlay AmusePlay { get; }
        public IGameState CurrentGameState { get; private set; }
        public GameStateMachine(IGameState initialState, AmusePlay amusePlay)
        {
            AmusePlay = amusePlay;
            CurrentGameState = initialState;
        }
        public async Task OnGameEnter(IUserMessage message)
        {
            await CurrentGameState.OnEnterAsync();

            var gui = await CurrentGameState.GetGameUiAsync();
            var discordUi = gui.ToDiscord();

            await message.ModifyAsync(x =>
            {
                x.Content = discordUi.Content;
                x.Components = discordUi.Components;
                x.Embeds = discordUi.Embeds;
            });
        }

        public async Task<(ulong Id, GameProgress Result)> OnUpdateMessageAsync(SocketMessageComponent component)
        {
            var parts = component.Data.CustomId.Split(':');
            if (parts.Length != 2)
            {
                return (AmusePlay.MessageId,GameProgress.Invalid);
            }

            if (!ulong.TryParse(parts[1], out var messageId))
            {
                return (AmusePlay.MessageId,GameProgress.Invalid);
            }

            if (AmusePlay.MessageId != messageId)
            {
                return(AmusePlay.MessageId,GameProgress.Invalid);
            }

            var action = parts[0];

            await component.DeferAsync();

            CurrentGameState = await CurrentGameState.GetNextStateAsync(action);
            await CurrentGameState.OnEnterAsync();

            var gui = await CurrentGameState.GetGameUiAsync();
            var discordUi = gui.ToDiscord();

            await component.Message.ModifyAsync(x =>
            {
                x.Content = discordUi.Content;
                x.Components = discordUi.Components;
                x.Embeds = discordUi.Embeds;
            });

            if (CurrentGameState is QuitGameState)
            {
                return (AmusePlay.MessageId,GameProgress.Exit);
            }

            return(AmusePlay.MessageId,GameProgress.InProgress);
        }
    }
}