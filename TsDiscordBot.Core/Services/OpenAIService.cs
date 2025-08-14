
using System.Text;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using TsDiscordBot.Core.Data;
using TsDiscordBot.Core.Utility;

namespace TsDiscordBot.Core.Services
{
    public class OpenAIService
    {
        private readonly DatabaseService _databaseService;
        private readonly ChatClient _client;

        private readonly string _systemPrompt;

        public OpenAIService(IConfiguration config, DatabaseService databaseService)
        {
            _databaseService = databaseService;

            var apiKey = Envs.OPENAI_API_KEY;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                apiKey = config["open_ai_api_key"];
            }

            _client = new(model: "gpt-5-nano", apiKey: apiKey);

            var systemPrompt = Envs.OPENAI_PROMPT;
            if (string.IsNullOrWhiteSpace(systemPrompt))
            {
                systemPrompt = config["open_ai_prompt"];
            }

            if (string.IsNullOrWhiteSpace(systemPrompt))
            {
                try
                {
                    systemPrompt = File.ReadAllText("_prompt.txt");
                }
                catch
                {
                    systemPrompt = string.Empty;
                }
            }

            _systemPrompt = systemPrompt ?? string.Empty;
        }

        private bool HasEducation(ulong guildId)
        {
            return _databaseService
                .FindAll<LongTermMemory>(LongTermMemory.TableName)
                .Any(x => x.GuildId == guildId);
        }

        private string GetEducationPrompt(ulong guildId)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("# 以下は長期つむぎの長期記憶です。 Discord に参加してもらったユーザーから教えてもらった情報です。");

            var memories = _databaseService.FindAll<LongTermMemory>(LongTermMemory.TableName)
                .Where(x => x.GuildId == guildId)
                .ToArray();

            foreach (var memory in memories)
            {
                sb.AppendLine($"{memory.Content} by {memory.Author}");
            }

            return sb.ToString();
        }

        public async Task<string> GetResponse(ulong guildId,ConvertedMessage convertedMessage,ConvertedMessage[] previousMessages)
        {
            var recent = previousMessages
                .OrderBy(x => x.Date)
                .TakeLast(20);

            ChatMessage.CreateSystemMessage(GetEducationPrompt(guildId));

            var conversationHistory = ToChatHistoryWithSparseNames(recent);
            conversationHistory.AddRange(ToChatHistoryWithSparseNames([convertedMessage]));

            ChatCompletion completion = await _client
                .CompleteChatAsync(
                    new[]{
                            ChatMessage.CreateSystemMessage(_systemPrompt),
                            HasEducation(guildId) ? ChatMessage.CreateSystemMessage(GetEducationPrompt(guildId)) : null
                        }.Where(x=> x is not null)
                    .Concat(conversationHistory));

            return completion.Content[0].Text;
        }

        List<ChatMessage> ToChatHistoryWithSparseNames(IEnumerable<ConvertedMessage> msgs)
        {
            var list = msgs.OrderBy(m => m.Date).ToList();
            var result = new List<ChatMessage>();
            string lastUser = string.Empty;

            foreach (var m in list)
            {
                var text = m.Content.Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (!m.FromTsumugi && lastUser != m.Author)
                {
                    text = $"@{m.Author}: {text}";
                }

                if (m.FromTsumugi)
                {
                    result.Add(ChatMessage.CreateAssistantMessage(text));
                }
                else
                {
                    result.Add(ChatMessage.CreateUserMessage(text));
                }

                lastUser = m.Author;
            }
            return result;
        }
    }
}