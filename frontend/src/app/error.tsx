"use client";

import { useEffect } from "react";

export default function ErrorBoundary({ error, reset }: { error: Error & { digest?: string }; reset: () => void }) {
  useEffect(() => {
    // Real server-side logging doesn't exist yet (no telemetry/Sentry-style
    // sink wired up in Phase 1) — console.error is the honest placeholder:
    // visible in server logs, not pretending to be a real observability
    // pipeline.
    console.error(error);
  }, [error]);

  return (
    <main id="main">
      <div className="wrap">
        <div className="state-panel">
          <h2>حدث خطأ غير متوقع</h2>
          <p>نعتذر عن الإزعاج. حاول تحديث الصفحة، أو عد إلينا بعد قليل.</p>
          <p style={{ marginTop: 16 }}>
            <button type="button" className="btn-nav btn-accent" onClick={() => reset()}>
              إعادة المحاولة
            </button>
          </p>
        </div>
      </div>
    </main>
  );
}
