export default function ArticleLoading() {
  return (
    <main id="main">
      <div className="article-head wrap skeleton-pulse" aria-hidden="true">
        <p className="article-subtitle">&nbsp;</p>
        <h1 className="article-title">&nbsp;</h1>
      </div>
      <figure className="article-hero-figure">
        <div className="article-hero-img wrap article-hero-placeholder skeleton-pulse" aria-hidden="true" />
      </figure>
      <div className="wrap">
        <article className="article-body skeleton-pulse" aria-hidden="true">
          <p>&nbsp;</p>
          <p>&nbsp;</p>
        </article>
      </div>
    </main>
  );
}
