import { useState, useRef } from "react";

import Header from "./Header";
import AutoPlayVideo from "./components/AutoPlayVideo";

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

function CommandItem({ name, desc }) {
  return (
    <div className="rounded-2xl border bg-white p-4">
      <h3 className="font-semibold">{name}</h3>
      <p className="mt-1 text-sm text-gray-600">{desc}</p>
    </div>
  );
}

const commands = [
  { name: "/be-real-initialize", desc: "be realのチャンネルとロールを作成します。(管理者用)" },
  { name: "/be-real-destroy", desc: "be real の設定を解除します。(管理者用)" },
  { name: "/auto-delete-enable", desc: "メッセージを一定時間後に自動削除するように設定します。" },
  { name: "/auto-delete-disable", desc: "このチャンネルの自動削除設定を解除します。" },
  { name: "/auto-delete-next", desc: "次に削除されるメッセージを表示します。" },
  { name: "/image", desc: "説明文から画像を生成します。" },
  { name: "/image-detail", desc: "詳細を指定して画像を生成します。" },
  { name: "/poll", desc: "質問と選択肢を指定して投票を開始します。" },
  { name: "/poll-result", desc: "保存された投票の結果を集計します。" },
  { name: "/remind", desc: "指定した時刻にリマインドを設定します。" },
  { name: "/remind-list", desc: "あなたのリマインドを一覧表示します。" },
  { name: "/remind-remove", desc: "あなたのリマインドを全て削除します。" },
  { name: "/add-memory", desc: "つむぎちゃんに長期的に物事を覚えこませます。" },
  { name: "/remove-memory", desc: "つむぎちゃんの記憶を消去します。" },
  { name: "/show-memories", desc: "つむぎちゃんが覚えていること一覧を表示させる" },
  { name: "/add-trigger-reaction", desc: "特定の言葉にリアクションをつけさせます。" },
  { name: "/remove-trigger-reaction", desc: "特定の言葉からリアクションを解除します。" },
  { name: "/dice", desc: "サイコロを振ります。" },
  { name: "/oversea-register", desc: "当該チャンネルマルチサーバー用に登録します。" },
  { name: "/oversea-leave", desc: "当該チャンネルに登録されているマルチサーバーを解除します。" },
  { name: "/oversea-set-name", desc: "マルチサーバーで利用する専用の名前を設定します。" },
  { name: "/oversea-set-icon", desc: "マルチサーバーで利用する匿名アイコンを設定します。" },
  { name: "/who", desc: "サーバー全体で匿名化するキャラクターを選択します。" },
  { name: "/iam", desc: "サーバー全体の匿名化を解除します。" },
  { name: "/add-banned-word", desc: "禁止に該当するワードを登録します。" },
  { name: "/add-banned-words", desc: "カンマまたは改行区切りで禁止ワードを登録します。" },
  { name: "/remove-banned-word", desc: "登録されている禁止ワードを削除します。" },
  { name: "/remove-banned-words", desc: "カンマまたは改行区切りで禁止ワードを削除します。" },
  { name: "/export-banned-words", desc: "登録されている禁止ワードをCSV形式で出力します。" },
  { name: "/set-banned-text-mode", desc: "禁止テキストの処理モードを設定します。(hide/delete)" },
  { name: "/set-banned-text-enabled", desc: "禁止テキスト機能を有効/無効にします。" },
  { name: "/auto-message", desc: "AIで会話を促す自動メッセージを設定します。" },
  { name: "/show-auto-message", desc: "AIで会話を促す自動メッセージの現在の設定を表示します。" },
  { name: "/debug-auto-message", desc: "デバッグ用に自動メッセージを今すぐ送信します。" },
  { name: "/overwrite-auto-message", desc: "AIで会話を促す自動メッセージの設定を上書きします。" },
  { name: "/remove-auto-message", desc: "AIで会話を促す自動メッセージの設定を解除します。" },
];

export default function App() {
  return (
    <div className="min-h-screen bg-white text-gray-900">
      {/* Header */}
      <Header/>

    <section className="relative">
      <div className="mx-auto grid max-w-screen-xl items-center gap-8 px-4 py-12 md:grid-cols-2 md:gap-10 md:py-20">
        {/* 画像側（モバイルで先頭に表示 → order-1 md:order-2） */}
        <div className="mx-auto w-full max-w-xs order-1 md:order-2 md:max-w-sm">
          <img
            src={headerIcon}
            alt="TsDiscordBot Hero"
            className="h-full w-full rounded-3xl object-cover shadow-xl ring-1 ring-black/5"
          />
        </div>

        {/* テキスト側（モバイルでは後ろ → order-2 md:order-1） */}
        <div className="order-2 md:order-1 text-center md:text-left">
          <h1 className="text-2xl font-bold tracking-tight sm:text-3xl md:text-5xl">
            サーバーに遊びを、<br/>
            もっと自由に話せる Discord に
          </h1>
          <p className="mt-4 text-base text-gray-600 sm:text-lg md:mt-5 md:text-lg">
            いつものサーバーが、ちょっと特別な遊び場に変わります。<br className="hidden md:block" />
            キャラになりきって匿名で話したり、時間制限つきのチャンネルで秘密を共有したり。<br className="hidden md:block" />
            さらに AI 会話や画像生成まで、会話を盛り上げるユニークな機能を搭載しています。
          </p>

          <div className="mt-6 md:mt-8 flex flex-wrap justify-center md:justify-start gap-3">
            <a
              href="https://github.com/p4j4dyxcry/Discord.NET-bot"
              target="_blank"
              rel="noreferrer"
              className="rounded-xl border px-4 py-2 text-sm sm:px-5 sm:py-3 sm:text-base hover:bg-gray-50"
            >
              GitHubで見る
            </a>
          </div>
        </div>
      </div>
    </section>

      <div className="h-px bg-gray-200 mx-auto w-2/3 my-10" />

      <IntroSection/>

      {/* Features */}
      <section id="features" className="mx-auto max-w-screen-xl px-4 py-20">
        <h2 className="text-2xl font-bold text-center">つむぎのできること</h2>
        <p className="mt-2 text-center text-gray-600">
          つむぎには、ここで紹介する以外にもユニークな機能が盛りだくさん。サーバーの雰囲気や遊び方に合わせて、自由に活用できます。
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

      {/* Commands */}
      <section id="commands" className="mx-auto max-w-screen-xl px-4 py-20">
        <h2 className="text-2xl font-bold text-center">コマンド一覧</h2>
        <CommandsExpander>
        <div className="mt-10 grid gap-6 sm:grid-cols-2 md:grid-cols-3">
          {commands.map((c) => (
            <CommandItem key={c.name} name={c.name} desc={c.desc} />
          ))}
        </div>

        </CommandsExpander>
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

function CommandsExpander({ children, defaultOpen = false }) {
  const [open, setOpen] = useState(defaultOpen);
  const contentRef = useRef(null);

  // コンテンツ高さを測ってスムーズに展開
  const maxHeight = open && contentRef.current
    ? `${contentRef.current.scrollHeight}px`
    : "0px";

  return (
    <div className="mt-6 rounded-xl border bg-white/60 shadow-sm">
      <button
        onClick={() => setOpen((v) => !v)}
        className="flex w-full items-center justify-between px-4 py-3 text-left font-medium hover:bg-gray-50"
        aria-expanded={open}
        aria-controls="commands-panel"
      >
        <span>{open ? "閉じる" : "表示する"}</span>
        <svg
          className={`h-5 w-5 transform transition-transform duration-200 ${open ? "rotate-180" : ""}`}
          viewBox="0 0 24 24" fill="none"
        >
          <path d="M6 9l6 6 6-6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      </button>

      <div
        id="commands-panel"
        ref={contentRef}
        className="overflow-hidden transition-[max-height] duration-300"
        style={{ maxHeight }}
      >
        <div className="px-4 pb-4">
          {children}
        </div>
      </div>
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

function IntroSection() {
  return (
    <section id="introduction" className="mx-auto max-w-screen-xl px-4 py-0">
      <div className="mx-auto max-w-3xl text-center">
        <h2 className="text-3xl font-bold">
          つむぎの雰囲気、<br className="md:hidden" />
          まずは30秒で
        </h2>
      </div>


      <div className="mt-12 space-y-16"> 
        {/* space-y-16 でブロック間に大きめの余白 */}

        {/* ブロック1 */}
        <div className="mx-auto max-w-3xl text-center">
          <p className="mt-2 text-lg text-gray-600 leading-relaxed">
            なりきりチャットで匿名会話！
          </p>
          <AutoPlayVideo src="media/introduction01.webm" type="video/webm" />
        </div>

        <div className="h-px bg-gray-200 mx-auto w-2/3" />

        {/* ブロック2 */}
        <div className="mx-auto max-w-3xl text-center">
          <p className="mt-2 text-lg text-gray-600 leading-relaxed">
            画像生成で遊んでみよう！
          </p>
          <AutoPlayVideo src="media/introduction02.webm" type="video/webm" />
        </div>
      </div>
    </section>
  );
}
