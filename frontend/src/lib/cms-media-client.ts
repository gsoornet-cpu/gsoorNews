import { Upload } from "tus-js-client";
import { CmsError } from "@/lib/cms-client";

export interface CmsMediaAsset {
  id: string;
  fileName: string;
  kind: "Image" | "Video" | "Document";
  contentType: string;
  sizeBytes: number;
  altText: string;
  credit: string;
  caption: string | null;
  focalPointX: number | null;
  focalPointY: number | null;
  publicUrl: string;
  readyAtUtc: string;
}

interface MediaPage {
  items: CmsMediaAsset[];
  page: number;
  pageSize: number;
  totalCount: number;
}

interface SignedUpload {
  assetId: string;
  signedUploadUrl: string;
  uploadToken: string;
  resumableEndpoint: string;
  bucket: string;
  objectPath: string;
  contentType: string;
  expiresAtUtc: string;
}

export interface MediaMetadataInput {
  altText: string;
  credit: string;
  caption: string;
  focalPointX: number | null;
  focalPointY: number | null;
}

async function readMessage(response: Response): Promise<string> {
  const body = await response.json().catch(() => ({})) as {
    message?: string;
    errors?: Array<{ message?: string }>;
  };
  if (typeof body.message === "string" && body.message.trim()) return body.message;
  const validationMessages = body.errors?.map(error => error.message?.trim()).filter(Boolean);
  if (validationMessages?.length) return validationMessages.join(" • ");

  switch (response.status) {
    case 401: return "انتهت جلسة الدخول. سجّل الدخول إلى غرفة الأخبار من جديد.";
    case 403: return "حسابك لا يملك صلاحية رفع الوسائط.";
    case 404: return "مسار الوسائط غير متاح. حدّث واجهة الموقع وأعد تشغيل الخدمات.";
    case 413: return "حجم الملف أكبر من الحد الذي تسمح به خدمة الرفع.";
    case 502: return "تعذّر الاتصال بخدمة الموقع. تحقق من تشغيل الـ API.";
    case 503: return "خدمة تخزين الوسائط غير مهيأة. راجع إعداد Supabase على الخادم.";
    default: return "تعذّر إكمال رفع الوسائط. راجع اتصال الخدمة وحاول مرة أخرى.";
  }
}

async function postJson<T>(path: string, value?: unknown): Promise<T> {
  const response = await fetch(`/cms-api/media${path}`, {
    method: "POST",
    credentials: "same-origin",
    cache: "no-store",
    headers: { "Content-Type": "application/json" },
    ...(value === undefined ? {} : { body: JSON.stringify(value) }),
  });
  if (!response.ok) throw new CmsError(await readMessage(response), response.status);
  return await response.json() as T;
}

export const cmsMedia = {
  async list(): Promise<MediaPage> {
    const response = await fetch("/cms-api/media?page=1&pageSize=100", {
      credentials: "same-origin",
      cache: "no-store",
    });
    if (!response.ok) throw new CmsError(await readMessage(response), response.status);
    return await response.json() as MediaPage;
  },

  async upload(file: File, metadata: MediaMetadataInput, onProgress?: (percent: number) => void): Promise<CmsMediaAsset> {
    const signed = await postJson<SignedUpload>("/uploads", {
      fileName: file.name,
      contentType: file.type,
      sizeBytes: file.size,
      ...metadata,
      caption: metadata.caption.trim() || null,
    });

    if (file.type.startsWith("video/")) {
      try {
        await uploadResumable(file, signed, onProgress);
      } catch (error) {
        // Hosted Storage can reject the signed token only on the TUS session
        // endpoint. Use the signed URL PUT path for this specific failure;
        // that same path is already used successfully for images.
        if (!(error instanceof CmsError) || !error.message.includes("Invalid Compact JWS")) throw error;
        await uploadSignedPut(file, signed, onProgress);
      }
    } else {
      const response = await fetch(signed.signedUploadUrl, {
        method: "PUT",
        headers: { "Content-Type": file.type },
        body: file,
        cache: "no-store",
        credentials: "omit",
      });
      if (!response.ok) throw new CmsError("فشل رفع الملف إلى مخزن الوسائط.", response.status);
      onProgress?.(100);
    }

    return postJson<CmsMediaAsset>(`/${signed.assetId}/complete`);
  },
};

function uploadSignedPut(file: File, signed: SignedUpload, onProgress?: (percent: number) => void): Promise<void> {
  return new Promise((resolve, reject) => {
    const request = new XMLHttpRequest();
    request.open("PUT", signed.signedUploadUrl);
    request.withCredentials = false;
    request.setRequestHeader("Content-Type", signed.contentType);
    request.upload.onprogress = (event) => {
      if (event.lengthComputable) onProgress?.(Math.min(99, Math.floor((event.loaded / event.total) * 100)));
    };
    request.onload = () => {
      if (request.status >= 200 && request.status < 300) {
        onProgress?.(100);
        resolve();
      } else {
        reject(new CmsError("فشل رفع الفيديو إلى مخزن الوسائط.", request.status));
      }
    };
    request.onerror = () => reject(new CmsError("تعذّر الاتصال بمخزن الوسائط أثناء رفع الفيديو.", 502));
    request.onabort = () => reject(new CmsError("أُوقف رفع الفيديو قبل اكتماله.", 0));
    request.send(file);
  });
}

function uploadResumable(file: File, signed: SignedUpload, onProgress?: (percent: number) => void): Promise<void> {
  return new Promise((resolve, reject) => {
    const upload = new Upload(file, {
      endpoint: signed.resumableEndpoint,
      metadata: {
        bucketName: signed.bucket,
        objectName: signed.objectPath,
        contentType: signed.contentType,
      },
      headers: { "x-signature": signed.uploadToken },
      chunkSize: 6 * 1024 * 1024,
      retryDelays: [0, 1000, 3000, 5000, 10000],
      storeFingerprintForResuming: false,
      onProgress: (sent, total) => onProgress?.(Math.min(99, Math.floor((sent / total) * 100))),
      onError: (error) => reject(new CmsError(error.message || "انقطع رفع الفيديو.", 502)),
      onSuccess: () => {
        onProgress?.(100);
        resolve();
      },
    });

    upload.start();
  });
}
