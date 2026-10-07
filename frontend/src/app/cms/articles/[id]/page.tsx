import { ArticleEditor } from "@/components/cms/ArticleEditor";

export default async function CmsArticlePage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return <ArticleEditor articleId={id === "new" ? null : id} />;
}
