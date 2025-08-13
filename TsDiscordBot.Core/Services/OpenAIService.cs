
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
        private readonly LimitedQueue<ChatMessage> _history = new(20);

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

        public string GetEducationPrompt(ulong guildId)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("# 以下は長期つむぎの長期記憶です。 Discord に参加してもらったユーザーから教えてもらった大事な情報です。");

            var memories = _databaseService.FindAll<LongTermMemory>(LongTermMemory.TableName)
                .Where(x => x.GuildId == guildId)
                .ToArray();

            foreach (var memory in memories)
            {
                sb.AppendLine($"{memory.Content} by {memory.Author}");
            }

            return sb.ToString();
        }

        public record Message(string Content, string Author, DateTimeOffset Date);

        public async Task<string> GetResponse(ulong guildId,Message message,Message[] previousMessages)
        {
            StringBuilder serverContextBuilder = new();
            serverContextBuilder.AppendLine("#Discord サーバー内の直前のやりとり");

            if (previousMessages.Length > 0)
            {
                foreach (Message previousMessage in previousMessages)
                {
                    serverContextBuilder.AppendLine($"{previousMessage.Author}({previousMessage.Date}):{previousMessage.Content}");
                }
            }
            else
            {
                serverContextBuilder.AppendLine("メッセージはありません。");
            }

            StringBuilder sb = new();

            sb.AppendLine("# 送信者情報");
            sb.AppendLine($"{message.Author},{message.Date}");
            sb.AppendLine("# 入力メッセージ");
            sb.AppendLine(message.Content);

            string prompt = sb.ToString();

            ChatMessage userMessage = ChatMessage.CreateUserMessage(prompt);
            _history.Enqueue(userMessage);
            ChatCompletion completion = await _client
                .CompleteChatAsync(
                    new[]{
                            ChatMessage.CreateSystemMessage(_systemPrompt),
                            ChatMessage.CreateSystemMessage( GetEducationPrompt(guildId)),
                            ChatMessage.CreateSystemMessage(serverContextBuilder.ToString())
                        }
                    .Concat(_history));

            string response = completion.Content[0].Text;

            _history.Enqueue(ChatMessage.CreateAssistantMessage(response));

            return completion.Content[0].Text;
        }

        public void ClearHistory()
        {
            _history.Clear();
        }
    }
}