using System.Text;
using System.Text.RegularExpressions;
using Discord;
using Discord.WebSocket;

namespace TsDiscordBot.Core.Utility
{
    public record AttachmentInfo(string Url, string ContentType);

    public record ConvertedMessage(
        string Content,
        string Author,
        DateTimeOffset Date,
        bool FromTsumugi,
        bool FromSystem,
        IReadOnlyList<AttachmentInfo>? Attachments = null);

    public static class DiscordToOpenAIMessageConverter
    {
        public static ConvertedMessage ConvertFromDiscord(IMessage message)
        {
            return ConvertFromDiscord(message, null);
        }

        private const int MaxQuoteLen = 120; // 返信の引用は短めに
        private const int MaxBodyLen = 600; // 1メッセージの最大長（プロンプト肥大化防止）

        private const ulong _tsumugiId = 1315441123715579985;

        public static ConvertedMessage ConvertFromDiscord(IMessage message, ConvertedMessage? reply = null)
        {
            // 表示名
            string author = DiscordUtility.GetAuthorNameFromMessage(message);

            // ベース本文
            string body = message.Content?.Trim() ?? string.Empty;

            // コマンド接頭辞の除去（必要に応じて増やせる）
            body = StripBotPrefixes(body, new[] { "!つむぎ" });

            // メンション/チャンネル/ロールの解決（@123 → @name, <#123> → #general 等）
            body = ResolveMentions(body, message);

            // コードブロックは保つ（整形のみ）
            body = NormalizeCodeBlocks(body);

            // 返信（リプライ）を要約して先頭に付与
            if (reply is not null && !string.IsNullOrWhiteSpace(reply.Content))
            {
                var quoted = TrimOneLine(reply.Content, MaxQuoteLen);
                body = $"⤷ @{reply.Author}: {quoted}\n{body}";
            }

            // 添付ファイル/スタンプ/埋め込みの軽要約
            body = AppendLightSummaries(body, message);

            // 本文が空ならプレースホルダ（例：添付のみの投稿対策）
            if (string.IsNullOrWhiteSpace(body))
                body = "(内容なし)";

            // 過剰長トリム（末尾に…）
            if (body.Length > MaxBodyLen)
                body = body[..MaxBodyLen] + "…";

            // 最終：ローカル時刻に変換（表示用）
            var when = message.Timestamp.ToLocalTime();

            bool isTsumugi = message.Author.Id == _tsumugiId;

            IReadOnlyList<AttachmentInfo>? attachments = null;
            if (message is IUserMessage um && um.Attachments.Count > 0)
            {
                var list = new List<AttachmentInfo>();
                foreach (var a in um.Attachments)
                {
                    list.Add(new AttachmentInfo(a.Url, a.ContentType ?? "application/octet-stream"));
                }
                attachments = list;
            }

            return new ConvertedMessage(body, author, when, isTsumugi, false, attachments);
        }

        private static string StripBotPrefixes(string text, IEnumerable<string> prefixes)
        {
            foreach (var p in prefixes)
            {
                if (text.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                    return text[p.Length..]
                        .Trim();
            }

            return text;
        }

        private static string NormalizeCodeBlocks(string text)
        {
            // 改行・全角スペース周りの軽整形（必要以上に触らない）
            text = text.Replace("\r\n", "\n");
            // 連続空行を1つに
            text = Regex.Replace(text, @"\n{3,}", "\n\n");
            return text.Trim();
        }

        private static string ResolveMentions(string text, IMessage msg)
        {
            if (msg is IUserMessage um)
            {
                // ユーザーメンション置換
                foreach (var uid in um.MentionedUserIds)
                {
                    var user = (msg.Channel as SocketGuildChannel)?.Guild.GetUser(uid);
                    if (user != null)
                        text = Regex.Replace(text, $@"<@!?{uid}>", $"@{user.DisplayName ?? user.Username}");
                }

                // ロール置換
                if (msg.Channel is SocketGuildChannel gch)
                {
                    var guild = gch.Guild;
                    foreach (var rid in um.MentionedRoleIds)
                    {
                        var role = guild.GetRole(rid);
                        if (role != null)
                            text = Regex.Replace(text, $@"<@&{rid}>", $"@{role.Name}");
                    }

                    // チャンネル置換
                    text = Regex.Replace(text,
                        @"<#(\d+)>",
                        m =>
                        {
                            if (ulong.TryParse(m.Groups[1].Value, out var id))
                            {
                                var ch = guild.GetChannel(id);
                                return ch != null ? $"#{ch.Name}" : m.Value;
                            }

                            return m.Value;
                        });
                }
            }

            // @everyone/@here はそのまま残す
            return text;
        }

        private static string AppendLightSummaries(string body, IMessage msg)
        {
            var sb = new StringBuilder(body);

            if (msg is IUserMessage um)
            {
                // 添付
                foreach (var a in um.Attachments)
                {
                    var kind = a.ContentType?.Split('/')[0] ?? "file";
                    sb.AppendLine()
                        .Append($"[attach:{kind}:{a.Filename}]");
                }

                // 埋め込み（タイトルのみ）
                foreach (var e in um.Embeds)
                {
                    var title = e.Title ?? e.Url?.ToString() ?? "embed";
                    sb.AppendLine()
                        .Append($"[embed:{TrimOneLine(title, 60)}]");
                }

                // ステッカー
                if (um.Stickers?.Count > 0)
                {
                    foreach (var s in um.Stickers)
                        sb.AppendLine()
                            .Append($"[sticker:{s.Name}]");
                }
            }

            return sb.ToString()
                .TrimEnd();
        }

        private static string TrimOneLine(string text, int max)
        {
            // 改行をスペースに、長すぎたら省略
            var one = Regex.Replace(text, @"\s+", " ")
                .Trim();
            return one.Length <= max ? one : one[..max] + "…";
        }
    }
}