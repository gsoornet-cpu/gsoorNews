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
| `UseLocalFieldEncryption` | `false` |
| `FieldEncryption__KeyVaultUri` | `https://<اسم-vault>.vault.azure.net/` |
| `FieldEncryption__KeyEncryptionKeyName` | اسم مفتاح RSA في Key Vault |
| `AZURE_TENANT_ID` / `AZURE_CLIENT_ID` / `AZURE_CLIENT_SECRET` | بيانات هوية خدمة مخصصة؛ خزّنها كـ Railway secrets وامنحها دور `Key Vault Crypto User` على الـ vault |
| `UseLocalOtp` | `false` |
| `Twilio__AccountSid` / `Twilio__AuthToken` / `Twilio__VerifyServiceSid` | بيانات حساب Twilio Verify كـ Railway secrets |
| `SUPABASE_URL` / `SUPABASE_STORAGE_BUCKET` / `SUPABASE_SERVICE_ROLE_KEY` | من Supabase |
| `Cors__AllowedOrigins__0` | `https://<دومين web>` (تحطه بعد الخطوة 4) |
| `DevSeed__Email` / `DevSeed__Password` | حساب العميل (باسورد قوي: حرف كبير وصغير ورقم ورمز، 8+) |
| `DevSeed__Role` | `EditorInChief` (الوحيد اللي يقدر يكتب ويعتمد وينشر لوحده) |

**متعملش Generate Domain للـ api** — يفضل داخلي (خاص) والـ web بيكلمه من جوّه الشبكة.

> **أمان بيانات اطمّن:** في Production استخدم `UseLocalFieldEncryption=false` و`UseLocalOtp=false` مع Azure Key Vault وTwilio. الاستثناء الوحيد هو ملف معاينة العميل المعزول أدناه: `Staging`، قاعدة جديدة لا تحتوي بيانات شخصية حقيقية، مفتاح بيئي ثابت، وOTP معطل. إذا كانت قاعدة بيانات تحتوي حالات شُفرت بالمفتاح المحلي، لا تغيّر مزود التشفير مع الاحتفاظ بهذه البيانات قبل تنفيذ خطة نقل معتمدة؛ وإلا قد تتعذر قراءة الحقول. لا توصل وضع المعاينة بقاعدة قائمة أو إنتاجية.

### معاينة عميل مؤقتة على Railway بلا Azure أو Twilio

استخدم هذا الوضع فقط لعرض الـCMS على **قاعدة PostgreSQL جديدة ومعزولة ومخصصة للتجربة**. لا تعِد استخدام قاعدة إنتاج أو تنسخ إليها بيانات اطمّن الحقيقية. أنشئ خدمة Postgres منفصلة للمشروع التجريبي، ثم اضبط خدمة `api` بهذه المتغيرات:

| المتغير | القيمة |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Staging` |
| `ClientDemoMode` | `true` |
| `UseLocalFieldEncryption` | `true` |
| `UseLocalOtp` | `false` |
| `FieldEncryption__LocalKey` | مفتاح جديد: PowerShell: `[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))` (أو `openssl rand -base64 32`)؛ خزّنه في Railway Variables كـsecret واحتفظ بنسخة آمنة ثابتة حتى لا تفقد قراءة بيانات هذه القاعدة بعد إعادة النشر |
| `Bootstrap__RunOnStartup` | `true` لتجهيز مخطط قاعدة التجربة والأدوار عند أول تشغيل |
| `ConnectionStrings__Postgres` | اربطه بقاعدة التجربة الجديدة فقط |
| `DevSeed__Email` / `DevSeed__Password` | حساب CMS تجريبي بكلمة مرور قوية وفريدة |
| `Jwt__SigningKey` | مفتاح مستقل للتجربة، مولد بـ `openssl rand -base64 64` |

احتفظ بإعدادات Supabase وCORS وواجهة `web` من بقية الدليل حتى يعمل رفع الصور والفيديو. في وضع العرض، OTP الخاص بخدمة «اطمّن» **معطّل عمدًا** ويرجع 503؛ لا يطبع النظام رموز OTP في السجلات ولا يقبل طلبات حقيقية. لا تستخدم هذا الوضع لحفظ بيانات أشخاص حقيقية، واحذف قاعدة التجربة والمفاتيح بعد انتهاء العرض. عند الانتقال إلى Azure/Twilio ابدأ بقاعدة الإنتاج الجديدة أو نفّذ خطة نقل بيانات معتمدة؛ تشفير المفتاح المحلي غير متوافق تلقائيًا مع Key Vault.

هذا الوضع لا يحوّل الخدمة إلى Development ولا يفتح Swagger العام. وهو إعداد معاينة مؤقت، وليس إعداد إنتاج.
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
