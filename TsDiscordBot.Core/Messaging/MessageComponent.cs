namespace TsDiscordBot.Core.Messaging
{
    public enum ButtonStyle
    {
        /// <summary>
        ///     A Blurple button.
        /// </summary>
        Primary = 1,

        /// <summary>
        ///     A Grey (or gray) button.
        /// </summary>
        Secondary = 2,

        /// <summary>
        ///     A Green button.
        /// </summary>
        Success = 3,

        /// <summary>
        ///     A Red button.
        /// </summary>
        Danger = 4,

        /// <summary>
        ///     A <see cref="Secondary"/> button with a little popup box indicating that this button is a link.
        /// </summary>
        Link = 5,

        /// <summary>
        ///     A gradient button, opens a product's details modal.
        /// </summary>
        Premium = 6,
    }

    public enum ComponentKind
    {
        Button,
    }

    public class MessageComponent
    {
        public ComponentKind Kind { get; set; }

        public ButtonStyle ButtonStyle { get; set; }

        public string Content { get; set; } = string.Empty;

        public string ActionId { get; set; } = string.Empty;
    }
}