import { useState } from "react";
import headerIcon from "./assets/hero.png";
import githubIcon from "./assets/github.svg";

function Header() {
  const [open, setOpen] = useState(false);

  return (
    <header className="sticky top-0 z-50 border-b bg-white/85 backdrop-blur supports-[backdrop-filter]:bg-white/60">
      <div className="mx-auto flex h-14 md:h-16 max-w-screen-xl items-center justify-between px-3 md:px-4">
        {/* ロゴ */}
        <a href={import.meta.env.BASE_URL} className="flex items-center gap-2 font-semibold">
          <img src={headerIcon} alt="TsDiscordBot Logo" className="h-7 w-7 rounded-full object-cover shrink-0" />
          {/* SPは短く / PCはフル表記 */}
          <span className="sm:hidden whitespace-nowrap">つむぎ</span>
          <span className="hidden sm:inline whitespace-nowrap">つむぎ (Discord Bot)</span>
        </a>

        {/* デスクトップナビ（←ここを hidden md:flex に変更） */}
        <nav className="hidden md:flex items-center gap-5">
          <a href="#features" className="text-sm text-gray-600 hover:text-gray-900 whitespace-nowrap">Features</a>
          <a href="#introduction" className="text-sm text-gray-600 hover:text-gray-900 whitespace-nowrap">Introduction</a>
          <a href="#commands" className="text-sm text-gray-600 hover:text-gray-900 whitespace-nowrap">Commands</a>
          <a
            href="https://github.com/p4j4dyxcry/Discord.NET-bot"
            target="_blank" rel="noreferrer"
            className="inline-flex items-center gap-2 rounded-lg border px-3 py-1.5 hover:bg-gray-50"
          >
            <img src={githubIcon} alt="" className="h-5 w-5" />
            <span className="text-sm">GitHub</span>
          </a>
        </nav>

        {/* モバイル：ハンバーガー（←新規追加 / md:hidden） */}
        <button
          type="button"
          className="md:hidden inline-flex items-center justify-center rounded-lg border px-3 py-2 text-sm shadow-sm active:scale-[0.98]"
          aria-label="Open menu"
          aria-expanded={open}
          aria-controls="mobile-menu"
          onClick={() => setOpen(v => !v)}
        >
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none">
            <path d="M3 6h18M3 12h18M3 18h18" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
          </svg>
        </button>
      </div>

      {/* モバイルメニュー（←新規追加） */}
      <div
        id="mobile-menu"
        className={`md:hidden overflow-hidden transition-[max-height] duration-300 ${open ? "max-h-48" : "max-h-0"}`}
      >
        <nav className="mx-auto max-w-screen-xl px-3 pb-3">
          <a href="#features" className="block rounded-lg px-3 py-2 text-gray-700 hover:bg-gray-50">Features</a>
          <a href="#introduction" className="block rounded-lg px-3 py-2 text-gray-700 hover:bg-gray-50">Introduction</a>
          <a href="#commands" className="block rounded-lg px-3 py-2 text-gray-700 hover:bg-gray-50">Commands</a>
          <a
            href="https://github.com/p4j4dyxcry/Discord.NET-bot"
            target="_blank" rel="noreferrer"
            className="block rounded-lg px-3 py-2 text-gray-700 hover:bg-gray-50"
          >
            GitHub
          </a>
        </nav>
      </div>
    </header>
  );
}

export default Header;
