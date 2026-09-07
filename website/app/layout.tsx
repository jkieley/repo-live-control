import type { Metadata } from 'next';
import './globals.css';
export const metadata: Metadata = {
  metadataBase: new URL('https://repo-command-console.jkieley543940.chatgpt.site'),
  title: 'REPO Command Console — Spawn Items, Loot & Enemies',
  description: 'Watch REPO Command Console in real gameplay. Browse the complete spawn list with item previews, spawn weapons, valuables and enemies, and control your R.E.P.O. run.',
  alternates: { canonical: '/' },
  icons: { icon: '/mascot.png' },
  openGraph: {
    type: 'website',
    url: '/',
    title: 'REPO Command Console — Spawn Items, Loot & Enemies',
    description: 'Real gameplay, a scrollable spawn catalog, compact item previews and host-controlled commands for R.E.P.O.',
    images: [{ url: '/gameplay-preview.jpg', width: 1920, height: 1080, alt: 'REPO Command Console in game' }],
  },
  twitter: { card: 'summary_large_image' },
};
export default function RootLayout({ children }: Readonly<{children: React.ReactNode}>) {
  return <html lang="en"><body>{children}</body></html>;
}
