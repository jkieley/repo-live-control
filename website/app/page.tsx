const install = 'https://thunderstore.io/c/repo/p/Coollectors/RepoCommandConsole/';
const videoUrl = 'https://www.youtube.com/watch?v=j_Qd60MG6VA';
const videoEmbed = 'https://www.youtube-nocookie.com/embed/j_Qd60MG6VA';
export default function Home() {
  return <main>
    <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify({
      '@context': 'https://schema.org', '@type': 'VideoObject',
      name: 'REPO Command Console 2.2 — Spawn Items, Loot & Enemies | Real Gameplay',
      description: 'Real R.E.P.O. gameplay showing the full spawn catalog, fuzzy search, compact item previews, spawning and holding equipment and loot, cosmetic cases, and spawning and removing an Apex Predator.',
      thumbnailUrl: 'https://repo-command-console.jkieley543940.chatgpt.site/gameplay-preview.jpg',
      uploadDate: '2026-09-07', duration: 'PT2M8.2S', embedUrl: videoEmbed,
      url: videoUrl,
    }) }} />
    <header><a className="brand" href="/"><img src="/mascot.png" alt="" width="48" height="48"/><span>REPO COMMAND CONSOLE</span></a><a className="install" href={install}>Get the mod <span aria-hidden="true">↗</span></a></header>
    <section className="intro"><p className="eyebrow">R.E.P.O. MOD · BY COOLLECTORS</p><h1>Your run.<br/><em>Your command.</em></h1><p className="lead">Spawn items, loot and enemies from one in-game console. Find what you need with fuzzy search, or scroll the complete catalog with compact item previews.</p><div className="actions"><a className="primary" href={install}>Install on Thunderstore ↗</a><a href="https://github.com/jkieley/repo-live-control">Source & command reference</a></div></section>
    <section className="walkthrough" aria-labelledby="watch"><div className="section-label"><h2 id="watch">See it in game</h2><span>F2 TO OPEN · TAB TO SELECT · ENTER TO RUN</span></div><div className="video-frame"><iframe src={videoEmbed} title="REPO Command Console 2.2 real gameplay walkthrough" loading="lazy" allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share" referrerPolicy="strict-origin-when-cross-origin" allowFullScreen /></div><p className="video-caption">Two minutes of real gameplay: browse, spawn, hold and clean up. Captions and chapters included. <a href={videoUrl}>Watch on YouTube ↗</a></p></section>
    <section className="features" aria-label="Console features"><article><span className="number">01 / FIND</span><h2>Browse everything</h2><p>Scroll the full list with your mouse or arrow keys. Small item previews keep eight results visible, and fuzzy search still takes you straight to a target.</p></article><article><span className="number">02 / SPAWN</span><h2>Bring it into the run</h2><p>Spawn weapons, upgrades, valuables and enemies. Search familiar names such as Pistol or Light Bridge, plus cosmetic cases, enemy souls and surplus bags.</p></article><article><span className="number">03 / CONTROL</span><h2>Keep the host in charge</h2><p>Heal, revive, summon players or return to the truck. In multiplayer, the host decides who can use commands and can grant or revoke access.</p></article></section>
    <section className="quickstart"><div><p className="eyebrow">READY WHEN YOU ARE</p><h2>Open. Find. Spawn.</h2></div><ol><li>Install with Thunderstore Mod Manager or r2modman.</li><li>Start R.E.P.O. modded and enter a run.</li><li>Press <kbd>F2</kbd>, type <code>/spawn</code>, then browse or search.</li><li>Use <kbd>Tab</kbd> to accept a target and <kbd>Enter</kbd> to run.</li></ol></section>
    <footer><span>REPO Command Console · Free and open source</span><a href={install}>Thunderstore</a><a href="https://github.com/jkieley/repo-live-control/issues">Report an issue</a></footer>
  </main>;
}
