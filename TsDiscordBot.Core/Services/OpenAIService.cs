
using System.Text;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using TsDiscordBot.Core.Utility;

namespace TsDiscordBot.Core.Services
{
    public class OpenAIService
    {
        private readonly ChatClient _client;

        private readonly string _systemPrompt;
        private readonly LimitedQueue<ChatMessage> _history = new(20);

        public OpenAIService(IConfiguration config)
        {
            _client = new(model: "gpt-4o-mini", apiKey: config["open_ai_api_key"]);;

            _systemPrompt = string.Empty;
            try
            {
                _systemPrompt = File.ReadAllText("_prompt.txt");
            }
            catch
            {
                //ignored.
            }
        }

        public string GetEducationPrompt()
        {
            StringBuilder sb = new StringBuilder();
            return sb.ToString();
        }

        public record Message(string Content, string Author, DateTimeOffset Date);

        public async Task<string> GetResponse(Message message,Message[] previousMessages)
        {
            StringBuilder serverContextBuilder = new();
            serverContextBuilder.AppendLine("#サーバー内の直前のやりとり");

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
                            ChatMessage.CreateSystemMessage(GetEducationPrompt()),
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