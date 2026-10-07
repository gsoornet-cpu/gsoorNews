# نشر جسور على Railway وإرسال لينك للعميل

المشروع 3 خدمات: **Postgres** + **API** (.NET، فولدر `backend`) + **Web** (Next.js، فولدر `frontend`).

## 0) قبل أي حاجة
- ملف `.env` اللي في الـ zip فيه أسرار حقيقية (مفتاح Supabase service-role، مفتاح JWT، باسورد الداتابيز). لو الـ zip اتبعت لأي حد: **غيّر مفتاح Supabase service-role** من لوحة Supabase.
- `.env` موجود في `.gitignore` فمش هيترفع على GitHub. اتأكد بـ `git status` إنه مش ظاهر.

## 1) ارفع الكود على GitHub
```bash
cd jusoor
git init && git add . && git commit -m "client trial"
git branch -M main
git remote add origin https://github.com/<you>/jusoor.git
git push -u origin main
```

## 2) Railway: أنشئ المشروع والخدمات
1. New Project → **Deploy PostgreSQL** (سيب اسمها `Postgres`).
2. New → GitHub Repo → اختار الريبو → سمّيها `api` → Settings → **Root Directory = `backend`**.
3. New → GitHub Repo (نفس الريبو) → سمّيها `web` → **Root Directory = `frontend`**.
(Railway هيلاقي الـ `Dockerfile` في كل فولدر ويبني بيه.)

## 3) متغيرات خدمة `api` (Variables)
| المتغير | القيمة |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ASPNETCORE_HTTP_PORTS` | `8080` |
| `Bootstrap__RunOnStartup` | `true` (يعمل migrations + Roles أول تشغيل) |
| `ConnectionStrings__Postgres` | `Host=${{Postgres.PGHOST}};Port=${{Postgres.PGPORT}};Database=${{Postgres.PGDATABASE}};Username=${{Postgres.PGUSER}};Password=${{Postgres.PGPASSWORD}}` |
| `Jwt__SigningKey` | نص عشوائي طويل: `openssl rand -base64 64` |
| `Jwt__Issuer` / `Jwt__Audience` | `jusoor-api` / `jusoor-clients` |
| `UseLocalFieldEncryption` | `true` |
| `FieldEncryption__LocalKey` | `openssl rand -base64 32` |
| `UseLocalOtp` | `true` |
| `SUPABASE_URL` / `SUPABASE_STORAGE_BUCKET` / `SUPABASE_SERVICE_ROLE_KEY` | من Supabase |
| `Cors__AllowedOrigins__0` | `https://<دومين web>` (تحطه بعد الخطوة 4) |
| `DevSeed__Email` / `DevSeed__Password` | حساب العميل (باسورد قوي: حرف كبير وصغير ورقم ورمز، 8+) |
| `DevSeed__Role` | `EditorInChief` (الوحيد اللي يقدر يكتب ويعتمد وينشر لوحده) |

**متعملش Generate Domain للـ api** — يفضل داخلي (خاص) والـ web بيكلمه من جوّه الشبكة.

## 4) متغيرات خدمة `web`
| المتغير | القيمة |
|---|---|
| `API_INTERNAL_BASE_URL` | `http://${{api.RAILWAY_PRIVATE_DOMAIN}}:8080` |
| `SITE_URL` | `https://<دومين web>` |

- متحطش `COOKIE_INSECURE` (ده للمحلي بس).
- web → Settings → Networking → **Generate Domain** → ده اللينك اللي هتبعته للعميل.
- ارجع حط الدومين ده في `Cors__AllowedOrigins__0` في خدمة `api` وSITE_URL.

## 5) Supabase (رفع الفيديو/الصور)
في الـ bucket: قراءة عامة (public read) + CORS يسمح بدومين `web` الجديد، وإلا رفع الميديا من الـ CMS هيفشل.

## 6) امسح أخبارك التجريبية (قبل ما تبعت اللينك)
- على الداتابيز الجديدة على Railway مفيش أخبار أصلاً (قاعدة فاضية).
- لو بتستخدم داتابيز قديمة فيها أخبار تجربة: افتح Postgres → Data/Query وشغّل `scripts/reset-trial-content.sql` (بيمسح الأخبار + سجل المراجعات + التصحيحات + سجلات الميديا، وبيسيب المستخدمين والصلاحيات). شغّل الاستعلام المعلّق في أوله لو عايز تمسح الملفات من Supabase Storage كمان.

## 7) تأكد إنه شغال
1. `https://<web>/` بيفتح بنفس شكل الفاينال ديمو (الأقسام الفاضية فيها رسالة «لا توجد مواد…»).
2. `https://<web>/cms/login` ادخل بحساب `DevSeed`.
3. اكتب خبر → اختار القسم (مثلاً «قصة نجاح») → قدّم للمراجعة → اعتمد → انشر → ظهوره في قسم قصص نجاح بالرئيسية.

## ربط الأخبار بالأقسام
كل قسم في الرئيسية بيعرض بس الأخبار **المنشورة** اللي اتحدد لها نفس القسم (Presentation Desk) في الـ CMS: `success`=قصص نجاح، `mughtarib`=أخبار المغتربين، `egypt`، `opportunities`، `red`، `official`، `articles`، `secondgen`، `lamma`، `events`، `arts`، `sports`، `various`.
خبر من غير قسم مش هيظهر في أي سكشن (بيظهر في الهيرو وأحدث الأخبار بس).
