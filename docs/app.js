function Nav() {
  return (
    <nav>
      <a href="#home">ホーム</a>
      <a href="#features">特徴</a>
      <a href="#howto">使い方</a>
      <a href="#contact">問い合わせ</a>
    </nav>
  );
}

function Section({ id, title, children }) {
  return (
    <section id={id}>
      <h2>{title}</h2>
      {children}
    </section>
  );
}

function App() {
  return (
    <>
      <header>
        <h1>Discord.NET Bot</h1>
        <Nav />
      </header>
      <main>
        <Section id="home" title="ようこそ！">
          <p>ここにワクワクする紹介文や画像を追加してください。</p>
        </Section>
        <Section id="features" title="特徴">
          <ul>
            <li>便利な機能1</li>
            <li>便利な機能2</li>
            <li>便利な機能3</li>
          </ul>
        </Section>
        <Section id="howto" title="使い方">
          <p>ステップバイステップのガイドをここに記載します。</p>
        </Section>
        <Section id="contact" title="問い合わせ">
          <p>ご質問やフィードバックは <a href="https://github.com">GitHub</a> まで。</p>
        </Section>
      </main>
      <footer>
        <p>&copy; 2024 Discord.NET Bot</p>
      </footer>
    </>
  );
}

const root = ReactDOM.createRoot(document.getElementById('root'));
root.render(<App />);
