# Discord.NET ボット(つむぎ)

このプロジェクトは、Discord.NET と OpenAI を利用して実装された多機能な Discord ボットです。
会話の生成、定型リアクション、禁止ワード検知、自動メッセージ送信などをサポートします。

This project is a feature-rich Discord bot built with Discord.NET and OpenAI.
It supports chat generation, automatic reactions, banned word detection, and scheduled messages.

## 特徴 / Features
- OpenAI を用いた対話機能 / OpenAI-based conversational replies
- スラッシュコマンドによる操作 / Control via slash commands
- 特定ワードへのリアクション設定 / Custom reactions triggered by keywords
- R18 ワードの検知とメッセージ削除 / Detects registered R18 words and deletes messages
- 会話を促す自動メッセージ投稿 / Posts prompts automatically to encourage conversation
- AI を使った画像生成・編集 / Generate and edit images with AI
- 投票機能（作成・集計） / Create polls and tally results
- 指定時刻にメッセージを送るリマインダー / Schedule reminders at specific times
- ダイスを振る簡単なゲーム / Roll dice for simple games
- 長期記憶の追加・削除・一覧表示 / Manage long-term memories (add/remove/list)
- 「なう(20xx/xx/xx ...)」への自動応答 / Automatic response to messages starting with "なう(20"

## 必要な環境変数 / Required Environment Variables
- `DISCORD_TOKEN` : Discord ボットのトークン / Discord bot token
- `OPENAI_API_KEY` : OpenAI の API キー / OpenAI API key
- `OPENAI_PROMPT` : AI 振る舞いを制御するシステムプロンプト (任意) / System prompt for AI behavior (optional)
- `LITEDB_PATH` : LiteDB データベースの保存先パス (任意) / Storage path for LiteDB database (optional)
サーバー(ギルド)毎の個別のコンテキストを保存するためのデータベースです。 Database for saving contexts each guilds.

# For Developer

## 実行方法 / Running
1. .NET 8 SDK をインストールします。
   Install the .NET 8 SDK.
2. 必要な環境変数を設定します。
   Set the required environment variables.
3. プロジェクトをビルド・実行します。
   Build and run the project.

```bash
# ビルド / Build
dotnet build

# 実行 / Run
dotnet run --project TsDiscordBot.Entry
```

## Docker での実行 / Running with Docker
```bash
# ビルド / Build
docker build -t discord-net-bot .

# 実行 / Run
docker run -e DISCORD_TOKEN=your_token -e OPENAI_API_KEY=your_key discord-net-bot
```

## コードデザイン / Code Design
- 機能の中核は `TsDiscordBot.Core` に集約し、実行エントリは `TsDiscordBot.Entry` プロジェクトでホストしています。
- 設定や機能拡張を容易にするため、依存性注入とモジュラー構成を採用しています。

## ライセンス / License
このプロジェクトは MIT ライセンスの下で公開されています。詳細は [LICENSE](LICENSE) を参照してください。

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.
