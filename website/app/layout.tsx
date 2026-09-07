import type { Metadata } from 'next';
import './globals.css';
export const metadata: Metadata = {
  metadataBase: new URL('https://repo-command-console.satiny-moon-8601.chatgpt.site'),
  title: 'REPO Command Console — Spawn Items, Loot & Enemies',
  description: 'Watch REPO Command Console in real gameplay. Browse the complete spawn list with item previews, spawn weapons, valuables and enemies, and control your R.E.P.O. run.',
  alternates: { canonical: '/' },
  icons: { icon: '/mascot.png' },
};
export default function RootLayout({ children }: Readonly<{children: React.ReactNode}>) {
  return <html lang="en"><body>{children}</body></html>;
}
