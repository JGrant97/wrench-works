import type { Metadata } from "next";
import { Toaster } from "react-hot-toast";
import { ThemeInit } from "@/components/theme-init";
import "./globals.css";

export const metadata: Metadata = {
  title: "Wrench Works",
  description: "Workshop management platform",
};

// Inline script prevents flash of wrong theme before React hydrates
const themeScript = `
  (function() {
    try {
      var t = localStorage.getItem('ww_theme') || 'system';
      var d = t === 'system'
        ? window.matchMedia('(prefers-color-scheme: dark)').matches
        : t === 'dark';
      if (d) document.documentElement.classList.add('dark');
    } catch(e) {}
  })();
`;

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" suppressHydrationWarning>
      <head>
        <script dangerouslySetInnerHTML={{ __html: themeScript }} />
      </head>
      <body>
        <ThemeInit />
        {children}
        <Toaster position="top-right" toastOptions={{ duration: 4000 }} />
      </body>
    </html>
  );
}
