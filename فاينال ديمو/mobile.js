/* ==========================================================================
   جسور — MOBILE UX ENHANCEMENTS
   ==========================================================================
   ملف إضافي محمّل بعد app.js على كل صفحة. لا يعدّل أي دالة موجودة في
   app.js ولا يكرر منطقها — فقط يضيف سلوكيات كانت ناقصة في تجربة اللمس:
   قفل تمرير الصفحة خلف الـ Drawer/المودال، إغلاق بـ ESC، إثراء روابط
   الـ Drawer لتطابق قائمة التنقل الكاملة (بدل نسخة مختصرة كانت تفقد بعض
   الأقسام)، ودعم السحب باللمس لمعرض "اللمّة الحلوة".
   ========================================================================== */

(function () {
  "use strict";

  /* ---------- 1) قفل تمرير الجسم خلف أي طبقة علوية مفتوحة ---------- */
  const lockers = new Set();
  function lockBodyScroll(id) {
    lockers.add(id);
    document.body.style.overflow = "hidden";
  }
  function unlockBodyScroll(id) {
    lockers.delete(id);
    if (lockers.size === 0) document.body.style.overflow = "";
  }

  function watchToggle(el, className, id) {
    if (!el) return;
    const obs = new MutationObserver(() => {
      if (el.classList.contains(className)) lockBodyScroll(id);
      else unlockBodyScroll(id);
    });
    obs.observe(el, { attributes: true, attributeFilter: ["class"] });
  }

  document.addEventListener("DOMContentLoaded", () => {
    watchToggle(document.querySelector(".drawer-overlay"), "open", "drawer");
    watchToggle(document.querySelector(".search-overlay"), "open", "search-overlay");
    document
      .querySelectorAll(".overlay")
      .forEach((el, i) => watchToggle(el, "open", "overlay-" + i));
    // نافذة البحث الفعلية المستخدمة في app.js تتفعّل بكلاس "is-active"
    // (مش "open")
    const searchPanel = document.getElementById("searchPanel");
    if (searchPanel) {
      const obs = new MutationObserver(() => {
        const visible = searchPanel.classList.contains("is-active");
        // يُفعَّل قفل التمرير فقط تحت 768px، لأن نسخة الديسكتوب نافذة
        // عائمة صغيرة في منتصف الشاشة ومش محتاجة قفل تمرير الصفحة كلها
        if (window.innerWidth <= 768 && visible) lockBodyScroll("search-panel");
        else unlockBodyScroll("search-panel");
      });
      obs.observe(searchPanel, { attributes: true, attributeFilter: ["class"] });
    }
  });

  /* ---------- 2) إغلاق أي طبقة مفتوحة بمفتاح ESC (سهولة وصول) ---------- */
  document.addEventListener("keydown", (e) => {
    if (e.key !== "Escape") return;
    document.querySelector(".drawer-overlay.open")?.classList.remove("open");
    document.querySelector(".search-overlay.open")?.classList.remove("open");
    document.querySelectorAll(".overlay.open").forEach((o) => o.classList.remove("open"));
    const searchPanel = document.getElementById("searchPanel");
    if (searchPanel?.classList.contains("open")) searchPanel.classList.remove("open");
  });

  /* ---------- 3) إغلاق الـ Drawer تلقائياً عند اختيار رابط ---------- */
  document.addEventListener("DOMContentLoaded", () => {
    const drawerOverlay = document.querySelector(".drawer-overlay");
    if (!drawerOverlay) return;
    drawerOverlay.querySelectorAll("nav a").forEach((a) => {
      a.addEventListener("click", () => drawerOverlay.classList.remove("open"));
    });
  });

  /* ---------- 3-ب) زرار همبرغر bluebarr — نفس دالة فتح الـ Drawer
     المستخدمة في app.js (initDrawer)، من غير ما نكرر أو نعدّل منطقها.
     ده الزرار اللي بيفضل متاح طول وقت التمرير على الموبايل لأن bluebarr
     هو الشريط الوحيد الثابت (Sticky) — الهيدر الأساسي بيتمرّر برّه
     الشاشة زي مواقع الأخبار الاحترافية ---------- */
  document.addEventListener("DOMContentLoaded", () => {
    const bluebarrBurger = document.getElementById("bluebarrBurger");
    const drawerOverlay = document.querySelector(".drawer-overlay");
    if (!bluebarrBurger || !drawerOverlay) return;
    bluebarrBurger.addEventListener("click", () => {
      drawerOverlay.classList.add("open");
    });
  });

  /* ---------- 4) إثراء الـ Drawer: نفس قائمة bluebarr كاملة + معلومات
     رؤساء التحرير (كانت تختفي من الهيدر على الموبايل) + السوشيال ميديا،
     بدل نسخة مختصرة كانت تفقد "خط أحمر" و"المزيد" وغيرها ---------- */
  document.addEventListener("DOMContentLoaded", () => {
    const drawerNav = document.querySelector(".drawer-overlay .drawer nav");
    const primaryLinks = document.querySelectorAll(".bluebarr .nav-links > li > a.nav-link");
    const moreLinks = document.querySelectorAll(".bluebarr .dropdown-menu a");
    if (drawerNav && (primaryLinks.length || moreLinks.length)) {
      const seen = new Set();
      const frag = document.createDocumentFragment();
      const addLink = (href, text) => {
        const key = href + "|" + text;
        if (seen.has(key)) return;
        seen.add(key);
        const a = document.createElement("a");
        a.href = href;
        a.textContent = text;
        frag.appendChild(a);
      };
      primaryLinks.forEach((a) => addLink(a.getAttribute("href"), a.textContent.trim()));
      if (moreLinks.length) {
        const title = document.createElement("div");
        title.className = "drawer-section-title";
        title.textContent = "أقسام إضافية";
        frag.appendChild(title);
        moreLinks.forEach((a) => addLink(a.getAttribute("href"), a.textContent.trim()));
      }
      const authGroup = document.querySelector(".bluebarr .auth-group");
      if (authGroup) {
        const title = document.createElement("div");
        title.className = "drawer-section-title";
        title.textContent = "الحساب";
        frag.appendChild(title);
        authGroup.querySelectorAll("a").forEach((a) => addLink(a.getAttribute("href"), a.textContent.trim()));
      }
      drawerNav.innerHTML = "";
      drawerNav.appendChild(frag);
      // إعادة تفعيل إغلاق الـ Drawer على الروابط المُنشأة ديناميكياً
      const drawerOverlay = document.querySelector(".drawer-overlay");
      drawerNav.querySelectorAll("a").forEach((a) => {
        a.addEventListener("click", () => drawerOverlay?.classList.remove("open"));
      });
    }

    // معلومات رؤساء التحرير — تُنقل نصياً لأعلى الـ Drawer بدل الاختفاء الكامل
    const credits = document.querySelector(".masthead-credits");
    const drawerPanel = document.querySelector(".drawer-overlay .drawer");
    if (credits && drawerPanel && !drawerPanel.querySelector(".drawer-credits")) {
      const box = document.createElement("div");
      box.className = "drawer-credits";
      box.innerHTML = credits.innerHTML;
      drawerPanel.appendChild(box);
    }

    // أيقونات التواصل الاجتماعي — تُضاف أسفل الـ Drawer (كانت تختفي فقط
    // من التوب بار على أضيق الشاشات، مش من الموقع كله)
    const socialSource = document.querySelector(".topbar .social-icons, .foot-v2-social");
    if (socialSource && drawerPanel && !drawerPanel.querySelector(".drawer-social")) {
      const box = document.createElement("div");
      box.className = "drawer-social social-icons";
      box.innerHTML = socialSource.innerHTML;
      drawerPanel.appendChild(box);
    }
  });

  /* ---------- 5) سحب باللمس لمعرض "اللمّة الحلوة" بدل الاعتماد فقط
     على أسهم يسار/يمين (مخفية على الموبايل في mobile.css) ---------- */
  document.addEventListener("DOMContentLoaded", () => {
    const carousel = document.querySelector(".lamma-carousel");
    const prevBtn = document.querySelector(".lamma-nav.prev, .lamma-nav .prev");
    const nextBtn = document.querySelector(".lamma-nav.next, .lamma-nav .next");
    if (!carousel) return;
    let startX = 0;
    let tracking = false;
    carousel.addEventListener("touchstart", (e) => {
      startX = e.touches[0].clientX;
      tracking = true;
    }, { passive: true });
    carousel.addEventListener("touchend", (e) => {
      if (!tracking) return;
      tracking = false;
      const dx = e.changedTouches[0].clientX - startX;
      if (Math.abs(dx) < 40) return;
      // RTL: سحب لليمين = التالي بصرياً في نفس منطق أزرار prev/next الحالية
      if (dx < 0) prevBtn?.click();
      else nextBtn?.click();
    }, { passive: true });
  });

  /* ---------- 6) منع تكبير Safari التلقائي عند التركيز على حقول البحث/
     المودالات بضبط حجم خط 16px فعلياً وقت الفوكس (دعم إضافي لأجهزة
     قديمة لا تلتزم بقاعدة الـ font-size في CSS بدقة) ---------- */
  document.addEventListener(
    "focusin",
    (e) => {
      if (window.innerWidth > 768) return;
      const el = e.target;
      if (el.matches('input[type="text"], input[type="tel"], input[type="email"], textarea, select')) {
        el.style.fontSize = "16px";
      }
    },
    true
  );

  /* ---------- 7) حقن مساحة الإعلان الموبايل + نسخة زرار دخول/إنشاء حساب
     جوه التوب بار — بالجافاسكريبت مركزيًا بدل تكرار نفس الماركب يدويًا
     في كل صفحة HTML على حدة (الموقع صفحات ثابتة من غير محرك قوالب،
     فده أضمن طريقة إن كل الصفحات تتحدّث بنفس الشكل بالظبط ومن غير
     أخطاء نسخ ولصق). الشكل والمقاسات معرّفين في mobile.css (قسم 1
     و1-ب)، هنا بس بنركّب العناصر في مكانها الصح في الـ DOM. ---------- */
  document.addEventListener("DOMContentLoaded", () => {
    const topbar = document.querySelector(".topbar");
    if (!topbar) return;

    /* 7-أ) مساحة الإعلان — تتحط فورًا بعد التوب بار وقبل الهيدر مباشرة */
    if (!document.querySelector(".mobile-ad-slot")) {
      const adSlot = document.createElement("div");
      adSlot.className = "mobile-ad-slot";
      adSlot.innerHTML =
        '<div class="mobile-ad-slot__inner">' +
          '<span class="mobile-ad-slot__tag">إعلان</span>' +
          '<div class="mobile-ad-slot__placeholder">' +
            "<span>مساحة إعلانية</span>" +
            "<small>ضع إعلانك هنا</small>" +
          "</div>" +
        "</div>";
      topbar.insertAdjacentElement("afterend", adSlot);
    }

    /* 7-ب) نسخة تانية من [data-auth-slot] جوه التوب بار — تظهر على
       الموبايل بس (CSS بيتكفل بإخفاء نسخة الهيدر وإظهار دي بدلها، شوف
       mobile.css قسم 1 و2). app.js's renderAuthSlot() بيملى أي عنصر
       بخاصية data-auth-slot تلقائيًا، فبنعيد تشغيلها هنا عشان تملى
       النسخة الجديدة دي كمان بنفس حالة تسجيل الدخول.

       ملحوظة مهمة: بنضيفها كـ "أخت" لـ .left جوه .wrap مباشرة، مش جوه
       .left نفسه — لأن .left بتحمل كلاس social-icons، وده عنده سيلكتور
       عام ".social-icons a" بيشتغل على أي رابط جواه (مصمم بس لأيقونات
       فيسبوك/إكس/انستجرام)، فلو حطينا الزرارين جوّاه هيتلخبطوا بنفس
       الشكل الدائري بتاع أيقونات السوشيال بالغلط. */
    const topbarWrap = topbar.querySelector(".wrap");
    const topbarLeft = topbar.querySelector(".left");
    if (topbarWrap && topbarLeft && !topbarWrap.querySelector("[data-auth-slot]")) {
      const authSlot = document.createElement("span");
      authSlot.setAttribute("data-auth-slot", "");
      authSlot.className = "topbar-auth-slot";
      topbarLeft.insertAdjacentElement("afterend", authSlot);

      if (typeof renderAuthSlot === "function") {
        renderAuthSlot();
      }
    }
  });
})();