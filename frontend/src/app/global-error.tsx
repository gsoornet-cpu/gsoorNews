"use client";

export default function GlobalError({ reset }: { error: Error & { digest?: string }; reset: () => void }) {
  return (
    <html lang="ar" dir="rtl">
      <body>
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
      </body>
    </html>
  );
}
