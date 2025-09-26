using TsDiscordBot.Core.Game;
using TsDiscordBot.Core.Messaging;
using ButtonStyle = TsDiscordBot.Core.Messaging.ButtonStyle;
using MessageComponent = TsDiscordBot.Core.Messaging.MessageComponent;

namespace TsDiscordBot.Discord.HostedService.Amuse.HighLowState
{
    public class HighLowUiBuilder
    {
        private readonly ulong _messageId;

        private bool _enableHighLowButton;
        private bool _enableDropButton;
        private bool _enableRetryButton;

        private string? _header;
        private string? _title;
        private string? _description;
        private string? _footer;

        private string? _currentCardUrl;
        private string? _nextCardUrl;

        private MessageColor? _color;

        public HighLowUiBuilder(ulong messageId)
        {
            _messageId = messageId;
        }

        public HighLowUiBuilder WithHeader(string header)
        {
            _header = header;
            return this;
        }

        public HighLowUiBuilder WithTitle(string title)
        {
            _title = title;
            return this;
        }

        public HighLowUiBuilder WithFooter(string footer)
        {
            _footer = footer;
            return this;
        }

        public HighLowUiBuilder EnableHighLowButton()
        {
            _enableHighLowButton = true;
            return this;
        }

        public HighLowUiBuilder EnableDropButton()
        {
            _enableDropButton = true;
            return this;
        }

        public HighLowUiBuilder EnableRetryButton()
        {
            _enableRetryButton = true;
            return this;
        }

        public HighLowUiBuilder WithCard(string? cardUrl)
        {
            _currentCardUrl = cardUrl;
            return this;
        }

        public HighLowUiBuilder WithNextCard(string? cardUrl)
        {
            _nextCardUrl = cardUrl;
            return this;
        }

        public HighLowUiBuilder WithColor(MessageColor? color)
        {
            _color = color;
            return this;
        }

        public HighLowUiBuilder WithDescription(string description)
        {
            _description = description;
            return this;
        }

        private IEnumerable<MessageComponent> BuildButtons()
        {
            if (_enableHighLowButton)
            {
                yield return Button(HighLowActions.High, "ハイ", ButtonStyle.Primary);
                yield return Button(HighLowActions.Low, "ロー", ButtonStyle.Secondary);
            }
            if (_enableDropButton)
            {
                yield return Button(HighLowActions.Drop, "ドロップ", ButtonStyle.Secondary);
            }

            if (_enableRetryButton)
            {
                yield return Button(HighLowActions.Replay, "もう1回", ButtonStyle.Primary);
                yield return Button(HighLowActions.Quit, "やめる", ButtonStyle.Danger);
            }

            MessageComponent Button(string action, string label, ButtonStyle style)
                => new()
                {
                    ActionId = GameMessageUtil.MakeActionId(action, _messageId),
                    Content = label,
                    Kind = ComponentKind.Button,
                    ButtonStyle = style,
                };
        }
        public GameUi Build()
        {
            GameUi result = new GameUi
            {
                MessageEmbed =
                [
                    new MessageEmbed
                    {
                        Author = _header,
                        Color = _color,
                        Title = _title,
                        Description = _description,
                        ThumbnailUrl = _nextCardUrl,
                        ImageUrl = _currentCardUrl,
                        Footer = _footer
                    }
                ],
                MessageComponents = BuildButtons()
                    .ToArray()
            };

            return result;
        }
    }
}