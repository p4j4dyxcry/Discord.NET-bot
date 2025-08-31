export default function App() {
  return (
    <div className="min-h-screen bg-white text-gray-900">
      {/* Header */}
      <header className="sticky top-0 z-30 border-b bg-white/80 backdrop-blur">
        <div className="mx-auto flex h-16 max-w-screen-xl items-center justify-between px-4">
          <a href="/" className="flex items-center gap-2 font-semibold">
            <span className="inline-block h-6 w-6 rounded bg-black" />
            <span>TsDiscordBot</span>
          </a>
          <nav className="flex items-center gap-4">
            <a href="#features" className="text-sm text-gray-600 hover:text-gray-900">Features</a>
            <a href="#install" className="text-sm text-gray-600 hover:text-gray-900">Install</a>
            <a
              className="rounded-lg border px-3 py-1.5 text-sm hover:bg-gray-50"
              href="https://github.com/p4j4dyxcry/Discord.NET-bot"
              target="_blank" rel="noreferrer"
            >
              GitHub
            </a>
          </nav>
        </div>
      </header>

      {/* Hero */}
      <section className="mx-auto max-w-screen-xl px-4 py-24">
        <div className="grid items-center gap-10 md:grid-cols-2">
          <div>
            <h1 className="text-4xl font-bold tracking-tight md:text-5xl">
              シンプルに強い Discord.NET 製ボット
            </h1>
            <p className="mt-5 text-lg text-gray-600">
              匿名プロフィール、マルチサーバー・リレー、Webhook 連携。
              最低限の設定で導入できる、開発者フレンドリーな設計です。
            </p>
            <div className="mt-8 flex gap-3">
              <a
                href="#install"
                className="rounded-lg bg-black px-5 py-3 text-white hover:opacity-90"
              >
                Get Started
              </a>
              <a
                href="https://github.com/p4j4dyxcry/Discord.NET-bot"
                target="_blank" rel="noreferrer"
                className="rounded-lg border px-5 py-3 hover:bg-gray-50"
              >
                View on GitHub
              </a>
            </div>
          </div>
          <div className="rounded-2xl border bg-gray-50 p-6">
            <pre className="overflow-x-auto text-sm leading-relaxed">
{`// Sample: Oversea relay
/oversea-register 1001
/oversea-enable-anonymous @user
// Post in this channel -> relayed to linked servers (anonymized)`}
            </pre>
          </div>
        </div>
      </section>

      {/* Features */}
      <section id="features" className="border-t bg-gray-50">
        <div className="mx-auto max-w-screen-xl px-4 py-20">
          <h2 className="text-2xl font-semibold">主な機能</h2>
          <p className="mt-2 text-gray-600">最初に知っておきたいポイント</p>

          <div className="mt-10 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
            <Feature
              title="匿名プロフィール"
              desc="キャラ名＋アイコンで投稿。ユーザー本体を隠して会話できます。"
            />
            <Feature
              title="マルチサーバー・リレー"
              desc="登録チャンネル間でメッセージを中継。匿名/実名は設定で切替。"
            />
            <Feature
              title="Webhook 連携"
              desc="チャンネルごとにWebhookを再利用。高速で安定したマルチポスト。"
            />
            <Feature
              title="簡単セットアップ"
              desc="必要最低限のコマンドだけで開始。あとから細かい調整も可能。"
            />
            <Feature
              title=".NET ベース"
              desc="Discord.NET を採用。C#で実装されており拡張も容易。"
            />
            <Feature
              title="OSS / GitHub"
              desc="ソースと導入手順は GitHub に公開。Issue/PR 歓迎。"
            />
          </div>
        </div>
      </section>

      {/* Install */}
      <section id="install" className="mx-auto max-w-screen-xl px-4 py-20">
        <h2 className="text-2xl font-semibold">導入手順（ざっくり）</h2>
        <ol className="mt-6 list-decimal space-y-3 pl-6 text-gray-700">
          <li>Bot をサーバーに招待し、対象チャンネルで権限を確認</li>
          <li><code className="rounded bg-gray-100 px-1.5 py-0.5">/oversea-register &lt;id&gt;</code> を実行</li>
          <li>匿名にしたい場合は <code className="rounded bg-gray-100 px-1.5 py-0.5">/oversea-enable-anonymous @user</code></li>
          <li>メッセージを送って中継・匿名化の動作を確認</li>
        </ol>
        <p className="mt-6 text-gray-600">
          詳細は GitHub の README をご覧ください。
        </p>
      </section>

      {/* Footer */}
      <footer className="border-t py-10 text-center text-sm text-gray-500">
        <p>
          © {new Date().getFullYear()} TsDiscordBot —{" "}
          <a
            className="underline hover:text-gray-700"
            href="https://github.com/p4j4dyxcry/Discord.NET-bot"
            target="_blank" rel="noreferrer"
          >
            GitHub
          </a>
        </p>
      </footer>
    </div>
  );
}

function Feature({ title, desc }) {
  return (
    <div className="rounded-2xl border bg-white p-6">
      <h3 className="font-semibold">{title}</h3>
      <p className="mt-2 text-sm text-gray-600">{desc}</p>
    </div>
  );
}
