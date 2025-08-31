export default function App() {
  return (
    <main style={{ maxWidth: 960, margin: '40px auto', padding: '0 16px', lineHeight: 1.6 }}>
      <h1>TsDiscordBot</h1>
      <p>Discord.NET 製ボットの機能紹介サイト（仮）。まずは React が動けばOK！</p>

      <h2>今後載せる予定</h2>
      <ul>
        <li>匿名プロフィール（キャラ選択）</li>
        <li>マルチサーバー（Oversea）リレー</li>
        <li>Webhook 連携・運用Tips</li>
        <li>導入手順 / コマンド一覧</li>
      </ul>

      <p>
        GitHub:{" "}
        <a href="https://github.com/p4j4dyxcry/Discord.NET-bot" target="_blank" rel="noreferrer">
          p4j4dyxcry/Discord.NET-bot
        </a>
      </p>
    </main>
  );
}
