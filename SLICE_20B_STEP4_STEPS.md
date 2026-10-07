# Slice 20b — الخطوة 4: الـ public queries + الـ sitemap + endpoint التفاصيل

## الخطوات
1. فك الزيب فوق فولدر المشروع الرئيسي (اللي فيه backend/ و docs/). الملفات هتستبدل بنفس المسارات.
2. **مفيش migration جديد في الخطوة دي** (مفيش تغيير في الـ schema). متعملش `dotnet ef migrations add`.
3. شغّل Docker Desktop (الـ 8 اختبارات الجديدة في الـ integration بتستخدم Testcontainers)، وبعدين من فولدر backend:
   ```
   dotnet build
   dotnet test
   ```
4. المتوقع: **992 اختبار، 0 فشل** (965 الحاليين + 27 جداد: 19 unit + 8 integration).
   لو العدد مختلف أو في فشل، الصق الـ output كامل (أول error في الـ build أو أسماء الاختبارات اللي فشلت).

## اللي اتغير (12 ملف: 8 معدّل + 2 جديد + 2 docs)
الكود:
- `Application/Editorial/Contracts/EditorialContracts.cs` — `PublicRetractionNoticeDto`، `PublicNewsDetailResult`، و`IsArchived`/`ArchivedAtUtc` على الـ detail DTO، و`IsArchived` على الـ sitemap DTO
- `Application/Editorial/EditorialQueries.cs` — الـ detail query بقت three-way، والـ sitemap بيشمل Archived بعلامة
- `Api/Controllers/NewsController.cs` — `GET /api/v1/news/{id}` بيرجّع 200 / **410** (+ `Cache-Control: no-store`) / 404

الاختبارات:
- جديد (2): `PublicNewsPostPublicationQueryTests.cs` (19) و `PublicNewsPostPublicationEndpointsTests.cs` (8)
- معدّل (تعديل ميكانيكي لنوع النتيجة الجديد): `EditorialHandlerTests`, `EditorialRichTextHandlerTests`, `EditorialRevisionAndCorrectionTests`, `EditorialWorkflowTests`, `PublishedNewsSeoTests`

Docs: `docs/project-progress.html`، و`docs/SLICE_20B_IMPLEMENTATION_PLAN.md` (سطر الـ Status بس).

## ملاحظات مهمة
- مفيش حاجة اتعملت build عندي (الـ sandbox مفيهوش .NET SDK)، فالأرقام اللي فوق عدّ مش نتيجة.
- متفعّلش endpoints الـ retract/archive للمحررين الحقيقيين (الخطوة 5) قبل الفرونت إند (الخطوة 7): الفرونت الحالي بيفهم 404 بس، و410 هيظهر عنده كصفحة error.
