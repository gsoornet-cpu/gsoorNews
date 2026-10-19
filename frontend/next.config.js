/** @type {import('next').NextConfig} */
const nextConfig = {
  output: "standalone",
  reactStrictMode: true,
  async headers() {
    const securityHeaders = [
      { key: "X-Content-Type-Options", value: "nosniff" },
      { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
      { key: "Strict-Transport-Security", value: "max-age=31536000" },
      { key: "X-Frame-Options", value: "SAMEORIGIN" },
      { key: "Permissions-Policy", value: "camera=(), microphone=()" },
    ];

    // Report-only gives us browser evidence before enforcing a policy that may
    // affect CMS embeds, user-uploaded media, fonts, or the configured API.
    if (process.env.NODE_ENV === "production") {
      securityHeaders.push({
        key: "Content-Security-Policy-Report-Only",
        value: [
          "default-src 'self'",
          "base-uri 'self'",
          "object-src 'none'",
          "frame-ancestors 'self'",
          "form-action 'self'",
          "script-src 'self' 'unsafe-inline'",
          "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com",
          "font-src 'self' data: https://fonts.gstatic.com",
          "img-src 'self' data: blob: https:",
          "media-src 'self' data: blob: https:",
          "connect-src 'self' https: wss:",
          "frame-src 'self' https://www.youtube-nocookie.com https://player.vimeo.com https://platform.twitter.com https://www.instagram.com https://www.facebook.com https://www.tiktok.com",
        ].join("; "),
      });
    }

    return [{ source: "/:path*", headers: securityHeaders }];
  },
};

module.exports = nextConfig;
