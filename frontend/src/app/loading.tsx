export default function HomeLoading() {
  return (
    <main id="main">
      <section className="section tight">
        <div className="wrap">
          <div className="story-card skeleton-pulse" aria-hidden="true">
            <div className="thumb thumb-placeholder" />
            <div className="body">
              <h3>&nbsp;</h3>
            </div>
          </div>
        </div>
      </section>
      <section className="section tight on-surface">
        <div className="wrap">
          <div className="six-grid">
            {Array.from({ length: 6 }).map((_, i) => (
              <div key={i} className="grid-card skeleton-pulse" aria-hidden="true">
                <div className="thumb thumb-placeholder" />
                <h3>&nbsp;</h3>
                <div className="foot">&nbsp;</div>
              </div>
            ))}
          </div>
        </div>
      </section>
    </main>
  );
}
