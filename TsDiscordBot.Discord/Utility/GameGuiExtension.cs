// エイリアスで名前衝突を回避（あなたのDTO名とDiscord.NETの型名が被るため）

using Discord;
using TsDiscordBot.Core.Game;
using DButtonStyle = Discord.ButtonStyle;
using DComponentBuilder = Discord.ComponentBuilder;
using DMessageComponent = Discord.MessageComponent;
using DEmbed = Discord.Embed;
using DEmbedBuilder = Discord.EmbedBuilder;
using DEmbedAuthorBuilder = Discord.EmbedAuthorBuilder;
using DEmbedFooterBuilder = Discord.EmbedFooterBuilder;

using TsDiscordBot.Core.Messaging;
using ButtonStyle = TsDiscordBot.Core.Messaging.ButtonStyle;
using MessageComponent = TsDiscordBot.Core.Messaging.MessageComponent;

namespace TsDiscordBot.Discord.Utility
{
    public static class GameGuiExtension
    {
        /// <summary>
        /// GameUi を Discord.NET の送信用オブジェクト群に変換します。
        /// - content: そのまま文字列
        /// - components: ActionRowに分割したボタン
        /// - embeds: Embed配列
        /// </summary>
        public static (string Content, DMessageComponent? Components, DEmbed[] Embeds)
            ToDiscord(this GameUi ui)
        {
            var content = ui?.Content ?? string.Empty;

            // Embeds
            var embeds = (ui?.MessageEmbed ?? Array.Empty<MessageEmbed>())
                .Select(ToDiscordEmbedBuilder)
                .Select(b => b.Build())
                .ToArray();

            // Components (ボタンをActionRowに分割)
            var components = BuildComponents(ui?.MessageComponents ?? Array.Empty<MessageComponent>());

            return (content, components, embeds);
        }

        /// <summary>
        /// Embed DTO → EmbedBuilder
        /// </summary>
        private static DEmbedBuilder ToDiscordEmbedBuilder(MessageEmbed src)
        {
            var eb = new DEmbedBuilder();

            if (!string.IsNullOrWhiteSpace(src.Author))
            {
                eb.Author = new DEmbedAuthorBuilder
                {
                    Name = src.Author,
                    IconUrl = string.IsNullOrWhiteSpace(src.AuthorAvatarUrl) ? null : src.AuthorAvatarUrl
                };
            }

            if (!string.IsNullOrWhiteSpace(src.Title)) eb.Title = src.Title;
            if (!string.IsNullOrWhiteSpace(src.Url)) eb.Url = src.Url;
            if (!string.IsNullOrWhiteSpace(src.Description)) eb.Description = src.Description;

            if (src.Color is MessageColor mc)
            {
                eb.Color = new Color(mc.R, mc.G, mc.B);
            }

            if (!string.IsNullOrWhiteSpace(src.ImageUrl)) eb.ImageUrl = src.ImageUrl;
            if (!string.IsNullOrWhiteSpace(src.ThumbnailUrl)) eb.ThumbnailUrl = src.ThumbnailUrl;

            if (src.Fields is not null)
            {
                foreach (var f in src.Fields)
                {
                    // Discord側は空文字でもOKだが、nullは避ける
                    eb.AddField(f.Name ?? string.Empty, f.Value ?? string.Empty, f.Inline);
                }
            }

            if (!string.IsNullOrWhiteSpace(src.Footer) || !string.IsNullOrWhiteSpace(src.FootetIconUrl))
            {
                eb.Footer = new DEmbedFooterBuilder
                {
                    Text = src.Footer ?? string.Empty,
                    IconUrl = string.IsNullOrWhiteSpace(src.FootetIconUrl) ? null : src.FootetIconUrl
                };
            }

            return eb;
        }

        /// <summary>
        /// DTOのMessageComponent配列→DiscordのMessageComponent（ActionRow構築）
        /// Discord制約:
        /// - 1 ActionRow = 最大5ボタン
        /// - 最大5 ActionRow
        /// </summary>
        private static DMessageComponent? BuildComponents(IReadOnlyList<MessageComponent> items)
        {
            if (items.Count == 0)
            {
                return null;
            }

            var builder = new DComponentBuilder();

            // 5つずつでActionRowを切る
            const int maxPerRow = 5;
            const int maxRows = 5;

            var rows = items
                .Select((btn, idx) => new { btn, idx })
                .GroupBy(x => x.idx / maxPerRow)
                .Take(maxRows)
                .Select(g => g.Select(x => x.btn)
                    .ToList())
                .ToList();

            foreach (var row in rows)
            {
                foreach (var btn in row)
                {
                    if (btn.Kind != ComponentKind.Button)
                        continue; // 将来拡張: 他コンポーネント種別の追加に備えてスキップ

                    var style = MapButtonStyle(btn.ButtonStyle);
                    var label = string.IsNullOrWhiteSpace(btn.Content) ? " " : btn.Content;

                    if (style == DButtonStyle.Link)
                    {
                        // Linkは custom_id 不可・URL必須。DTOにURLプロパティがないため ActionId をURLとみなす。
                        // 必要があれば DTO を拡張して Url フィールドを追加してください。
                        if (string.IsNullOrWhiteSpace(btn.ActionId))
                            throw new InvalidOperationException("Link ボタンは ActionId に URL を指定してください。");

                        builder.WithButton(label: label, style: DButtonStyle.Link, url: btn.ActionId);
                    }
                    else
                    {
                        // custom_id 必須（最大100文字が推奨）。空は避ける。
                        var id = string.IsNullOrWhiteSpace(btn.ActionId)
                            ? Guid.NewGuid()
                                .ToString("N")
                            : btn.ActionId;

                        builder.WithButton(label: label, customId: id, style: style);
                    }
                }

                builder.Build(); // 明示的に1行確定（内部でRow管理されるため、Build呼び出しで区切られる）
            }

            return builder.Build();
        }

        /// <summary>
        /// DTOのButtonStyle → DiscordのButtonStyle
        /// Premium(6) はDiscordに存在しないため Primary にフォールバックします。
        /// </summary>
        private static DButtonStyle MapButtonStyle(ButtonStyle style) => style switch
        {
            ButtonStyle.Primary => DButtonStyle.Primary,
            ButtonStyle.Secondary => DButtonStyle.Secondary,
            ButtonStyle.Success => DButtonStyle.Success,
            ButtonStyle.Danger => DButtonStyle.Danger,
            ButtonStyle.Link => DButtonStyle.Link,
            ButtonStyle.Premium => DButtonStyle.Premium, // フォールバック（必要なら例外へ）
            _ => DButtonStyle.Primary
        };
    }
}