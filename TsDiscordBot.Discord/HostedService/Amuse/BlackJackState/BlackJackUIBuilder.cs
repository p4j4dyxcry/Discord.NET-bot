using System.Text;
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
        private readonly BlackJackGame _game;
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

        private string BuildPlayerCardsEmotes()
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

        private string BuildDealerCardsEmotes()
        {
            StringBuilder cards = new StringBuilder();

            if (_game.IsFinished)
            {
                cards.Append(_emoteDatabase.GetEmote(_game.PlayerCards[0]));
                for (int i = 1; i < _game.DealerCards.Count; i++)
                {
                    var card = _game.DealerCards[i];
                    cards.Append(_emoteDatabase.GetFlipAnimationEmote(card));
                }
            }
            else
            {
                string dealerCard = _game.PlayerCards.Count is 2 ?
                    _emoteDatabase.GetFlipAnimationEmote(_game.DealerVisibleCard) :
                    _emoteDatabase.GetEmote(_game.DealerVisibleCard);

                string backGround = _emoteDatabase.GetBackgroundCardEmote();

                cards.Append(dealerCard);
                cards.Append(backGround);
            }

            return cards.ToString();
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

            return $"あなた[{playerScore}]";
        }

        private IEnumerable<MessageComponent> BuildButtons()
        {
            if (_enableHitButton)
                yield return Button(BlackJackActions.Hit, "ヒット", ButtonStyle.Primary);
            if (_enableStandButton)
                yield return Button(BlackJackActions.Stand, "スタンド", ButtonStyle.Secondary);
            if (_enableDoubleDownButton)
                yield return Button(BlackJackActions.DoubleDown, "ダブルダウン", ButtonStyle.Danger);
            if (_enableRetryButton)
                yield return Button(BlackJackActions.Replay, "もう1回", ButtonStyle.Primary);
            if (_enableQuitButton)
                yield return Button(BlackJackActions.Quit, "やめる", ButtonStyle.Secondary);

            MessageComponent Button(string action, string label, ButtonStyle style)
                => new()
                {
                    ActionId = GameMessageUtil.MakeActionId(action, _messageId),
                    Content = label,
                    Kind = ComponentKind.Button,
                    ButtonStyle = style,
                };
        }

        private MessageColor BuildColor()
        {
            if (_game.Result?.Outcome == GameOutcome.DealerWin)
            {
                // 朱色
                return MessageColor.FromRgb(219, 79, 46);
            }
            else if (_game.Result?.Outcome == GameOutcome.PlayerWin)
            {
                // 黄緑
                return MessageColor.FromRgb(184, 210, 0);
            }
            else if (_game.Result?.Outcome == GameOutcome.Push)
            {
                // オイスターホワイト
                return MessageColor.FromRgb(248, 245, 227);
            }

            // 黄色
            return MessageColor.FromRgb(255,217,0);
        }

        public GameUi Build()
        {
            var result = new GameUi();

            var emote = _emoteDatabase.FindEmoteByName("BG","");

            result.MessageEmbed = new MessageEmbed[1];
            result.MessageEmbed[0] = new MessageEmbed
            {
                Author = _title ?? $"BlackJack: Bet[{_game.Bet}]" ,
                AuthorAvatarUrl = emote?.Url,
                Color = BuildColor(),
                Fields =
                [
                    new EmbedField
                    {
                        Name = BuildDealerScoreString(),
                        Value = BuildDealerCardsEmotes(),
                    },
                    new EmbedField
                    {
                        Name = BuildPlayerScoreString(),
                        Value = BuildPlayerCardsEmotes(),
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