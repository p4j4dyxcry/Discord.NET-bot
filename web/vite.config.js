import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  // ← リポ名に合わせる（ユーザー/Org Pagesでは '/' でOK。今回はプロジェクトページなので必須）
  base: '/Discord.NET-bot/',
  build: {
    // ← リポ直下の docs/ に出力（GitHub Pages で main/docs を公開）
    outDir: '../docs',
    // プロジェクト外でも古い出力を掃除したい場合は true（掃除されなくても動作自体は問題なし）
    emptyOutDir: true
  }
})
