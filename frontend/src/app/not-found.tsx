import Link from "next/link";

export default function NotFound() {
  return (
    <main id="main">
      <div className="wrap">
        <div className="state-panel">
          <h2>الصفحة غير موجودة</h2>
          <p>الخبر أو الصفحة التي تبحث عنها غير متاحة، وربما تمت إزالتها.</p>
          <p style={{ marginTop: 16 }}>
            <Link href="/" className="btn-nav btn-accent">العودة للرئيسية</Link>
          </p>
        </div>
      </div>
    </main>
  );
}
