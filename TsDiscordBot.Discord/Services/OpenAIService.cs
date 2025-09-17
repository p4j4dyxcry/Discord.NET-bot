
using System.Text;
using System.ClientModel;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using TsDiscordBot.Core.Constants;
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

        public async Task<string> GetResponse(ulong guildId,ConvertedMessage? convertedMessage,ConvertedMessage[] previousMessages)
        {
            var recent = previousMessages
                .OrderBy(x => x.Date)
                .TakeLast(20);

            ChatMessage.CreateSystemMessage(GetEducationPrompt(guildId));

            var conversationHistory = ToChatHistoryWithSparseNames(recent);
            if (convertedMessage is not null)
            {
                conversationHistory.AddRange(ToChatHistoryWithSparseNames([convertedMessage]));
            }

            try
            {
                ChatCompletion completion = await _client
                    .CompleteChatAsync(
                        new[]{
                                ChatMessage.CreateSystemMessage(_systemPrompt),
                                HasEducation(guildId) ? ChatMessage.CreateSystemMessage(GetEducationPrompt(guildId)) : null
                            }.Where(x=> x is not null)
                        .Concat(conversationHistory));

                return completion.Content[0].Text;
            }
            catch (ClientResultException ex)
            {
                var code = OpenAIErrorHelper.TryGetErrorCode(ex);
                return code switch
                {
                    "insufficient_quota" => ErrorMessages.InsufficientQuota,
                    "content_policy_violation" => ErrorMessages.ContentPolicyViolationQuestion,
                    _ => ErrorMessages.ContentPolicyViolationQuestion
                };
            }
        }

        List<ChatMessage> ToChatHistoryWithSparseNames(IEnumerable<ConvertedMessage> msgs)
        {
            var list = msgs.OrderBy(m => m.Date).ToList();
            var result = new List<ChatMessage>();
            string lastUser = string.Empty;

            foreach (var m in list)
            {
                var text = m.Content.Trim();
                var parts = new List<ChatMessageContentPart>();

                if (!string.IsNullOrWhiteSpace(text))
                {
                    if (!m.FromTsumugi && !m.FromSystem && lastUser != m.Author)
                    {
                        text = $"@{m.Author}: {text}";
                    }
                    parts.Add(ChatMessageContentPart.CreateTextPart(text));
                }

                if (m.Attachments is { Count: > 0 })
                {
                    foreach (var att in m.Attachments)
                    {
                        if (att.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                        {
                            if (Uri.TryCreate(att.Url, UriKind.Absolute, out var uri))
                            {
                                parts.Add(ChatMessageContentPart.CreateImagePart(uri));
                            }
                        }
                    }
                }

                if (parts.Count == 0)
                {
                    continue;
                }

                if (m.FromSystem)
                {
                    result.Add(ChatMessage.CreateSystemMessage(parts));
                }
                else if (m.FromTsumugi)
                {
                    result.Add(ChatMessage.CreateAssistantMessage(parts));
                }
                else
                {
                    result.Add(ChatMessage.CreateUserMessage(parts));
                }

                lastUser = m.Author;
            }
            return result;
        }
    }
}