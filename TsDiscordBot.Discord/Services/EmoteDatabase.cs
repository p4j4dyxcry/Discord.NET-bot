using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Game;

namespace TsDiscordBot.Discord.Services
{
    public class EmoteCache
    {
        public const string TableName = "emote_cache";

        public int Id { get; set; }

        /// <summary>カードキー: AS, 10D, BG など（大文字）</summary>
        public string Name { get; set; } = null!;

        /// <summary>Discord表示用トークン &lt;:name:id&gt;</summary>
        public string Emote { get; set; } = null!;

        /// <summary>Emote名（サーバー上の実名）</summary>
        public string EmojiName { get; set; } = null!;

        /// <summary>EmoteのID（数値）</summary>
        public ulong EmojiId { get; set; }

        /// <summary>所属Guild</summary>
        public ulong GuildId { get; set; }
    }

    public class EmoteDatabase
    {
        private readonly DiscordSocketClient _discordSocketClient;
        private readonly DatabaseService _databaseService;
        private readonly ILogger<EmoteDatabase> _logger;
        // Rankトークン表記 ↔ Rank の対応
        private static readonly Dictionary<string, Rank> TokenToRank = new(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = Rank.Ace,
            ["2"] = Rank.Two,
            ["3"] = Rank.Three,
            ["4"] = Rank.Four,
            ["5"] = Rank.Five,
            ["6"] = Rank.Six,
            ["7"] = Rank.Seven,
            ["8"] = Rank.Eight,
            ["9"] = Rank.Nine,
            ["10"] = Rank.Ten,
            ["J"] = Rank.Jack,
            ["Q"] = Rank.Queen,
            ["K"] = Rank.King,
        };

        private static readonly Dictionary<Rank, string> RankToToken = TokenToRank
            .ToDictionary(kv => kv.Value, kv => kv.Key);

        // Suit頭文字 ↔ Suit の対応
        private static readonly Dictionary<char, Suit> CharToSuit = new()
        {
            ['C'] = Suit.Clubs,
            ['D'] = Suit.Diamonds,
            ['H'] = Suit.Hearts,
            ['S'] = Suit.Spades,
        };

        private static readonly Dictionary<Suit, char> SuitToChar = CharToSuit
            .ToDictionary(kv => kv.Value, kv => kv.Key);

        /// <summary>
        /// Rank/Suit → "AS", "10D" などのキー
        /// </summary>
        public static string MakeKey(Rank rank, Suit suit)
            => $"{RankToToken[rank]}{SuitToChar[suit]}".ToUpperInvariant();

        private static ulong[] EmojiGuilds { get; } =
        [
            1417849765319802955UL,
            1417843672560439338UL
        ];

        public EmoteDatabase(DiscordSocketClient discordSocketClient,
            DatabaseService databaseService,
            ILogger<EmoteDatabase> logger)
        {
            _discordSocketClient = discordSocketClient;
            _databaseService = databaseService;
            _logger = logger;
        }

        public string GetBackgroundCardEmote()
        {
            return FinEmoteInternalAsString("BG");

        }

        public string GetEmote(Card card)
        {
            return FinEmoteInternalAsString(MakeKey(card.Rank, card.Suit));
        }

        public string GetFlipAnimationEmote(Card card)
        {
            return FinEmoteInternalAsString(MakeKey(card.Rank, card.Suit),"flip_");
        }

        private string FinEmoteInternalAsString(string name, string prefix = "")
        {
            var e = FindEmoteByName(name, prefix);

            if (e is not null)
            {
                return e.ToString();
            }

            return string.Empty;
        }

        public GuildEmote? FindEmoteByCard(Card? card, bool isAnimation)
        {
            if (card is null)
            {
                return null;
            }

            string key = MakeKey(card.Value.Rank, card.Value.Suit);
            string prefix = isAnimation ? "flip_" : string.Empty;

            return FindEmoteByName(key, prefix);
        }

        public GuildEmote? FindEmoteByName(string name, string prefix)
        {
            var guilds = (EmojiGuilds.Length > 0)
                ? EmojiGuilds.Select(id => _discordSocketClient.GetGuild(id)).Where(g => g != null)!
                : _discordSocketClient.Guilds;

            foreach (var guild in guilds)
            {
                _logger.LogInformation($"Searching for Emotes from {guild.Name}");
                foreach (var e in guild.Emotes)
                {
                    if (e.Name == $"{prefix}{name}")
                    {
                        return e;
                    }
                }
            }

            return null;
        }


        /// <summary>
        /// 起動時などに呼び出し：絵文字をスキャンしてDBにUpsert（不足も検出）
        /// </summary>
        public void BuildCache(string? prefix = null)
        {
            _logger.LogInformation("Build cache for Emote");
            var prefixValue = prefix ?? string.Empty;

            // 走査対象ギルドの決定
            var guilds = (EmojiGuilds.Length > 0)
                ? EmojiGuilds.Select(id => _discordSocketClient.GetGuild(id)).Where(g => g != null)!
                : _discordSocketClient.Guilds;

            // 見つけたキー -> EmoteData（最後にUpsert）
            var map = new Dictionary<string, EmoteCache>(StringComparer.OrdinalIgnoreCase);

            foreach (var guild in guilds)
            {
                _logger.LogInformation($"Searching for Emotes from {guild.Name}");
                foreach (var e in guild.Emotes)
                {
                    if (TryMatchEmoteName(e.Name, out var key, prefixValue))
                    {
                        map[key] = new EmoteCache
                        {
                            Name = key,                         // "AS" / "10D" / "BG"
                            EmojiName = e.Name,                 // 実名
                            EmojiId = e.Id,
                            GuildId = guild.Id,
                            Emote = $"<:{e.Name}:{e.Id}>"
                        };
                    }
                }
            }

            // 53種の不足チェック
            var required = AllRequiredKeys();
            var missing = required.Where(k => !map.ContainsKey(k)).ToArray();
            if (missing.Length > 0)
            {
                // ログだけでなく、必要なら _db に「欠落」メタを書いたり通知も
                _logger.LogWarning($"[EmoteCache] missing: {string.Join(",", missing)}");
            }

            // Upsert
            foreach (var rec in map.Values)
            {
                // DatabaseServiceの実装に合わせてUpsert/Replace
                // 例: _db.Upsert(EmoteData.TableName, rec, x => x.Name == rec.Name);
                // 以下は例：同名削除→Insert
                foreach (var old in _databaseService.FindAll<EmoteCache>(EmoteCache.TableName)
                             .Where(x => x.Name == rec.Name))
                {
                    _databaseService.Delete(EmoteCache.TableName,old.Id);
                }
                _databaseService.Insert(EmoteCache.TableName, rec);
            }
        }

        /// <summary>
        /// Emote名を {prefix}{A|2..10|J|Q|K}{C|D|H|S} / {prefix}BG として解析し、カードキー(AS/10D/BG)を返す
        /// </summary>
        private static bool TryMatchEmoteName(string emoteName, out string key, string prefix)
        {
            key = string.Empty;
            if (string.IsNullOrWhiteSpace(emoteName)) return false;

            var name = emoteName.AsSpan();
            if (!string.IsNullOrEmpty(prefix) &&
                name.Length >= prefix.Length &&
                emoteName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                name = name[prefix.Length..];
            }

            // BG（裏）
            if (name.Equals("BG".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                key = "BG";
                return true;
            }

            // 末尾がスート
            if (name.Length < 2) return false;
            var suitCh = char.ToUpperInvariant(name[^1]);
            if (!CharToSuit.ContainsKey(suitCh)) return false;

            // 残りがランク
            var rankToken = name[..^1].ToString().ToUpperInvariant();
            if (!TokenToRank.TryGetValue(rankToken, out var rank)) return false;

            var suit = CharToSuit[suitCh];
            key = MakeKey(rank, suit); // 正規化（大文字）
            return true;
        }

        /// <summary>必要キー53種（AS..KH + BG）</summary>
        private static IReadOnlyList<string> AllRequiredKeys()
        {
            var list = new List<string>(53);
            foreach (Suit s in Enum.GetValues<Suit>())
            foreach (Rank r in Enum.GetValues<Rank>())
                list.Add(MakeKey(r, s));
            list.Add("BG");
            return list;
        }
    }
}