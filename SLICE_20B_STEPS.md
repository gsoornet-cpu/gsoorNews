# Slice 20b - الدومين + مصفوفة الصلاحيات + الاختبارات

## الخطوات
1. فك الزيب فوق فولدر المشروع الرئيسي (اللي فيه backend/). الملفات هتستبدل بنفس المسارات.
2. من داخل فولدر backend:
   dotnet build
   dotnet test
3. بعد ما الاختبارات تنجح، ولّد الـ migration:
   dotnet ef migrations add AddEditorialRetractedArchived \
     --project src/Jusoor.Infrastructure --startup-project src/Jusoor.Api
   وراجع الملف المتولد: 5 أعمدة nullable + check constraint اسمه CK_EditorialArticles_RetractedHasNotice
4. طالما الـ migration ما اتعملش، تشغيل الـ API (Development) واختبارات الـ Integration هتفشل
   بسبب pending model changes. ده متوقع.

## الملفات (10)
معدّلة (9) + جديد (1: EditorialArticleRetractionArchiveTests.cs)
