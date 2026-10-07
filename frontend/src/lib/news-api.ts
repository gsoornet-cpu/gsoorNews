import { apiFetch, ApiError } from "./api-client";
import type { PagedResult, StorySummary } from "./stories-api";

// Mirrors Jusoor.Application.Editorial.Contracts.PublicNewsSummaryDto /
// PublicNewsDetailDto. The public news API returns ONLY articles a newsroom
// user has explicitly published (Slice 15) — drafts never appear here.
interface NewsSummaryDto {
  id: string;
  title: string;
  authorName: string | null;
  excerpt: string | null;
  publishedAtUtc: string;
  countryNameAr: string | null;
  countryNameEn: string | null;
  cityNameAr: string | null;
  cityNameEn: string | null;
  slug: string;
  presentationDesks: string[];
  imageUrl: string | null;
}

/** Mirrors PublicCorrectionDto — never carries the issuer (decision D4). */
export interface NewsCorrection {
  kind: "Correction" | "Notice" | "Clarification" | "EditorNote" | "Update";
  note: string;
  isMajor: boolean;
  issuedAtUtc: string;
}

export interface NewsDetail {
  id: string;
  title: string;
  authorName: string | null;
  summary: string | null;
  body: string;
  publishedAtUtc: string;
  /** Later of publication and last edit (spec §16 "last updated" trust signal). */
  updatedAtUtc: string;
  countryNameAr: string | null;
  countryNameEn: string | null;
  cityNameAr: string | null;
  cityNameEn: string | null;
  /** Append-only editorial notes, oldest first ("تنويه صحفي / تصحيح"). */
  corrections: NewsCorrection[];
  isArchived: boolean;
  archivedAtUtc: string | null;
  slug: string;
  seoTitle: string | null;
  seoDescription: string | null;
  canonicalUrl: string | null;
  socialTitle: string | null;
  socialDescription: string | null;
  socialImageUrl: string | null;
  noIndex: boolean;
  noFollow: boolean;
  primaryCategoryNameAr: string | null;
  secondaryTags: string[];
  presentationDesks: string[];
  twitterTitle: string | null;
  twitterDescription: string | null;
  twitterImageUrl: string | null;
}

/** Public video hub rows; only a published article's selected ready video is returned. */
export interface PublishedVideo {
  id: string;
  title: string;
  authorName: string | null;
  excerpt: string | null;
  videoUrl: string;
  caption: string | null;
  credit: string;
  publishedAtUtc: string;
  slug: string;
  thumbnailUrl: string | null;
}

/** The API's 410 response deliberately contains only these five fields. */
export interface NewsRetraction {
  id: string;
  title: string;
  publishedAtUtc: string;
  retractedAtUtc: string;
  notice: string;
}

export type PublicNewsDetail = { outcome: "found"; article: NewsDetail } | { outcome: "retracted"; notice: NewsRetraction } | null;

/**
 * Returns the feed in the StorySummary shape the existing homepage cards
 * already render, so the approved card components are reused unchanged.
 * Editorial articles have no third-party sources, hence sourceNames: [].
 */
export async function getPublishedNews(page = 1, pageSize = 20, desk?: string, searchTerm?: string): Promise<PagedResult<StorySummary>> {
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (desk) query.set("desk", desk);
  if (searchTerm?.trim()) query.set("q", searchTerm.trim());
  const result = await apiFetch<PagedResult<NewsSummaryDto>>(`/api/v1/news?${query.toString()}`);

  return {
    ...result,
    items: result.items.map((n) => ({ ...n, sourceNames: [] })),
  };
}

/** `homeOnly`: just the videos an editor placed in the homepage hero, in the editor's order. */
export async function getPublishedVideos(page = 1, pageSize = 18, homeOnly = false): Promise<PagedResult<PublishedVideo>> {
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (homeOnly) query.set("home", "true");
  return apiFetch<PagedResult<PublishedVideo>>(`/api/v1/news/videos?${query.toString()}`);
}

export async function getPublishedNewsById(id: string): Promise<PublicNewsDetail> {
  try {
    const article = await apiFetch<NewsDetail>(`/api/v1/news/${id}`);
    return { outcome: "found", article };
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) {
      return null;
    }
    if (error instanceof ApiError && error.status === 410 && error.data && typeof error.data === "object") {
      return { outcome: "retracted", notice: error.data as NewsRetraction };
    }

    throw error;
  }
}

// Mirrors Jusoor.Application.Editorial.Contracts.PublicNewsSitemapEntryDto.
export interface NewsSitemapEntry {
  id: string;
  title: string;
  publishedAtUtc: string;
  updatedAtUtc: string;
  isArchived: boolean;
  slug: string;
}

/** Every published article, newest first, as lean rows (Slice 18 — sitemaps). */
export async function getPublishedNewsSitemap(): Promise<NewsSitemapEntry[]> {
  return apiFetch<NewsSitemapEntry[]>("/api/v1/news/sitemap");
}
