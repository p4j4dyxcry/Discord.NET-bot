using System.Text;
using Discord;
using TsDiscordBot.Core;
using TsDiscordBot.Core.Game;
using TsDiscordBot.Core.Game.BlackJack;
using TsDiscordBot.Core.Messaging;
using TsDiscordBot.Discord.Services;
using ButtonStyle = TsDiscordBot.Core.Messaging.ButtonStyle;
using EmbedField = TsDiscordBot.Core.Messaging.EmbedField;
using MessageComponent = TsDiscordBot.Core.Messaging.MessageComponent;

namespace TsDiscordBot.Discord.HostedService.Amuse.BlackJackState
{
    public class BlackJackUIBuilder
    {
        public BlackJackGame _game;
        private readonly EmoteDatabase _emoteDatabase;
        private readonly ulong _messageId;
        private string? _footer;
        private string? _title;

        private bool _enableHitButton;
        private bool _enableStandButton;
        private bool _enableDoubleDownButton;
        private bool _enableRetryButton;
        private bool _enableQuitButton;

        public BlackJackUIBuilder WithFooter(string footer)
        {
            _footer = footer;
            return this;
        }

        public BlackJackUIBuilder WithTitle(string title)
        {
            _title = title;
            return this;
        }

        public BlackJackUIBuilder EnableHitButton()
        {
            _enableHitButton = true;
            return this;
        }

        public BlackJackUIBuilder EnableStandButton()
        {
            _enableStandButton = true;
            return this;
        }

        public BlackJackUIBuilder EnableDoubleDownButton()
        {
            _enableDoubleDownButton = true;
            return this;
        }

        public BlackJackUIBuilder EnableRetryButton()
        {
            _enableRetryButton = true;
            return this;
        }

        public BlackJackUIBuilder EnableQuitButton()
        {
            _enableQuitButton = true;
            return this;
        }

        public BlackJackUIBuilder(BlackJackGame game, EmoteDatabase emoteDatabase, ulong messageId)
        {
            _game = game;
            _emoteDatabase = emoteDatabase;
            _enableDoubleDownButton = false;
            _messageId = messageId;
        }

        private string BuildPlayerCardsString()
        {
            StringBuilder playerCards = new StringBuilder();

            if (_game.PlayerCards.Count is 2)
            {
                playerCards.Append(_emoteDatabase.GetFlipAnimationEmote(_game.PlayerCards[0]));
                playerCards.Append(_emoteDatabase.GetFlipAnimationEmote(_game.PlayerCards[1]));
            }
            else
            {
                for (int i=0; i<_game.PlayerCards.Count - 1; i++)
                {
                    var card = _game.PlayerCards[i];
                    playerCards.Append(_emoteDatabase.GetEmote(card));
                }
                playerCards.Append(_emoteDatabase.GetFlipAnimationEmote(_game.PlayerCards[^1]));
            }

            return playerCards.ToString();
        }

        private string BuildDealerCardsString()
        {
            if (_game.IsFinished)
            {
                int dealerScore = BlackJackGame.CalculateScore(_game.DealerCards);
                return $"つむぎ[{dealerScore}]";
            }
            else
            {
                string dealerCard = _emoteDatabase.GetFlipAnimationEmote(_game.DealerVisibleCard);
                string backGround = _emoteDatabase.GetBackgroundCardEmote();

                return $"{dealerCard}{backGround}";
            }
        }

        private string BuildDealerScoreString()
        {
            if (_game.IsFinished)
            {
                int dealerScore = BlackJackGame.CalculateScore(_game.DealerCards);
                return $"つむぎ[{dealerScore}]";
            }
            else
            {
                int dealerScore = BlackJackGame.CalculateScore([_game.DealerVisibleCard]);
                return $"つむぎ[{dealerScore} + ?]";
            }
        }

        private string BuildPlayerScoreString()
        {
            int playerScore = BlackJackGame.CalculateScore(_game.PlayerCards);

            return $"あなた[{playerScore} + ?]";
        }

        private IEnumerable<MessageComponent> BuildButtons()
        {
            if (_enableHitButton)
            {
                yield return new MessageComponent
                {
                    ActionId = StateMachineUtil.MakeActionId(BlackJackActions.Hit,_messageId),
                    Content = "ヒット",
                    Kind = ComponentKind.Button,
                    ButtonStyle = ButtonStyle.Primary,
                };
            }

            if (_enableStandButton)
            {
                yield return new MessageComponent
                {
                    ActionId = StateMachineUtil.MakeActionId(BlackJackActions.Stand,_messageId),
                    Content = "スタンド",
                    Kind = ComponentKind.Button,
                    ButtonStyle = ButtonStyle.Secondary,
                };
            }

            if (_enableDoubleDownButton)
            {
                yield return new MessageComponent
                {
                    ActionId = StateMachineUtil.MakeActionId(BlackJackActions.DoubleDown,_messageId),
                    Content = "ダブルダウン",
                    Kind = ComponentKind.Button,
                    ButtonStyle = ButtonStyle.Danger,
                };
            }

            if (_enableRetryButton)
            {
                yield return new MessageComponent
                {
                    ActionId = StateMachineUtil.MakeActionId(BlackJackActions.Replay,_messageId),
                    Content = "もう1回",
                    Kind = ComponentKind.Button,
                    ButtonStyle = ButtonStyle.Primary,
                };
            }

            if (_enableQuitButton)
            {
                yield return new MessageComponent
                {
                    ActionId = StateMachineUtil.MakeActionId(BlackJackActions.Quit,_messageId),
                    Content = "やめる",
                    Kind = ComponentKind.Button,
                    ButtonStyle = ButtonStyle.Secondary,
                };
            }
        }

        public GameUi Build()
        {
            var result = new GameUi();

            var emote = _emoteDatabase.FindEmoteByName("Flip_BG");

            result.MessageEmbed = new MessageEmbed[1];
            result.MessageEmbed[0] = new MessageEmbed
            {
                Author = _title ?? $"BlackJack: Bet[{_game.Bet}]" ,
                AuthorAvatarUrl = emote?.Url,
                Fields =
                [
                    new EmbedField
                    {
                        Name = BuildDealerScoreString(),
                        Value = BuildDealerCardsString(),
                    },
                    new EmbedField
                    {
                        Name = BuildPlayerScoreString(),
                        Value = BuildPlayerCardsString(),
                    }
                ],
                Footer = _footer ?? "ゲームが進行中です",
            };

            result.MessageComponents = BuildButtons()
                .ToArray();
            return result;
        }
    }
}