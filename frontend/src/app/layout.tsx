import type { Metadata, Viewport } from "next";
import "@/styles/site.css";
import "@/styles/site-mobile.css";
import "@/styles/site-overrides.css";
import { SiteHeader } from "@/components/SiteHeader";
import { SiteFooter } from "@/components/SiteFooter";

export const viewport: Viewport = { width: "device-width", initialScale: 1 };

export const metadata: Metadata = {
  title: "جسور — أخبار المصريين بالمهجر",
  description: "منصة المصريين بالمهجر: أخبار الجاليات، خدمة اطمّن على مغتربك وقت الأزمات، وخريطة تفاعلية لأماكن تواجد المصريين حول العالم.",
};

// dir="rtl" and lang="ar" are hardcoded for now — the spec's own
// recommendation is to launch Arabic-only in MVP and add English in V1.
// When that happens, this becomes a per-locale value from the routing
// segment, not a second hardcoded layout.
//
// Fonts (Cairo, IBM Plex Sans Arabic, IBM Plex Mono) are loaded via the
// same Google Fonts CDN link the demo uses, not next/font — the demo's
// styles.css references these family names directly, and next/font's
// local-hosting rewrite only applies to its own <link>, so keeping the
// exact CDN link is the simplest way to guarantee the same font actually
// renders without re-deriving every weight the CSS uses.
export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="ar" dir="rtl">
      <head>
        <link rel="icon" href="/assets/josour-brand.png" />
        <link rel="preconnect" href="https://fonts.googleapis.com" />
        <link rel="preconnect" href="https://fonts.gstatic.com" crossOrigin="" />
        {/* eslint-disable-next-line @next/next/no-page-custom-font -- this
            rule targets the pages/ router's per-page <Head>; App Router's
            root layout.tsx <head> is exactly where a site-wide font link
            belongs, so the warning is a false positive here. */}
        <link
          href="https://fonts.googleapis.com/css2?family=Cairo:wght@400;600;700;800;900&family=IBM+Plex+Sans+Arabic:wght@300;400;500;600;700&family=IBM+Plex+Mono:wght@400;500&display=swap"
          rel="stylesheet"
        />
      </head>
      <body>
        <SiteHeader />
        {children}
        <SiteFooter />
      </body>
    </html>
  );
}
