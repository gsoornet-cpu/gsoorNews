import { apiFetch, ApiError } from "./api-client";

// Mirrors Jusoor.Application.Stories.Contracts.StorySummaryDto exactly —
// keep these two in sync by hand until/unless a shared-schema generation
// step exists (out of scope for Slice 8; noting it rather than building
// speculative tooling for a two-endpoint API).
export interface StorySummary {
  id: string;
  title: string;
  excerpt: string | null;
  publishedAtUtc: string | null;
  countryNameAr: string | null;
  countryNameEn: string | null;
  cityNameAr: string | null;
  cityNameEn: string | null;
  sourceNames: string[];
  /** Editorial placement; empty for aggregated stories without newsroom placement. */
  presentationDesks?: string[];
  slug?: string;
  imageUrl?: string | null;
}

export interface StorySource {
  sourceName: string;
  canonicalUrl: string;
  publishedAtUtc: string | null;
}

// Mirrors StoryDetailDto.
export interface StoryDetail {
  id: string;
  title: string;
  content: string | null;
  publishedAtUtc: string | null;
  countryNameAr: string | null;
  countryNameEn: string | null;
  cityNameAr: string | null;
  cityNameEn: string | null;
  sources: StorySource[];
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export async function getHomepageStories(page = 1, pageSize = 20): Promise<PagedResult<StorySummary>> {
  return apiFetch<PagedResult<StorySummary>>(`/api/v1/stories?page=${page}&pageSize=${pageSize}`);
}

export async function getStoryById(id: string): Promise<StoryDetail | null> {
  try {
    return await apiFetch<StoryDetail>(`/api/v1/stories/${id}`);
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) {
      return null;
    }

    throw error;
  }
}
