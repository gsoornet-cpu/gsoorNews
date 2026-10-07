import ArticlePageById from "../page";

export { dynamic, generateMetadata } from "../page";

export default async function SluggedArticlePage({ params }: { params: Promise<{ id: string; slug: string }> }) {
  const resolved = await params;
  return ArticlePageById({ params: Promise.resolve(resolved) });
}
