import headerIcon from "./assets/hero.png";
import githubIcon from "./assets/github.svg";

import feature1Image from "./assets/feature_card/feature1.png";
import feature2Image from "./assets/feature_card/feature2.png";
import feature3Image from "./assets/feature_card/feature3.png";
import feature4Image from "./assets/feature_card/feature4.png";
import feature5Image from "./assets/feature_card/feature5.png";
import feature6Image from "./assets/feature_card/feature6.png";

function FeatureCard({ img, title, desc }) {
  return (
    <div className="rounded-2xl border bg-white shadow hover:shadow-lg transition p-4">
      <img
        src={img}
        alt={title}
        className="rounded-lg mb-3 w-full object-cover"
      />
      <h3 className="font-semibold text-lg">{title}</h3>
      <p className="mt-1 text-sm text-gray-600">{desc}</p>
    </div>
  );
}

export default function App() {
  return (
    <div className="min-h-screen bg-white text-gray-900">
      {/* Header */}
      <header className="sticky top-0 z-30 border-b bg-white/80 backdrop-blur">
        <div className="mx-auto flex h-16 max-w-screen-xl items-center justify-between px-4">
          <a href="/" className="flex items-center gap-2 font-semibold">
            <img
              src={headerIcon}
              alt="TsDiscordBot Logo"
              className="h-6 w-6 rounded-full object-cover"
            />
            <span>つむぎ (Discord Bot)</span>
          </a>
          <nav className="flex items-center gap-4">
            <a href="#features" className="text-sm text-gray-600 hover:text-gray-900">Features</a>
            <a href="#commands" className="text-sm text-gray-600 hover:text-gray-900">Commands</a>
            <a
              className="inline-flex items-center gap-2 rounded-lg border px-3 py-1.5 hover:bg-gray-50"
              href="https://github.com/p4j4dyxcry/Discord.NET-bot"
              target="_blank" rel="noreferrer"
            >
              <img
                src={githubIcon}
                alt="GitHub"
                className="h-5 w-5"
              />
              <span>GitHub</span>
            </a>
          </nav>
        </div>
      </header>

      <section className="relative">
        <div className="mx-auto grid max-w-screen-xl items-center gap-10 px-4 py-20 md:grid-cols-2">
          {/* テキスト側 */}
          <div>
            <h1 className="text-4xl font-bold tracking-tight md:text-5xl">
              サーバーに遊びを、<br />
              もっと自由に話せるDiscord
            </h1>
            <p className="mt-5 text-lg text-gray-600">
              匿名プロフィール、メッセージ自動削除、BeReal投稿、画像生成…  
              ユニークな機能でサーバーの会話が盛り上げていきましょう！
            </p>

            <div className="mt-8 flex flex-wrap gap-3">
              <a
                href="https://github.com/p4j4dyxcry/Discord.NET-bot"
                target="_blank"
                rel="noreferrer"
                className="rounded-xl border px-5 py-3 hover:bg-gray-50"
              >
                GitHubで見る
              </a>
            </div>
          </div>

          {/* 画像側 */}
          <div className="mx-auto w-full max-w-sm">
            <img
              src={headerIcon}
              alt="TsDiscordBot Hero"
              className="h-full w-full rounded-3xl object-cover shadow-xl ring-1 ring-black/5"
            />
          </div>
        </div>
      </section>

      {/* Features */}
      <section id="features" className="mx-auto max-w-screen-xl px-4 py-20">
        <h2 className="text-2xl font-bold text-center">つむぎのできること</h2>
        <p className="mt-2 text-center text-gray-600">
          ここに紹介されていない機能も盛りだくさん。随時開発中です！
        </p>

        <div className="mt-10 grid gap-6 sm:grid-cols-2 lg:grid-cols-2">
          <FeatureCard
            img={feature1Image}
            title="匿名会話"
            desc="キャラ名とアイコンでなりきり投稿。ユーザー本人を隠して会話できます。更に追加の設定で別サーバーと匿名同士の会話も可能！"
          />
          <FeatureCard
            img={feature2Image}
            title="テレグラムモード"
            desc="30分で自動削除されるチャンネル。秘密の話やログを残したくない会話に最適。最短１分から自由に設定できます。残したいメッセージはピン止めで残すことも。"
          />
          <FeatureCard
            img={feature3Image}
            title="Be-real 投稿"
            desc="画像を投稿すると24時間だけ他人の投稿を閲覧可能。BeReal風の体験をDiscordで。"
          />
          <FeatureCard
            img={feature4Image}
            title="禁止ワードフィルター"
            desc="管理者が登録したワードを即座にブロック。健全なサーバー運営をサポート。投稿の伏字化とメッセージの削除の２つのモードから選べます。"
          />
          <FeatureCard
            img={feature5Image}
            title="AI会話"
            desc="AI Botのつむぎちゃんと自然に会話。アイディア出しや雑談、英語練習など多用途に活用できます。"
          />
          <FeatureCard
            img={feature6Image}
            title="画像生成"
            desc="Discord上でAI画像生成。キャラ立ち絵やネタ画像を簡単に作成できます。既存の画像を元にイラスト化や高画質化の依頼も可能！"
          />
        </div>
      </section>

      {/* Install */}
      <section id="commands" className="mx-auto max-w-screen-xl px-4 py-20">
        <p className="mt-6 text-gray-600">
          詳細は GitHub の README をご覧ください。
        </p>
      </section>

      {/* Footer */}
      <footer className="border-t py-10 text-center text-sm text-gray-500">
        <p>
          © {new Date().getFullYear()} p4j4dyxcry (ytsune) —{" "}

          <a
            className="inline-flex items-center gap-2 rounded-lg px-3 py-1.5 hover:bg-gray-50"
            href="https://github.com/p4j4dyxcry/Discord.NET-bot"
            target="_blank" rel="noreferrer"
          >
              <span>GitHub</span>
              <img
                src={githubIcon}
                alt="GitHub"
                className="h-5 w-5"
              />

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
