using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.Commands;

public class EducationEntry
{
    public string Content { get; }
    public string Author { get; }

    public EducationEntry(string content, string author)
    {
        Content = content;
        Author = author;
    }
}

public class AICommandModule: InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger _logger;
    private readonly DatabaseService _databaseService;
    private readonly OpenAIService _openAiService;
    private readonly Random _rand = new();
    private readonly Dictionary<int, EducationEntry> _educationEntries = new();

    public AICommandModule(ILogger<ReactionCommandModule> logger, DatabaseService databaseService,OpenAIService openAiService)
    {
        _logger = logger;
        _databaseService = databaseService;
        _openAiService = openAiService;

        _educationEntries.Add(0,new EducationEntry("abc","papa"));
        _educationEntries.Add(1,new EducationEntry("def","aya"));
    }

    // /ai manage コマンドを定義
    [SlashCommand("ai-manage", "わからない事をつむぎちゃんが教えてくれます。")]
    public async Task ManageEducation()
    {
        var embed = CreateEducationEmbed();
        var component = CreateComponent();

        var components = new ComponentBuilder()
            .WithRows([component])
            .Build();

        // メッセージを送信
        await RespondAsync(embed: embed, components: components);
    }

    // 教育内容を表示するEmbedを作成するメソッド
    private Embed CreateEducationEmbed()
    {
        var builder = new EmbedBuilder()
            .WithTitle("教育内容の管理")
            .WithDescription("以下の教育内容があります。")
            .WithColor(Discord.Color.Green);

        foreach (var entry in _educationEntries)
        {
            builder.AddField($"ID: {entry.Key}", $"{entry.Value.Content} - 登録者: {entry.Value.Author}", true);
        }

        return builder.Build();
    }

    // 教育内容の管理用ボタンコンポーネントを作成
    private ActionRowBuilder CreateComponent()
    {
        var manageButton = new ButtonBuilder()
            .WithStyle(ButtonStyle.Primary)
            .WithLabel("manage")
            .WithEmote(new Emoji("🛠"))
            .Build();

        ButtonComponent? deleteButton = new ButtonBuilder()
            .WithStyle(ButtonStyle.Danger)
            .WithLabel("delete")
            .WithEmote(new Emoji("❌"))
            .Build();

        return new ActionRowBuilder()
            .WithComponents(new[] { manageButton, deleteButton }.ToList<IMessageComponent>());

    }

    // 追加: ボタンが押された時の反応
    [ComponentInteraction("manage")]
    public async Task ManageEducationContent()
    {
        var embed = new EmbedBuilder()
            .WithTitle("教育内容の管理")
            .WithDescription("教育内容の管理ができます。")
            .AddField("操作方法", "教育内容の追加、削除、更新が可能です。")
            .Build();

        var component = CreateManageActionButtons();

        var components = new ComponentBuilder()
            .WithRows([component])
            .Build();

        await RespondAsync(embed: embed, components: components);
    }

    // 追加: 管理用ボタンコンポーネント（教育内容の操作）
    private ActionRowBuilder CreateManageActionButtons()
    {
        var addButton = new ButtonBuilder()
            .WithStyle(ButtonStyle.Success)
            .WithLabel("add")
            .WithEmote(new Emoji("➕"))
            .Build();

        ButtonComponent? deleteButton = new ButtonBuilder()
            .WithStyle(ButtonStyle.Danger)
            .WithLabel("delete")
            .WithEmote(new Emoji("❌"))
            .Build();

        return new ActionRowBuilder()
            .WithComponents(new[] { addButton, deleteButton }.ToList<IMessageComponent>());
    }

    // 削除ボタンが押された時の処理
    [ComponentInteraction("delete")]
    public async Task DeleteEducationContent(string[] args)
    {
        if (args.Length == 0)
        {
            await RespondAsync("削除する教育内容のIDを指定してください。");
            return;
        }

        int entryId;
        if (!int.TryParse(args[0], out entryId))
        {
            await RespondAsync("無効なIDです。");
            return;
        }

        if (_educationEntries.ContainsKey(entryId))
        {
            _educationEntries.Remove(entryId);
            await RespondAsync($"教育内容（ID: {entryId}）を削除しました。");
        }
        else
        {
            await RespondAsync($"ID: {entryId} の教育内容は存在しません。");
        }
    }

    // 追加ボタンの処理
    [ComponentInteraction("add")]
    public async Task AddEducationContent()
    {
        var embed = new EmbedBuilder()
            .WithTitle("教育内容の追加")
            .WithDescription("新しい教育内容を追加することができます。")
            .Build();

        // 必要に応じて、ユーザー入力を受けるための処理を追加
        await RespondAsync(embed: embed);
    }
}