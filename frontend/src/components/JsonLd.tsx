import { serializeJsonLd, type JsonLdObject } from "@/lib/seo";

/** Inlines a Schema.org JSON-LD block. Renders nothing when SITE_URL is not configured (data is null). */
export function JsonLd({ data }: { data: JsonLdObject | null }) {
  if (!data) {
    return null;
  }

  return <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: serializeJsonLd(data) }} />;
}
