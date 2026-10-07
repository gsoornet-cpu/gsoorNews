/* ==========================================================================
   جسور — shared frontend behaviors. Loaded after data.js on every page.
   ========================================================================== */

/* ---------- Toasts ---------- */
function toast(msg, kind = 'info') {
  let stack = document.querySelector('.toast-stack');

  if (!stack) {
    stack = document.createElement('div');
    stack.className = 'toast-stack';
    document.body.appendChild(stack);
  }

  const t = document.createElement('div');
  t.className = `toast toast-${kind}`;
  t.textContent = msg;
  stack.appendChild(t);

  setTimeout(() => {
    t.style.opacity = '0';
    t.style.transition = 'opacity .2s ease';

    setTimeout(() => t.remove(), 200);
  }, 2600);
}


/* ==========================================================
   TOPBAR — Arabic Date & Time
   Example:
   الاثنين 31، أغسطس، 2026 · 04:43:34 صباحًا
   ========================================================== */

function initTopbarClock() {
  const clock = document.querySelector("[data-clock]");

  if (!clock) return;

  const dateElement = clock.querySelector(".date-part");
  const timeElement = clock.querySelector(".time-part");

  if (!dateElement || !timeElement) return;

  const days = [
    "الأحد",
    "الاثنين",
    "الثلاثاء",
    "الأربعاء",
    "الخميس",
    "الجمعة",
    "السبت"
  ];

  const months = [
    "يناير",
    "فبراير",
    "مارس",
    "أبريل",
    "مايو",
    "يونيو",
    "يوليو",
    "أغسطس",
    "سبتمبر",
    "أكتوبر",
    "نوفمبر",
    "ديسمبر"
  ];

  function updateClock() {
    const now = new Date();

    // اليوم
    const dayName = days[now.getDay()];

    // التاريخ
    const dayNumber = String(now.getDate()).padStart(2, "0");
    const monthName = months[now.getMonth()];
    const year = now.getFullYear();

    // الوقت بنظام 12 ساعة
    let hours = now.getHours();
    const minutes = String(now.getMinutes()).padStart(2, "0");
    const seconds = String(now.getSeconds()).padStart(2, "0");

    const period = hours >= 12 ? "مساءً" : "صباحًا";

    hours = hours % 12;
    hours = hours || 12;

    hours = String(hours).padStart(2, "0");

    dateElement.textContent =
      `${dayName} ${dayNumber}، ${monthName}، ${year}`;

    timeElement.textContent =
      `${hours}:${minutes}:${seconds} ${period}`;
  }

  updateClock();

  setInterval(updateClock, 1000);
}

document.addEventListener("DOMContentLoaded", initTopbarClock);

/* ---------- Mobile drawer ---------- */
function initDrawer() {
  const burger = document.querySelector('.burger');
  const overlay = document.querySelector('.drawer-overlay');

  if (!burger || !overlay) return;

  const closeDrawer = () => {
    overlay.classList.remove('open');
  };

  burger.addEventListener('click', () => {
    overlay.classList.add('open');
  });

  overlay.addEventListener('click', e => {
    if (e.target === overlay) {
      closeDrawer();
    }
  });

  overlay
    .querySelector('.drawer-close')
    ?.addEventListener('click', closeDrawer);
}


/* ---------- Header search overlay & Spotlight Panel ---------- */
function initSearchOverlay() {
  const searchBtns = document.querySelectorAll('[data-open-search]');
  const searchPanel = document.getElementById('searchPanel');
  const searchBackdrop = document.getElementById('searchBackdrop');
  const searchInput = document.getElementById('searchInput');
  const searchResults = document.getElementById('searchResults');
  const clearSearchBtn = document.getElementById('clearSearch');

  function openSearch() {
    if (!searchPanel || !searchBackdrop) return;

    searchPanel.classList.add('is-active');
    searchBackdrop.classList.add('is-active');

    setTimeout(() => {
      searchInput?.focus();
    }, 50);
  }

  function closeSearch() {
    if (!searchPanel || !searchBackdrop) return;

    searchPanel.classList.remove('is-active');
    searchBackdrop.classList.remove('is-active');
  }

  searchBtns.forEach(btn => btn.addEventListener('click', openSearch));
  searchBackdrop?.addEventListener('click', closeSearch);

  // اختصارات الكيبورد (Ctrl + K / Cmd + K و Esc)
  document.addEventListener('keydown', e => {
    if (
      (e.metaKey || e.ctrlKey) &&
      e.key.toLowerCase() === 'k'
    ) {
      e.preventDefault();

      searchPanel?.classList.contains('is-active')
        ? closeSearch()
        : openSearch();
    }

    if (
      e.key === 'Escape' &&
      searchPanel?.classList.contains('is-active')
    ) {
      closeSearch();
    }
  });

  // البحث المباشر في البيانات (Live Search)
  searchInput?.addEventListener('input', () => {
    const q = searchInput.value.trim().toLowerCase();

    if (clearSearchBtn) {
      clearSearchBtn.hidden = !q;
    }

    if (!q) {
      if (searchResults) {
        searchResults.innerHTML = '';
      }

      return;
    }

    const articleData =
      typeof ARTICLES !== 'undefined' &&
      Array.isArray(ARTICLES)
        ? ARTICLES
        : [];

    const matches = articleData
      .filter(
        a =>
          a &&
          a.title &&
          a.title.toLowerCase().includes(q)
      )
      .slice(0, 5);

    if (!searchResults) return;

    if (matches.length > 0) {
      searchResults.innerHTML = matches
        .map(a => {
          const safeCat = escapeHTML(
            typeof getSectionLabelForArticle === 'function'
              ? getSectionLabelForArticle(a)
              : (a.cat || '')
          );
          const safeTitle = escapeHTML(a.title || '');
          const safeId = encodeURIComponent(
            String(a.id ?? '')
          );

          return `
            <div
              class="row"
              onclick="location.href='article.html?id=${safeId}'"
              style="
                cursor:pointer;
                padding:8px 12px;
                border-bottom:1px solid var(--line,#e2e8f0);
              "
            >
              في <b>${safeCat}</b> — ${safeTitle}
            </div>
          `;
        })
        .join('');
    } else {
      searchResults.innerHTML = `
        <div
          class="row"
          style="padding:8px 12px;"
        >
          لا نتائج مطابقة لـ «${escapeHTML(q)}» — جرّب كلمة مختلفة
        </div>
      `;
    }
  });

  clearSearchBtn?.addEventListener('click', () => {
    if (!searchInput) return;

    searchInput.value = '';
    searchInput.focus();

    if (searchResults) {
      searchResults.innerHTML = '';
    }

    if (clearSearchBtn) {
      clearSearchBtn.hidden = true;
    }
  });

  searchInput?.addEventListener('keydown', e => {
    if (
      e.key === 'Enter' &&
      searchInput.value.trim()
    ) {
      window.location.href =
        'search.html?q=' +
        encodeURIComponent(
          searchInput.value.trim()
        );
    }
  });
}


/* ---------- Escaping Utility for XSS Security ---------- */
function escapeHTML(str) {
  return String(str ?? '').replace(
    /[&<>'"]/g,
    tag =>
      ({
        '&': '&amp;',
        '<': '&lt;',
        '>': '&gt;',
        "'": '&#39;',
        '"': '&quot;'
      }[tag] || tag)
  );
}


/* ---------- Auth state ---------- */
const AUTH = {
  user: null
};

function renderAuthSlot() {
  // querySelectorAll بدل querySelector: ممكن يبقى فيه أكتر من نسخة
  // لعنصر [data-auth-slot] في نفس الصفحة (نسخة الهيدر + نسخة التوب
  // بار على الموبايل من mobile.js) وكل النسخ لازم تتملى بنفس الحالة.
  const slots = document.querySelectorAll('[data-auth-slot]');

  if (!slots.length) return;

  let html;

  if (AUTH.user) {
    const name = String(
      AUTH.user.name ?? ''
    );

    html = `
      <div
        class="avatar"
        title="${escapeHTML(name)}"
      >
        ${escapeHTML(name[0] || '')}
      </div>
    `;
  } else {
    html =
      `<a class="btn btn-ghost btn-sm" href="auth.html">دخول</a>` +
      `<a class="btn btn-primary btn-sm" href="auth.html?tab=signup">إنشاء حساب</a>`;
  }

  slots.forEach(slot => { slot.innerHTML = html; });
}


/* ---------- Central article routing ---------- */
/*
   Any article carrying a real gallery (2+ images)
   goes to lamma-article.html.

   Articles without a gallery continue to article.html.
*/
function articleHref(a) {
  const id = encodeURIComponent(
    String(
      a && a.id != null
        ? a.id
        : ''
    )
  );

  const hasGallery =
    a &&
    Array.isArray(a.gallery) &&
    a.gallery.length > 1;

  return hasGallery
    ? `lamma-article.html?id=${id}`
    : `article.html?id=${id}`;
}


/* ---------- Card renderers ---------- */

function cardStory(a) {
  if (!a) return '';

  return `
    <div
      class="story-card"
      onclick="location.href='${articleHref(a)}'"
    >
      <div class="thumb">
        

        <img
          loading="lazy"
          src="${escapeHTML(a.img || '')}"
          alt="${escapeHTML(a.title || '')}"
        >
      </div>

      <div class="body">
        <h3>
          ${escapeHTML(a.title || '')}
        </h3>

        <div class="foot">
          <span>
            ${escapeHTML(
              a.city || a.country || ''
            )}
          </span>

          <span>·</span>

          <span>
            ${escapeHTML(a.time || '')}
          </span>
        </div>
      </div>
    </div>
  `;
}

/* بطاقة "اللمة الحلوة" — عنوان ظاهر بالكامل تحت الصورة دايمًا (مفيش
   قص أو تغطية)، على نمط .recirc-feature: صورة بنسبة ثابتة ثم عنوان
   وتفاصيل في التدفق الطبيعي للصفحة، من غير أي كارت/خلفية حوالين النص. */
function cardLamma(a) {
  if (!a) return '';

  return `
    <a
      class="lamma-card"
      href="${articleHref(a)}"
      aria-label="${escapeHTML(a.title || '')}"
    >
      <div class="thumb">
        <img
          loading="lazy"
          src="${escapeHTML(a.img || '')}"
          alt=""
        >
      </div>

      <h4>${escapeHTML(a.title || '')}</h4>

      <div class="foot">
        <span>${escapeHTML(a.city || a.country || '')}</span>
        <span>·</span>
        <span>${escapeHTML(a.time || '')}</span>
      </div>
    </a>
  `;
}


function cardGrid(a) {
  if (!a) return '';

  return `
    <div
      class="grid-card"
      onclick="location.href='${articleHref(a)}'"
    >
      <div class="thumb">
        <img
          loading="lazy"
          src="${escapeHTML(a.img || '')}"
          alt="${escapeHTML(a.title || '')}"
        >
      </div>

      <h3>
        ${escapeHTML(a.title || '')}
      </h3>

      <div class="foot">
        <span>
          ${escapeHTML(
            a.city || a.country || ''
          )}
        </span>

        <span>·</span>

        <span>
          ${escapeHTML(a.time || '')}
        </span>
      </div>
    </div>
  `;
}


function cardRow(a) {
  if (!a) return '';

  return `
    <div
      class="row-card"
      onclick="location.href='${articleHref(a)}'"
    >
      <img
        loading="lazy"
        src="${escapeHTML(a.img || '')}"
        alt="${escapeHTML(a.title || '')}"
      >

      <div>
        <h4>
          ${escapeHTML(a.title || '')}
        </h4>

        <div class="foot">
          ${escapeHTML(
            a.city || a.country || ''
          )}
          ·
          ${escapeHTML(a.time || '')}
        </div>
      </div>
    </div>
  `;
}


function cardRank(a, i) {
  if (!a) return '';

  return `
    <div
      class="item ${i < 3 ? 'top' : ''}"
      onclick="location.href='${articleHref(a)}'"
      style="cursor:pointer"
    >
      <span class="rank">
        ${String(i + 1).padStart(2, '0')}
      </span>

      <div>
        <h5>
          ${escapeHTML(a.title || '')}
        </h5>
      </div>
    </div>
  `;
}


/* ---------- Opinion / مقالات رأي tiles ---------- */

function cardOpinionTile(a, isFeatured) {
  if (!a) return '';

  const au =
    typeof authorById === 'function'
      ? authorById(a.authorId)
      : null;

  const href = articleHref(a);

  return `
    <article
      class="opinion-tile${isFeatured ? ' featured' : ''}"
      data-reveal
    >
      <div class="opinion-tile-title">
        <a href="${href}">
          ${escapeHTML(a.title || '')}
        </a>
      </div>

      ${
        au
          ? `
            <a
              class="opinion-byline"
              href="author.html?slug=${encodeURIComponent(au.slug)}"
            >
              <img
                class="opinion-avatar"
                loading="lazy"
                src="${escapeHTML(au.avatar)}"
                alt="${escapeHTML(au.name)}"
              >

              <span>
                ${escapeHTML(au.name)}
              </span>
            </a>
          `
          : ''
      }
    </article>
  `;
}


/* ---------- Hero video slider ---------- */

function initHeroSlider(stories) {
  const main = document.getElementById('heroMain');
  const sideR = document.getElementById('heroSideR');
  const sideL = document.getElementById('heroSideL');
  const dotsWrap = document.getElementById('heroDots');
  const below = document.getElementById('heroBelow');

  if (
    !main ||
    !Array.isArray(stories) ||
    stories.length === 0
  ) {
    return;
  }

  let active = 0;
  let timer = null;

  function render() {
    const s = stories[active];

    const cleanId = s.id
      ? String(s.id).replace('h', '')
      : '';

    main.innerHTML = `
      <img
        src="${escapeHTML(s.img || '')}"
        alt="${escapeHTML(s.title || '')}"
      >

      ${
        s.duration
          ? `
            <span class="duration-badge duration-badge-lg">
              ${escapeHTML(s.duration)}
            </span>
          `
          : ''
      }

      <button
        class="playbtn"
        aria-label="تشغيل الفيديو"
      >
        ▶
      </button>
    `;

    main.onclick = () => {
      openVideoModal(s, {
        articleId: cleanId
      });
    };

    if (below) {
      below.innerHTML = `
        <span class="hero-below__content">
          <span class="hero-below__play" aria-hidden="true">▶</span>
          <span class="hero-below__copy">
            <span class="hero-below__eyebrow">تغطية مرئية</span>
            <h2>${escapeHTML(s.title || '')}</h2>
          </span>
        </span>
      `;

      below.onclick = () => {
        location.href = articleHref({
          ...s,
          id: cleanId
        });
      };
    }

    const others = stories.filter(
      (_, i) => i !== active
    );

    const rightThree = others.slice(0, 3);
    const leftThree = others.slice(3, 6);

    if (sideR) {
      sideR.innerHTML = rightThree
        .map(thumbHTML)
        .join('');
    }

    if (sideL) {
      sideL.innerHTML = leftThree
        .map(thumbHTML)
        .join('');
    }

    const sideThumbs = [
      ...(sideR?.children || []),
      ...(sideL?.children || [])
    ];

    sideThumbs.forEach(el => {
      el.addEventListener('click', () => {
        active = stories.findIndex(
          x =>
            String(x.id) ===
            el.dataset.id
        );

        resetTimer();
        render();
      });
    });

    if (dotsWrap) {
      dotsWrap.innerHTML = stories
        .map(
          (_, i) => `
            <button
              class="${i === active ? 'active' : ''}"
              data-i="${i}"
            >
              ${i + 1}
            </button>
          `
        )
        .join('');

      dotsWrap
        .querySelectorAll('button')
        .forEach(b => {
          b.addEventListener('click', () => {
            active = +b.dataset.i;
            resetTimer();
            render();
          });
        });
    }
  }

  function thumbHTML(s) {
    return `
      <div
        class="hero-thumb"
        data-id="${escapeHTML(String(s.id ?? ''))}"
      >
        <span class="play">▶</span>

        ${
          s.duration
            ? `
              <span class="duration-badge">
                ${escapeHTML(s.duration)}
              </span>
            `
            : ''
        }

        <img
          src="${escapeHTML(s.img || '')}"
          alt="${escapeHTML(s.title || '')}"
        >

        <div class="tag">
          ${escapeHTML(s.title || '')}
        </div>
      </div>
    `;
  }

  function nextSlide() {
    active =
      (active + 1) %
      stories.length;

    render();
  }

  function prevSlide() {
    active =
      (active - 1 + stories.length) %
      stories.length;

    render();
  }

  function resetTimer() {
    clearInterval(timer);
    timer = setInterval(
      nextSlide,
      7000
    );
  }

  document
    .getElementById('heroPrev')
    ?.addEventListener('click', () => {
      prevSlide();
      resetTimer();
    });

  document
    .getElementById('heroNext')
    ?.addEventListener('click', () => {
      nextSlide();
      resetTimer();
    });

  render();
  resetTimer();
}


/* ---------- Cinematic video player ---------- */

let videoModalEl = null;

function ensureVideoModal() {
  if (videoModalEl) {
    return videoModalEl;
  }

  const overlay =
    document.createElement('div');

  overlay.className =
    'video-overlay';

  overlay.id =
    'videoModalOverlay';

  overlay.innerHTML = `
    <div
      class="video-modal-box"
      role="dialog"
      aria-modal="true"
      aria-label="مشغّل الفيديو"
    >
      <div class="video-modal-head">
        <div class="video-modal-heading">
          <span

            data-video-tag
          ></span>

          <h3 data-video-title></h3>
        </div>

        <button
          class="video-modal-close"
          type="button"
          aria-label="إغلاق"
        >
          ✕
        </button>
      </div>

      <div class="video-modal-player">
        <div
          class="video-modal-loading"
          data-video-loading
        >
          <span class="spinner"></span>
        </div>

        <video
          data-video-el
          playsinline
          controls
          preload="none"
          controlsList="nodownload"
        ></video>
      </div>

      <div class="video-modal-foot">
        <span
          class="video-modal-meta"
          data-video-meta
        ></span>

        <a
          class="btn btn-ghost btn-sm"
          data-video-article
          href="#"
        >
          اقرأ التقرير كاملاً →
        </a>
      </div>
    </div>
  `;

  document.body.appendChild(overlay);

  const videoEl =
    overlay.querySelector(
      '[data-video-el]'
    );

  const loadingEl =
    overlay.querySelector(
      '[data-video-loading]'
    );

  function close() {
    overlay.classList.remove('open');

    document.body.classList.remove(
      'video-modal-locked'
    );

    videoEl.pause();
    videoEl.removeAttribute('src');
    videoEl.load();

    window.removeEventListener(
      'keydown',
      onKeydown
    );
  }

  function onKeydown(e) {
    if (e.key === 'Escape') {
      close();
    }
  }

  overlay.addEventListener(
    'click',
    e => {
      if (e.target === overlay) {
        close();
      }
    }
  );

  overlay
    .querySelector('.video-modal-close')
    .addEventListener(
      'click',
      close
    );

  videoEl.addEventListener(
    'waiting',
    () => {
      loadingEl.classList.add('show');
    }
  );

  videoEl.addEventListener(
    'canplay',
    () => {
      loadingEl.classList.remove('show');
    }
  );

  videoEl.addEventListener(
    'playing',
    () => {
      loadingEl.classList.remove('show');
    }
  );

  videoModalEl = {
    overlay,
    videoEl,
    loadingEl,
    close,
    onKeydown
  };

  return videoModalEl;
}


function openVideoModal(
  story,
  opts = {}
) {
  if (!story) return;

  const m =
    ensureVideoModal();

  const articleId =
    opts.articleId ??
    story.articleId ??
    String(
      story.id ?? ''
    ).replace('h', '');

  m.overlay.querySelector(
    '[data-video-tag]'
  ).textContent =
    story.cat ||
    story.tag ||
    'فيديو';

  m.overlay.querySelector(
    '[data-video-title]'
  ).textContent =
    story.title || '';

  m.overlay.querySelector(
    '[data-video-meta]'
  ).textContent =
    [
      story.time,
      story.duration
    ]
      .filter(Boolean)
      .join(' · ');

  const articleLink =
    m.overlay.querySelector(
      '[data-video-article]'
    );

  if (articleId) {
    let article = null;

    if (
      typeof articleById === 'function'
    ) {
      article =
        articleById(articleId);
    }

    if (!article && typeof ARTICLES !== 'undefined' && Array.isArray(ARTICLES)) {
      article =
        ARTICLES.find(
          a =>
            String(a?.id) ===
            String(articleId)
        );
    }

    articleLink.href =
      article
        ? articleHref(article)
        : 'article.html?id=' +
          encodeURIComponent(
            articleId
          );

    articleLink.style.display = '';
  } else {
    articleLink.style.display =
      'none';
  }

  m.loadingEl.classList.add('show');

  m.videoEl.poster =
    story.img || '';

  m.videoEl.src =
    story.video || '';

  m.overlay.classList.add(
    'open'
  );

  document.body.classList.add(
    'video-modal-locked'
  );

  window.addEventListener(
    'keydown',
    m.onKeydown
  );

  const playPromise =
    m.videoEl.play();

  if (
    playPromise &&
    playPromise.catch
  ) {
    playPromise.catch(() => {});
  }
}


/* ---------- Video cards ---------- */

function videoCardHTML(v) {
  if (!v) return '';

  return `
    <div
      class="video-card"
      data-video-id="${escapeHTML(String(v.id ?? ''))}"
    >
      <div class="thumb">
        <img
          loading="lazy"
          src="${escapeHTML(v.img || '')}"
          alt="${escapeHTML(v.title || '')}"
        >

        <span class="play">
          ▶
        </span>

        ${
          v.duration
            ? `
              <span class="duration-badge">
                ${escapeHTML(v.duration)}
              </span>
            `
            : ''
        }
      </div>

      <div class="video-card-body">
        <span class="eyebrow">
          ${escapeHTML(v.cat || '')}
        </span>

        <h3>
          ${escapeHTML(v.title || '')}
        </h3>

        <div class="foot">
          <span>
            ${escapeHTML(v.time || '')}
          </span>
        </div>
      </div>
    </div>
  `;
}


function wireVideoCards(root) {
  if (!root) return;

  root
    .querySelectorAll(
      '[data-video-id]'
    )
    .forEach(card => {
      card.addEventListener(
        'click',
        () => {
          const v =
            typeof videoById === 'function'
              ? videoById(
                  card.dataset.videoId
                )
              : null;

          if (v) {
            openVideoModal(v);
          }
        }
      );
    });
}


/* ---------- Homepage video rail — "أحدث الفيديوهات" ---------- */

function initVideoRail() {
  const rail =
    document.getElementById(
      'videoRail'
    );

  if (
    !rail ||
    typeof VIDEOS === 'undefined' ||
    !Array.isArray(VIDEOS)
  ) {
    return;
  }

  rail.innerHTML =
    VIDEOS
      .slice(0, 6)
      .map(videoCardHTML)
      .join('');

  wireVideoCards(rail);
}


/* ---------- Video hub page (videos.html) ---------- */

function initVideoHub(videos) {
  const featuredEl =
    document.getElementById(
      'videoFeatured'
    );

  const tabsEl =
    document.getElementById(
      'videoTabs'
    );

  const gridEl =
    document.getElementById(
      'videoGrid'
    );

  if (
    !gridEl ||
    !Array.isArray(videos) ||
    !videos.length
  ) {
    return;
  }

  function renderFeatured(v) {
    if (!featuredEl) return;

    featuredEl.innerHTML = `
      <div class="video-featured-media">
        <img
          src="${escapeHTML(v.img || '')}"
          alt="${escapeHTML(v.title || '')}"
        >

        <span class="duration-badge duration-badge-lg">
          ${escapeHTML(v.duration || '')}
        </span>

        <button
          class="playbtn"
          aria-label="تشغيل الفيديو"
        >
          ▶
        </button>
      </div>

      <div class="video-featured-body">
        <span class="eyebrow">
          ${escapeHTML(v.cat || '')}
        </span>

        <h2>
          ${escapeHTML(v.title || '')}
        </h2>
      </div>
    `;

    featuredEl.onclick = () =>
      openVideoModal(v);
  }

  function renderGrid(list) {
    gridEl.innerHTML =
      list.length
        ? list
            .map(videoCardHTML)
            .join('')
        : `
          <p
            class="empty-state"
            style="
              padding:40px 0;
              text-align:center;
              color:var(--text-mute);
            "
          >
            لا توجد فيديوهات في هذا القسم حالياً.
          </p>
        `;

    [
      ...gridEl.children
    ].forEach(el => {
      el.setAttribute(
        'data-reveal',
        ''
      );
    });

    gridEl.setAttribute(
      'data-reveal-group',
      ''
    );

    wireVideoCards(gridEl);

    if (
      typeof initScrollReveal ===
      'function'
    ) {
      initScrollReveal();
    }
  }

  renderFeatured(videos[0]);
  renderGrid(videos);

  if (
    tabsEl &&
    typeof VIDEO_CATEGORIES !==
      'undefined'
  ) {
    tabsEl.innerHTML =
      VIDEO_CATEGORIES
        .map(
          (c, i) => `
            <button
              class="${i === 0 ? 'active' : ''}"
              data-cat="${escapeHTML(c)}"
            >
              ${escapeHTML(c)}
            </button>
          `
        )
        .join('');

    tabsEl
      .querySelectorAll('button')
      .forEach(btn => {
        btn.addEventListener(
          'click',
          () => {
            tabsEl
              .querySelector(
                'button.active'
              )
              ?.classList.remove(
                'active'
              );

            btn.classList.add(
              'active'
            );

            const list =
              typeof videosByCategory ===
              'function'
                ? videosByCategory(
                    btn.dataset.cat
                  )
                : videos;

            renderGrid(list);
          }
        );
      });
  }
}


/* ---------- Atmaen Modal ---------- */

function initAtmaenModal() {
  const overlay =
    document.getElementById(
      'atmaenOverlay'
    );

  if (!overlay) return;

  const total = 4;
  let cur = 1;

  const progressEl =
    overlay.querySelector(
      '.progress'
    );

  if (
    progressEl &&
    progressEl.children.length === 0
  ) {
    for (
      let i = 0;
      i < total;
      i++
    ) {
      progressEl.appendChild(
        document.createElement(
          'span'
        )
      );
    }
  }

  function render() {
    if (progressEl) {
      [
        ...progressEl.children
      ].forEach((el, i) => {
        el.classList.toggle(
          'done',
          i < cur
        );
      });
    }

    overlay
      .querySelectorAll(
        '.step-view'
      )
      .forEach(v => {
        v.classList.toggle(
          'active',
          +v.dataset.step === cur
        );
      });

    const prevBtn =
      overlay.querySelector(
        '#atmaenPrev'
      );

    const nextBtn =
      overlay.querySelector(
        '#atmaenNext'
      );

    if (prevBtn) {
      prevBtn.style.visibility =
        cur === 1
          ? 'hidden'
          : 'visible';
    }

    if (nextBtn) {
      nextBtn.textContent =
        cur === total
          ? 'إغلاق'
          : cur === 3
            ? 'تحقق وإنشاء الحالة'
            : 'التالي';
    }
  }

  const open = () => {
    cur = 1;
    render();
    overlay.classList.add(
      'open'
    );
  };

  const close = () => {
    overlay.classList.remove(
      'open'
    );
  };

  overlay
    .querySelector(
      '#atmaenNext'
    )
    ?.addEventListener(
      'click',
      () => {
        if (cur < total) {
          cur++;
          render();
        } else {
          close();
        }
      }
    );

  overlay
    .querySelector(
      '#atmaenPrev'
    )
    ?.addEventListener(
      'click',
      () => {
        if (cur > 1) {
          cur--;
          render();
        }
      }
    );

  overlay.addEventListener(
    'click',
    e => {
      if (e.target === overlay) {
        close();
      }
    }
  );

  document
    .querySelectorAll(
      '[data-open-atmaen]'
    )
    .forEach(b => {
      b.addEventListener(
        'click',
        open
      );
    });

  overlay
    .querySelector(
      '[data-close]'
    )
    ?.addEventListener(
      'click',
      close
    );

  render();
}


/* ---------- Scroll-reveal ---------- */

function initScrollReveal() {
  const items =
    document.querySelectorAll(
      '[data-reveal]'
    );

  if (!items.length) return;

  if (
    !('IntersectionObserver' in window)
  ) {
    items.forEach(el =>
      el.classList.add(
        'is-visible'
      )
    );

    return;
  }

  const io =
    new IntersectionObserver(
      entries => {
        entries.forEach(
          entry => {
            if (
              entry.isIntersecting
            ) {
              entry.target.classList.add(
                'is-visible'
              );

              io.unobserve(
                entry.target
              );
            }
          }
        );
      },
      {
        threshold: 0.12,
        rootMargin:
          '0px 0px -40px 0px'
      }
    );

  items.forEach(el =>
    io.observe(el)
  );
}


/* ---------- Article reading-progress bar ---------- */

function initReadProgress() {
  const bar =
    document.getElementById(
      'readProgress'
    );

  const body =
    document.querySelector(
      '.article-body'
    );

  if (!bar || !body) return;

  function update() {
    const start =
      body.offsetTop;

    const total =
      body.offsetHeight;

    const scrolled =
      window.scrollY +
      window.innerHeight * 0.5 -
      start;

    const pct =
      Math.min(
        100,
        Math.max(
          0,
          (scrolled / total) * 100
        )
      );

    bar.style.width =
      pct + '%';
  }

  update();

  window.addEventListener(
    'scroll',
    update,
    {
      passive: true
    }
  );

  window.addEventListener(
    'resize',
    update
  );
}


/* ---------- Header Navigation & Scroll Behavior ---------- */

/* ==========================================================================
   وضع الأزمة (Crisis Mode)
   - initCrisisMode(): يُستدعى في كل صفحة. يقرأ الحالة من data.js
     (getCrisisState — تدمج القيمة الافتراضية مع أي تعديل من الـ CMS محفوظ
     في localStorage) ويضيف شريط التنبيه العلوي + يملأ بطاقة "مركز الأزمة"
     المصغّرة في الرئيسية إن وُجدت.
   - لا يظهر الشريط في صفحات تسجيل الدخول، لوحة التحكم، أو صفحة مركز الأزمة
     نفسها (لأنها هي التفاصيل الكاملة أصلاً).
   - الإغلاق يُخفي الشريط لبقية الجلسة الحالية فقط (sessionStorage) — نفس
     سلوك الوكالات الإخبارية: لا يعاود الإزعاج لحد ما يقفل المتصفح، لكنه
     يرجع يظهر في زيارة جديدة أو لو تغيّر عنوان الأزمة.
   ========================================================================== */
const CRISIS_BAR_HIDDEN_PAGES = ["auth.html", "cms.html", "crisis.html"];

function currentPageFile() {
  const file = location.pathname.split("/").pop();
  return file && file.length ? file : "index.html";
}

/* initCrisisMode() يُستدعى مرة واحدة عند تحميل كل صفحة: يقرأ الحالة
   ويطبّق المظهر الكامل (بدون أنيميشن الستارة — دي مخصوصة للتفعيل الحي
   من الزرار، مش لتحميل الصفحة العادي) عبر applyCrisisModeState(). */
function initCrisisMode() {
  if (typeof getCrisisState !== "function") return;
  applyCrisisModeState(getCrisisState(), { silent: true });
}

/* applyCrisisModeState() هي نقطة الحقيقة الوحيدة لمظهر وضع الأزمة —
   بتتنادى وقت تحميل الصفحة (silent) وبرضه وقت التفعيل/الإيقاف الحي من
   الزرار التجريبي (مع أنيميشن الستارة والـ ripple عبر body class). كل
   سكاشن الصفحة (topbar / header / nav / ticker / hero) بتاخد شكلها من
   كلاس body.crisis-mode-active المعرّف بالكامل في styles.css، فمفيش أي
   تكرار للألوان هنا في الجافاسكريبت. */
function applyCrisisModeState(state, opts) {
  opts = opts || {};
  const wasActive = document.body.classList.contains("crisis-mode-active");
  document.body.classList.toggle("crisis-mode-active", !!state.active);

  renderCrisisSpotlight(state);
  renderCrisisTickerBadge(state);
  reorderCrisisSpotlight(!!state.active, { silent: opts.silent });

  const hideBarOnThisPage = CRISIS_BAR_HIDDEN_PAGES.includes(currentPageFile());
  const dismissKey = "jusoor_crisis_dismissed:" + state.title;
  const dismissed = (() => {
    try { return sessionStorage.getItem(dismissKey) === "1"; } catch (e) { return false; }
  })();

  const existingBar = document.querySelector(".crisis-bar");

  if (state.active && !hideBarOnThisPage && !dismissed) {
    if (!existingBar) insertCrisisBar(state, dismissKey);
  } else if (existingBar) {
    removeCrisisBar(existingBar);
  }

  if (!opts.silent) triggerCrisisSweep();

  /* إنذار الأزمة (صوت + اهتزاز + وميض التاب) — نقطة التشغيل الوحيدة،
     شايفة applyCrisisModeState سواء جت من تحميل صفحة عادي أو تفعيل حي. */
  if (state.active) {
    handleCrisisAlarmTrigger(state, { justActivated: !wasActive, liveToggle: !opts.silent });
    if (document.hidden) startCrisisAttentionFlash(state);
  } else {
    stopCrisisAttentionFlash();
    stopCrisisAlarmRepeaters();
  }
}

/* reorderCrisisSpotlight() — الصفحة الرئيسية فقط (العنصرين موجودين في
   index.html بس). وقت تفعيل وضع الأزمة، سكشن "مركز الأزمة" بيتنقل
   ليطلع فوق سكشن فيديو الهيرو مباشرة (الأولوية للمعلومة العاجلة)، وبيرجع
   مكانه الطبيعي تلقائياً تحت الفيديو عند الإيقاف. الانتقال بيتم بكروس-
   فيد بسيط (فيد-أوت → إعادة ترتيب فعلية في الـ DOM → فيد-إن) بدل قفزة
   مفاجئة في تخطيط الصفحة؛ في تحميل الصفحة العادي (silent) بيتم الترتيب
   فوراً من غير أنيميشن لتفادي وميض عند أول رسم للصفحة. */
function reorderCrisisSpotlight(active, opts) {
  opts = opts || {};
  const heroSection = document.getElementById("heroSliderSection");
  const crisisSection = document.getElementById("crisisSpotlightSection");
  if (!heroSection || !crisisSection || !heroSection.parentNode) return;

  const isBeforeHero = crisisSection.nextElementSibling === heroSection;
  if (active === isBeforeHero) return;

  const wrap = heroSection.parentNode;
  const move = () => {
    if (active) wrap.insertBefore(crisisSection, heroSection);
    else wrap.insertBefore(crisisSection, heroSection.nextSibling);
  };

  const reduceMotion = window.matchMedia &&
    window.matchMedia("(prefers-reduced-motion: reduce)").matches;

  if (opts.silent || reduceMotion) { move(); return; }

  [heroSection, crisisSection].forEach(el => el.classList.add("crisis-reorder-fading"));
  setTimeout(() => {
    move();
    void wrap.offsetHeight; // فرض reflow قبل شيل الكلاس عشان الفيد-إن يشتغل صح
    [heroSection, crisisSection].forEach(el => el.classList.remove("crisis-reorder-fading"));
  }, 260);
}

function insertCrisisBar(state, dismissKey) {
  const bar = document.createElement("div");
  bar.className = "crisis-bar";
  bar.setAttribute("role", "alert");
  bar.innerHTML = `
    <div class="crisis-bar__inner">
      <span class="crisis-bar__badge">${crisisAlarmIconHTML(13)}<span class="crisis-bar__dot"></span> ${state.level || "عاجل"}</span>
      <span class="crisis-bar__text"><strong>${state.title}</strong> — ${state.updatedLabel || ""}</span>
      <a class="crisis-bar__cta" href="crisis.html">تفاصيل الأزمة الكاملة ←</a>
      <button class="crisis-bar__close" type="button" aria-label="إغلاق التنبيه">×</button>
    </div>`;

  document.body.prepend(bar);

  bar.querySelector(".crisis-bar__close").addEventListener("click", () => {
    try { sessionStorage.setItem(dismissKey, "1"); } catch (e) {}
    removeCrisisBar(bar);
  });
}

/* إزالة الشريط بأنيميشن خروج (slide-up) بدل الاختفاء الفجائي، سواء كان
   الإغلاق من زر × أو من إيقاف وضع الأزمة نفسه من لوحة العرض التجريبية. */
function removeCrisisBar(bar) {
  bar.classList.add("is-leaving");
  const done = () => bar.remove();
  bar.addEventListener("animationend", done, { once: true });
  setTimeout(done, 500); // fallback لو الأنيميشن اتلغت (prefers-reduced-motion)
}

function renderCrisisSpotlight(state) {
  const mount = document.getElementById("crisisSpotlight");
  if (!mount) return;
  if (!state.active) { mount.innerHTML = ""; mount.hidden = true; return; }

  mount.hidden = false;
  mount.innerHTML = `
    <div class="crisis-spotlight">
      <span class="crisis-spotlight__eyebrow">${crisisAlarmIconHTML(15)} مركز الأزمة — ${state.level || "عاجل"}</span>
      <h3 class="crisis-spotlight__title">${state.title}</h3>
      <p class="crisis-spotlight__summary">${state.summary || ""}</p>
      <div class="crisis-spotlight__actions">
        <a class="primary" href="atmaen.html">اطمّن على مغتربك الآن</a>
        <a class="ghost" href="crisis.html">تفاصيل الأزمة والتحديثات ←</a>
      </div>
    </div>`;
}

/* بادچ "مباشر" داخل الشريط الإخباري (.bluebar) — بيتحط جنب الـ ticker
   وقت تفعيل وضع الأزمة بس، ولو العنصر مش موجود في الصفحة دي (مثلاً صفحة
   من غير bluebar) الدالة بتتجاهل بأمان. */
function renderCrisisTickerBadge(state) {
  const bluebar = document.querySelector(".bluebar");
  if (!bluebar) return;
  let badge = document.getElementById("crisisTickerBadge");

  if (!state.active) {
    if (badge) badge.remove();
    return;
  }

  if (!badge) {
    badge = document.createElement("span");
    badge.id = "crisisTickerBadge";
    badge.className = "crisis-ticker-badge";
    badge.innerHTML = `<span class="dot"></span> مباشر`;
    // بيتحط قبل الـ ticker مباشرة (أول عنصر داخل .bluebar) عشان يبان
    // في بداية الشريط بالـ RTL؛ لو التركيب مختلف في صفحة معينة بيتحط
    // في الآخر كـ fallback آمن.
    const ticker = bluebar.querySelector(".ticker");
    if (ticker) bluebar.insertBefore(badge, ticker);
    else bluebar.appendChild(badge);
  }
}

/* ستارة الانتقال — خط أحمر بيمسح الشاشة من فوق لتحت لحظة التفعيل/الإيقاف
   الحي، بيتشال تلقائي بعد ما الأنيميشن يخلص. بيُتخطى تماماً لو المستخدم
   مفعّل "تقليل الحركة" لأن الـ keyframes بتتلغي في CSS، فهنا بنتأكد كمان
   من عدم تكديس أكتر من ستارة واحدة في نفس اللحظة. */
function triggerCrisisSweep() {
  const old = document.getElementById("crisisSweep");
  if (old) old.remove();

  const sweep = document.createElement("div");
  sweep.id = "crisisSweep";
  document.body.appendChild(sweep);

  const done = () => sweep.remove();
  sweep.addEventListener("animationend", done, { once: true });
  setTimeout(done, 1000);
}

/* ==========================================================================
   إنذار الأزمة (Crisis Alarm) — صوت + اهتزاز + وميض التاب/الأيقونة
   ---------------------------------------------------------------------
   الفكرة: لو حد فاتح المنصة في تاب وحصلت أزمة، لازم ياخد باله حتى لو
   مش واقف قدام الشاشة أو التاب مش في الفوكس. الطبقة دي بتغطي 4 حاجات:
     1) صفارة تنبيه احترافية مُولَّدة بـ Web Audio API (مفيش ملف صوت
        خارجي مطلوب، ومفيش أي طلب شبكة).
     2) اهتزاز حقيقي للجهاز (Vibration API) على الموبايل وقت التفعيل.
     3) نبضة بصرية خفيفة "بتتناوب" على الشريط العلوي (crisisVibrateBurst
        في styles.css) طول ما وضع الأزمة شغال — بديل بصري للاهتزاز على
        أجهزة الديسكتوب اللي مفيهاش Vibration API.
     4) وميض عنوان التاب + الأيقونة (favicon) لما التاب يبقى مش في
        الفوكس، بنفس منطق تنبيهات البريد/الشات الاحترافية.
   قواعد الأمان: المتصفحات بتمنع تشغيل صوت من غير user gesture. فلو
   الصوت اتمنع، بيظهر توست صغير "فعّل تنبيه الصوت" بدل ما نفشل بصمت.
   كل حاجة هنا بتتلف بأمان (try/catch) لو الـ API مش متاحة.
   ========================================================================== */

/* أيقونة جرس + حلقتين رادار حوالينه — بتتحط جنب أي نص "عاجل" في الموقع
   (شريط التنبيه، بطاقة مركز الأزمة، هيرو صفحة الأزمة). SVG واحد بدل
   إيموجي 🚨 عشان نتحكم في الأنيميشن بدقة وتبقى متسقة عبر الموقع. */
function crisisAlarmIconHTML(size) {
  size = size || 14;
  return (
    '<span class="crisis-alarm-icon" aria-hidden="true">' +
      '<span class="crisis-alarm-icon__ring"></span>' +
      '<span class="crisis-alarm-icon__ring crisis-alarm-icon__ring--delay"></span>' +
      '<svg class="crisis-alarm-icon__bell" width="' + size + '" height="' + size + '" viewBox="0 0 24 24" fill="currentColor">' +
        '<path d="M12 22c1.1 0 2-.9 2-2h-4c0 1.1.89 2 2 2zm6-6v-5c0-3.07-1.63-5.64-4.5-6.32V4c0-.83-.67-1.5-1.5-1.5S10.5 3.17 10.5 4v.68C7.64 5.36 6 7.92 6 11v5l-2 2v1h16v-1l-2-2z"/>' +
      '</svg>' +
    '</span>'
  );
}

let _crisisAudioCtx = null;

function getCrisisAudioContext() {
  try {
    if (!_crisisAudioCtx) {
      const AC = window.AudioContext || window.webkitAudioContext;
      if (!AC) return null;
      _crisisAudioCtx = new AC();
    }
    return _crisisAudioCtx;
  } catch (e) {
    return null;
  }
}

function getCrisisAlarmConfig() {
  return (typeof CRISIS_ALARM_CONFIG !== "undefined") ? CRISIS_ALARM_CONFIG : {};
}

/* بتحاول تشغّل ملف صوت حقيقي (mp3/ogg/wav) من المسار المحدد في
   CRISIS_ALARM_CONFIG. بترجع Promise بترفض لو مفيش رابط، أو الملف مش
   موجود (404)، أو المتصفح منع التشغيل — عشان اللي بيستخدمها يقدر يرجع
   لصفارة بديلة بدل ما يفشل بصمت. */
function playCrisisFileSound(url) {
  return new Promise((resolve, reject) => {
    if (!url) { reject(new Error("crisis-no-sound-url")); return; }
    try {
      const audio = new Audio(url);
      audio.volume = 0.85;
      audio.addEventListener("error", () => reject(new Error("crisis-sound-file-error")), { once: true });
      const p = audio.play();
      if (p && typeof p.then === "function") {
        p.then(() => resolve(true)).catch(reject);
      } else {
        resolve(true);
      }
    } catch (e) {
      reject(e);
    }
  });
}

/* صفارة تنبيه من نغمتين متبادلتين (شبيهة بإنذارات غرف الأخبار
   الاحترافية — مش صفارة إسعاف)، مُولَّدة بالكامل بـ Web Audio API.
   دي الصفارة الاحتياطية (fallback) اللي بتشتغل تلقائياً لو مفيش ملف
   صوت مرفوع في CRISIS_ALARM_CONFIG.alertSoundUrl أو فشل تحميله. */
function playCrisisSynthAlarm() {
  const ctx = getCrisisAudioContext();
  if (!ctx) return Promise.reject(new Error("crisis-audio-unavailable"));

  const resumePromise = ctx.state === "suspended" ? ctx.resume() : Promise.resolve();

  return resumePromise.then(() => {
    if (ctx.state !== "running") throw new Error("crisis-audio-blocked");

    const now = ctx.currentTime;
    const master = ctx.createGain();
    master.gain.value = 0.25;
    master.connect(ctx.destination);

    const beeps = [
      { freq: 880, start: 0.00, dur: 0.16 },
      { freq: 659, start: 0.22, dur: 0.16 },
      { freq: 880, start: 0.44, dur: 0.16 },
      { freq: 659, start: 0.66, dur: 0.26 },
    ];

    beeps.forEach((b) => {
      const osc = ctx.createOscillator();
      const g = ctx.createGain();
      osc.type = "sine";
      osc.frequency.value = b.freq;
      const t0 = now + b.start;
      const t1 = t0 + b.dur;
      g.gain.setValueAtTime(0.0001, t0);
      g.gain.exponentialRampToValueAtTime(1, t0 + 0.015);
      g.gain.exponentialRampToValueAtTime(0.0001, t1);
      osc.connect(g);
      g.connect(master);
      osc.start(t0);
      osc.stop(t1 + 0.02);
    });

    return true;
  });
}

/* نبضة صوتية قصيرة جداً (fallback) لو مفيش ملف "نبضة" مرفوع في
   CRISIS_ALARM_CONFIG.pulseSoundUrl — نغمة واحدة هادئة، مش صفارة كاملة. */
function playCrisisSynthPulse() {
  const ctx = getCrisisAudioContext();
  if (!ctx) return Promise.reject(new Error("crisis-audio-unavailable"));

  const resumePromise = ctx.state === "suspended" ? ctx.resume() : Promise.resolve();

  return resumePromise.then(() => {
    if (ctx.state !== "running") throw new Error("crisis-audio-blocked");

    const now = ctx.currentTime;
    const osc = ctx.createOscillator();
    const g = ctx.createGain();
    osc.type = "sine";
    osc.frequency.value = 520;
    g.gain.setValueAtTime(0.0001, now);
    g.gain.exponentialRampToValueAtTime(0.18, now + 0.012);
    g.gain.exponentialRampToValueAtTime(0.0001, now + 0.15);
    osc.connect(g);
    g.connect(ctx.destination);
    osc.start(now);
    osc.stop(now + 0.17);
    return true;
  });
}

/* نقطة الدخول العامة لصوت "الإنذار الرئيسي": بيجرّب ملف العميل الأول
   (CRISIS_ALARM_CONFIG.alertSoundUrl)، ولو فشل (مش موجود / اتمنع) بيرجع
   تلقائياً للصفارة المُولَّدة. */
function playCrisisAlertSound() {
  return playCrisisFileSound(getCrisisAlarmConfig().alertSoundUrl).catch(() => playCrisisSynthAlarm());
}

/* نفس المنطق لصوت "النبضة" الخفيف. */
function playCrisisPulseSound() {
  return playCrisisFileSound(getCrisisAlarmConfig().pulseSoundUrl).catch(() => playCrisisSynthPulse());
}

/* اهتزاز حقيقي للجهاز (موبايل بس — الـ API مش موجودة على iOS Safari
   وده طبيعي وبيتجاهل بأمان). نمط خفيف بيتناوب (اهتزاز - وقفة - اهتزاز)
   بدل نبضة واحدة طويلة، عشان يحس المستخدم إن فيه "إنذار" مش رسالة
   عادية، من غير ما يبقى مزعج. */
function triggerCrisisHaptics() {
  try {
    if (navigator.vibrate) {
      navigator.vibrate([90, 60, 90, 60, 140]);
    }
  } catch (e) { /* بعض المتصفحات بتمنع الاهتزاز خارج تفاعل مباشر — تجاهل آمن */ }
}

function crisisAlarmSessionKey(state) {
  return "jusoor_crisis_alarm_played:" + (state.title || "");
}

/* توست صغير في أسفل الشاشة بيظهر لو المتصفح منع تشغيل صوت الإنذار
   تلقائياً (سياسة الـ autoplay في كل المتصفحات الحديثة). بيدي المستخدم
   فرصة يفعّل الصوت بضغطة واحدة (user gesture) بدل ما نفشل بصمت —
   نفس المنطق اللي بتستخدمه تطبيقات الشات والبريد الاحترافية. */
function showCrisisSoundPrompt(state) {
  if (document.getElementById("crisisSoundPrompt")) return;

  const toast = document.createElement("div");
  toast.id = "crisisSoundPrompt";
  toast.className = "crisis-sound-toast";
  toast.setAttribute("role", "status");
  toast.innerHTML =
    '<span class="crisis-sound-toast__icon">' + crisisAlarmIconHTML(15) + '</span>' +
    '<span class="crisis-sound-toast__text">في تحديث أزمة عاجل — فعّل تنبيه الصوت عشان توصلك التنبيهات القادمة فوراً</span>' +
    '<button type="button" class="crisis-sound-toast__btn">تفعيل الصوت</button>' +
    '<button type="button" class="crisis-sound-toast__close" aria-label="إغلاق">×</button>';

  document.body.appendChild(toast);

  const dismiss = () => {
    toast.classList.add("is-leaving");
    setTimeout(() => toast.remove(), 400);
  };

  toast.querySelector(".crisis-sound-toast__btn").addEventListener("click", () => {
    playCrisisAlertSound()
      .then(() => { try { sessionStorage.setItem(crisisAlarmSessionKey(state), "1"); } catch (e) {} })
      .catch(() => {});
    dismiss();
  });
  toast.querySelector(".crisis-sound-toast__close").addEventListener("click", dismiss);

  setTimeout(dismiss, 12000);
}

/* ---------- تكرار الإنذار طول ما وضع الأزمة شغال ----------
   تايمر مستقل لصوت الإنذار الرئيسي (كل CRISIS_ALARM_CONFIG.alertRepeatSeconds)
   وتايمر مستقل لصوت/اهتزاز النبضة الخفيفة (كل pulseRepeatSeconds)، الاتنين
   بيتوقفوا فوراً عند إيقاف وضع الأزمة أو مغادرة الصفحة. لو أول محاولة
   تشغيل اتمنعت من المتصفح (سياسة الـ autoplay)، بيظهر توست "فعّل الصوت"
   مرة واحدة بس مش مع كل تكرار. */
let _crisisAlertRepeatTimer = null;
let _crisisPulseRepeatTimer = null;
let _crisisAlertRepeatCount = 0;
let _crisisSoundPromptShown = false;

function startCrisisAlarmRepeaters(state) {
  stopCrisisAlarmRepeaters();
  const cfg = getCrisisAlarmConfig();

  const alertSeconds = Math.max(3, Number(cfg.alertRepeatSeconds) || 20);
  const pulseSeconds = Math.max(2, Number(cfg.pulseRepeatSeconds) || 5);
  const maxRepeats = Number(cfg.alertMaxRepeats) || 0;

  _crisisAlertRepeatCount = 0;
  _crisisAlertRepeatTimer = setInterval(() => {
    if (maxRepeats && _crisisAlertRepeatCount >= maxRepeats) {
      clearInterval(_crisisAlertRepeatTimer);
      _crisisAlertRepeatTimer = null;
      return;
    }
    _crisisAlertRepeatCount++;
    triggerCrisisHaptics();
    playCrisisAlertSound().catch(() => {
      if (!_crisisSoundPromptShown) {
        _crisisSoundPromptShown = true;
        showCrisisSoundPrompt(state);
      }
    });
  }, alertSeconds * 1000);

  _crisisPulseRepeatTimer = setInterval(() => {
    playCrisisPulseSound().catch(() => {});
  }, pulseSeconds * 1000);
}

function stopCrisisAlarmRepeaters() {
  if (_crisisAlertRepeatTimer) { clearInterval(_crisisAlertRepeatTimer); _crisisAlertRepeatTimer = null; }
  if (_crisisPulseRepeatTimer) { clearInterval(_crisisPulseRepeatTimer); _crisisPulseRepeatTimer = null; }
  _crisisSoundPromptShown = false;
}

/* نقطة القرار: إمتى نشغّل الصوت والاهتزاز فعلياً.
   - تفعيل حي من الزرار التجريبي (liveToggle=true): مضمون إن فيه user
     gesture، فالصوت بيشتغل في كل مرة يتفعّل فيها الوضع — مهم وقت العرض
     على العميل عشان يسمع الإنذار كل مرة يجرّب الزرار.
   - تحميل صفحة عادي والأزمة شغالة أصلاً (silent load): بيتشغّل مرة
     واحدة بس لكل تاب/أزمة (عبر sessionStorage) عشان ميبقاش مزعج مع كل
     تنقل بين صفحات الموقع. */
function handleCrisisAlarmTrigger(state, ctx) {
  triggerCrisisHaptics();

  if (ctx.liveToggle) {
    playCrisisAlertSound().catch(() => showCrisisSoundPrompt(state));
    startCrisisAlarmRepeaters(state);
    return;
  }

  startCrisisAlarmRepeaters(state);

  if (!ctx.justActivated) return;

  let alreadyPlayed = false;
  try { alreadyPlayed = sessionStorage.getItem(crisisAlarmSessionKey(state)) === "1"; } catch (e) {}
  if (alreadyPlayed) return;

  playCrisisAlertSound()
    .then(() => { try { sessionStorage.setItem(crisisAlarmSessionKey(state), "1"); } catch (e) {} })
    .catch(() => showCrisisSoundPrompt(state));
}

/* ---------- وميض عنوان التاب + الأيقونة (favicon) لما التاب مش في الفوكس ---------- */
let _crisisFavicon = { normalHref: null, altHref: null, ready: false };
let _crisisAttentionTimer = null;
let _crisisOriginalTitle = null;

/* بترسم نسخة من اللوجو الحالي وفوقها نقطة حمراء صغيرة على كانفاس محلي
   (من غير أي طلب شبكة) عشان تستخدم كـ favicon بديل وقت الوميض. */
function prepareCrisisFavicon() {
  if (_crisisFavicon.ready || _crisisFavicon.img) return;
  const link = document.querySelector('link[rel="icon"]');
  if (!link) return;
  _crisisFavicon.normalHref = link.href;

  const img = new Image();
  _crisisFavicon.img = img;
  img.onload = () => {
    try {
      const size = 64;
      const canvas = document.createElement("canvas");
      canvas.width = size;
      canvas.height = size;
      const c = canvas.getContext("2d");
      c.drawImage(img, 0, 0, size, size);
      c.beginPath();
      c.arc(size - 13, 13, 12, 0, Math.PI * 2);
      c.fillStyle = "#c6402f";
      c.fill();
      c.lineWidth = 3;
      c.strokeStyle = "#ffffff";
      c.stroke();
      _crisisFavicon.altHref = canvas.toDataURL("image/png");
      _crisisFavicon.ready = true;
    } catch (e) { /* بعض إعدادات المتصفح بتمنع قراءة الكانفاس — تجاهل آمن، الوميض هيفضل شغال على العنوان بس */ }
  };
  img.src = link.href;
}

function startCrisisAttentionFlash(state) {
  if (_crisisAttentionTimer) return;
  prepareCrisisFavicon();
  _crisisOriginalTitle = document.title;
  const alertTitle = "🚨 تحديث عاجل — " + (state.title || "جسور");
  const link = document.querySelector('link[rel="icon"]');
  let on = false;

  _crisisAttentionTimer = setInterval(() => {
    on = !on;
    document.title = on ? alertTitle : _crisisOriginalTitle;
    if (link && _crisisFavicon.ready) {
      link.href = on ? _crisisFavicon.altHref : _crisisFavicon.normalHref;
    }
  }, 1000);
}

function stopCrisisAttentionFlash() {
  if (_crisisAttentionTimer) {
    clearInterval(_crisisAttentionTimer);
    _crisisAttentionTimer = null;
  }
  if (_crisisOriginalTitle) {
    document.title = _crisisOriginalTitle;
    _crisisOriginalTitle = null;
  }
  const link = document.querySelector('link[rel="icon"]');
  if (link && _crisisFavicon.normalHref) link.href = _crisisFavicon.normalHref;
}

/* بيتابع تغيير فوكس التاب طول عمر الصفحة: يوقف الوميض لما المستخدم
   يرجع للتاب، ويبدأه لو سابه وهو مفتوح والأزمة شغالة. */
function initCrisisAttentionWatcher() {
  document.addEventListener("visibilitychange", () => {
    if (typeof getCrisisState !== "function") return;
    const state = getCrisisState();
    if (!state.active) return;
    if (document.hidden) startCrisisAttentionFlash(state);
    else stopCrisisAttentionFlash();
  });
}

/* ---------- زر تفعيل/إيقاف تجريبي (index.html فقط، للعروض على العميل) ----------
   TEMP DEMO ONLY — مربوط بعنصر data-crisis-demo-toggle. لو الزرار اتشال من
   index.html الدالة دي مش بتعمل حاجة تلقائياً؛ احذف هذا البلوك بالكامل مع
   حذف الزرار من index.html بعد انتهاء العروض التقديمية.
   ملحوظة: التفعيل هنا حي بالكامل (بدون location.reload) — التحوّل البصري
   يشتغل فوراً بالأنيميشن المعرّف في CSS بدل ما المستخدم يستنى تحميل صفحة
   جديدة، بالظبط زي أي CMS حقيقي بيبعت تحديث لحظي للواجهة. */
function initCrisisDemoToggle() {
  const btn = document.querySelector("[data-crisis-demo-toggle]");
  if (!btn || typeof getCrisisState !== "function") return;

  const sync = () => {
    const active = getCrisisState().active;
    btn.textContent = active ? "⛔ إيقاف وضع الأزمة (تجريبي)" : "🚨 تفعيل وضع الأزمة (تجريبي)";
  };

  btn.addEventListener("click", () => {
    const next = saveCrisisState({ active: !getCrisisState().active });
    applyCrisisModeState(next, { silent: false });
    sync();
  });

  sync();
}

/* ---------- Footer newsletter form (all pages, unified footer) ---------- */
function initFooterNewsletter() {
  const form = document.querySelector('[data-newsletter-form]');
  if (!form) return;
  form.addEventListener('submit', e => {
    e.preventDefault();
    const input = form.querySelector('input[type="email"]');
    if (input && input.value.trim()) {
      toast('تم تسجيل اشتراكك في نشرة جسور');
      form.reset();
    }
  });
}

function initSharedNavigation() {
  const mainHeader =
    document.querySelector(
      'header.site'
    );

  const mainNav =
    document.querySelector(
      '.bluebarr'
    );

  if (!mainNav) return;

  if (
    mainHeader &&
    'IntersectionObserver' in window
  ) {
    const observer =
      new IntersectionObserver(
        entries => {
          entries.forEach(
            entry => {
              mainNav.classList.toggle(
                'is-scrolled',
                !entry.isIntersecting
              );
            }
          );
        },
        {
          threshold: 0
        }
      );

    observer.observe(
      mainHeader
    );
  } else {
    window.addEventListener(
      'scroll',
      () => {
        mainNav.classList.toggle(
          'is-scrolled',
          window.scrollY > 80
        );
      },
      {
        passive: true
      }
    );
  }
}


/* ---------- Poll Handler ---------- */

function initPollForm() {
  const pollForm =
    document.getElementById(
      'pollForm'
    );

  pollForm?.addEventListener(
    'submit',
    function (e) {
      e.preventDefault();

      const selected =
        this.querySelector(
          'input[name="poll"]:checked'
        );

      if (!selected) {
        toast(
          'برجاء اختيار إجابة أولاً',
          'warning'
        );

        return;
      }

      this.classList.add(
        'voted'
      );

      toast(
        'تم تسجيل صوتك بنجاح ✅',
        'success'
      );
    }
  );
}


/* ---------- Services Slider Drag & Navigation ---------- */

function initSeniorServicesSlider() {
  const row =
    document.getElementById(
      'svcRow'
    );

  const prevBtn =
    document.getElementById(
      'svcPrev'
    );

  const nextBtn =
    document.getElementById(
      'svcNext'
    );

  if (!row) return;

  const scrollStep = 320;

  prevBtn?.addEventListener(
    'click',
    () => {
      row.scrollBy({
        left: -scrollStep,
        behavior: 'smooth'
      });
    }
  );

  nextBtn?.addEventListener(
    'click',
    () => {
      row.scrollBy({
        left: scrollStep,
        behavior: 'smooth'
      });
    }
  );

  let isDragging = false;
  let startX = 0;
  let startScroll = 0;
  let draggedDistance = 0;

  row.addEventListener(
    'pointerdown',
    e => {
      isDragging = true;
      draggedDistance = 0;
      startX = e.clientX;
      startScroll = row.scrollLeft;

      row.setPointerCapture(
        e.pointerId
      );

      row.style.cursor =
        'grabbing';

      row.style.scrollBehavior =
        'auto';
    }
  );

  row.addEventListener(
    'pointermove',
    e => {
      if (!isDragging) return;

      const distance =
        e.clientX - startX;

      draggedDistance =
        Math.abs(distance);

      row.scrollLeft =
        startScroll -
        distance * 1.5;
    }
  );

  row.addEventListener(
    'pointerup',
    e => {
      isDragging = false;

      if (
        row.hasPointerCapture(
          e.pointerId
        )
      ) {
        row.releasePointerCapture(
          e.pointerId
        );
      }

      row.style.cursor = 'grab';

      row.style.scrollBehavior =
        'smooth';
    }
  );

  row.addEventListener(
    'pointercancel',
    () => {
      isDragging = false;

      row.style.cursor = 'grab';

      row.style.scrollBehavior =
        'smooth';
    }
  );

  row
    .querySelectorAll('a')
    .forEach(link => {
      link.addEventListener(
        'click',
        e => {
          if (
            draggedDistance > 10
          ) {
            e.preventDefault();
          }
        }
      );
    });
}


/* ---------- لمّة الحلوة gallery carousel ---------- */

function initLammaCarousel(
  images,
  opts
) {
  const root =
    document.getElementById(
      'lammaCarousel'
    );

  if (
    !root ||
    !Array.isArray(images) ||
    !images.length
  ) {
    return;
  }

  const track =
    document.getElementById(
      'lammaTrack'
    );

  const progressRow =
    document.getElementById(
      'lammaProgressRow'
    );

  const counterEl =
    document.getElementById(
      'lammaCounter'
    );

  const playPauseBtn =
    document.getElementById(
      'lammaPlayPause'
    );

  const prevBtn =
    document.getElementById(
      'lammaPrev'
    );

  const nextBtn =
    document.getElementById(
      'lammaNext'
    );

  if (
    !track ||
    !progressRow
  ) {
    return;
  }

  const DURATION =
    (opts && opts.duration) ||
    4500;

  const altBase =
    (opts && opts.altBase) ||
    '';

  const reduceMotion =
    window.matchMedia(
      '(prefers-reduced-motion: reduce)'
    ).matches;

  const multi =
    images.length > 1;

  let index = 0;

  let playing =
    multi &&
    !reduceMotion;

  let timer = null;

  track.innerHTML =
    images
      .map(
        (src, i) => `
          <div
            class="lamma-slide${i === 0 ? ' active' : ''}"
          >
            <img
              loading="${i === 0 ? 'eager' : 'lazy'}"
              src="${escapeHTML(src)}"
              alt="${escapeHTML(
                altBase
              )}${
                altBase
                  ? ' — صورة ' +
                    (i + 1)
                  : ''
              }"
            >
          </div>
        `
      )
      .join('');

  progressRow.innerHTML =
    multi
      ? images
          .map(
            (_, i) => `
              <button
                type="button"
                class="lamma-progress-seg"
                aria-label="الانتقال لهذه الصورة"
              >
                <span class="fill"></span>
              </button>
            `
          )
          .join('')
      : '';

  if (prevBtn) {
    prevBtn.hidden = !multi;
  }

  if (nextBtn) {
    nextBtn.hidden = !multi;
  }

  if (playPauseBtn) {
    playPauseBtn.hidden = !multi;
  }

  const slides = [
    ...track.children
  ];

  const segs = [
    ...progressRow.children
  ];

  function paintSegs() {
    segs.forEach(
      (seg, i) => {
        const fill =
          seg.querySelector(
            '.fill'
          );

        seg.classList.toggle(
          'done',
          i < index
        );

        seg.classList.toggle(
          'active',
          i === index
        );

        fill.style.animation =
          'none';

        if (
          i === index &&
          playing
        ) {
          void fill.offsetWidth;

          fill.style.animation =
            `lammaFill ${DURATION}ms linear forwards`;
        }
      }
    );
  }

  function restartTimer() {
    clearTimeout(timer);

    if (
      playing &&
      multi
    ) {
      timer = setTimeout(
        () =>
          goTo(index + 1),
        DURATION
      );
    }
  }

  function goTo(next) {
    index =
      (next + images.length) %
      images.length;

    slides.forEach(
      (s, i) => {
        s.classList.toggle(
          'active',
          i === index
        );
      }
    );

    if (counterEl) {
      counterEl.textContent =
        `${index + 1} / ${images.length}`;
    }

    paintSegs();
    restartTimer();
  }

  function setPlaying(next) {
    playing = next;

    if (playPauseBtn) {
      playPauseBtn.textContent =
        playing
          ? '⏸'
          : '▶';

      playPauseBtn.setAttribute(
        'aria-label',
        playing
          ? 'إيقاف التبديل التلقائي بين الصور'
          : 'تشغيل التبديل التلقائي بين الصور'
      );
    }

    paintSegs();
    restartTimer();
  }

  if (multi) {
    prevBtn?.addEventListener(
      'click',
      () =>
        goTo(index - 1)
    );

    nextBtn?.addEventListener(
      'click',
      () =>
        goTo(index + 1)
    );

    playPauseBtn?.addEventListener(
      'click',
      () =>
        setPlaying(!playing)
    );

    segs.forEach(
      (seg, i) => {
        seg.addEventListener(
          'click',
          () => goTo(i)
        );
      }
    );

    root.addEventListener(
      'mouseenter',
      () => {
        clearTimeout(timer);
      }
    );

    root.addEventListener(
      'mouseleave',
      () => {
        if (playing) {
          restartTimer();
        }
      }
    );

    root.addEventListener(
      'focusin',
      () => {
        clearTimeout(timer);
      }
    );

    /*
      Restart only when focus leaves the entire carousel,
      not when it moves between controls inside it.
    */
    root.addEventListener(
      'focusout',
      e => {
        if (
          !root.contains(
            e.relatedTarget
          ) &&
          playing
        ) {
          restartTimer();
        }
      }
    );

    root.addEventListener(
      'keydown',
      e => {
        if (
          e.key ===
          'ArrowRight'
        ) {
          e.preventDefault();
          goTo(index - 1);
        } else if (
          e.key ===
          'ArrowLeft'
        ) {
          e.preventDefault();
          goTo(index + 1);
        }
      }
    );

    let touchStartX = null;

    root.addEventListener(
      'touchstart',
      e => {
        touchStartX =
          e.touches[0].clientX;

        clearTimeout(timer);
      },
      {
        passive: true
      }
    );

    root.addEventListener(
      'touchend',
      e => {
        if (
          touchStartX == null
        ) {
          return;
        }

        const dx =
          e.changedTouches[0]
            .clientX -
          touchStartX;

        if (
          Math.abs(dx) > 40
        ) {
          dx > 0
            ? goTo(index - 1)
            : goTo(index + 1);
        } else if (
          playing
        ) {
          restartTimer();
        }

        touchStartX = null;
      }
    );
  }

  if (counterEl) {
    counterEl.textContent =
      `1 / ${images.length}`;
  }

  paintSegs();
  restartTimer();
}


/* ==========================================================================
   Welcome intro — فيلم ترحيب سينمائي يُعرض مرة واحدة فقط عند أول زيارة
   لأي صفحة فى الموقع (وليس فى كل تنقل بين الصفحات). قابل لإعادة التشغيل فى
   أي وقت من أي عنصر يحمل الخاصية [data-replay-intro] (مستخدَم فى الفوتر
   وفى صفحة "من نحن").

   ملاحظة للفريق: INTRO_VIDEO_SRC يشير حاليًا لكليب تجريبي من مكتبة الأصول
   لأغراض العرض فقط. عند استلام الفيديو التعريفي النهائي من العميل:
   1) استبدل قيمة INTRO_VIDEO_SRC بمساره.
   2) عدّل توقيتات INTRO_CAPTIONS (بالثواني) لتتزامن مع محتواه.
   ========================================================================== */

const INTRO_STORAGE_KEY = 'josour_intro_seen_v2';

const INTRO_VIDEO_SRC = 'assets/videos/INTRO_VIDEO.mp4';

const INTRO_CAPTIONS = [
  { at: 0.15, eyebrow: 'أهلاً بك في', title: 'جسور' },
  {
    at: 3,
    eyebrow: 'منصة المصريين بالمهجر',
    title: 'أخبار الجاليات تصلك أينما كنت'
  },
  {
    at: 6,
    eyebrow: 'وقت الأزمات',
    title: '"اطمّن" يوصّلك بأهلك في لحظة'
  },
  { at: 9, eyebrow: '', title: 'تصفح جسور الآن' }
];

const ICON_SOUND_ON =
  '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M11 5 6 9H2v6h4l5 4V5Z"/><path d="M15.5 8.5a5 5 0 0 1 0 7"/><path d="M18.5 6a9 9 0 0 1 0 12"/></svg>';

const ICON_SOUND_OFF =
  '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M11 5 6 9H2v6h4l5 4V5Z"/><line x1="23" y1="9" x2="17" y2="15"/><line x1="17" y1="9" x2="23" y2="15"/></svg>';

const INTRO_RING_CIRCUMFERENCE = 2 * Math.PI * 9;

let introOverlayEl = null;

function introStorageAvailable() {
  try {
    const testKey = '__josour_test__';
    localStorage.setItem(testKey, '1');
    localStorage.removeItem(testKey);
    return true;
  } catch (err) {
    // خصوصية المتصفح (تصفح خاص، أو كوكيز معطّلة) قد تمنع localStorage —
    // فى هذه الحالة الأفضل إننا مانزعجش الزائر ومانعرضش الفيديو أصلًا
    return false;
  }
}

function hasSeenIntro() {
  if (!introStorageAvailable()) return true;
  try {
    return localStorage.getItem(INTRO_STORAGE_KEY) === '1';
  } catch (err) {
    return true;
  }
}

function markIntroSeen() {
  try {
    localStorage.setItem(INTRO_STORAGE_KEY, '1');
  } catch (err) {
    /* تجاهل بصمت — لا داعي لكسر التجربة لو التخزين مرفوض */
  }
}

function buildIntroOverlay() {
  const overlay = document.createElement('div');

  overlay.className = 'intro-overlay';
  overlay.id = 'introOverlay';
  overlay.setAttribute('role', 'dialog');
  overlay.setAttribute('aria-modal', 'true');
  overlay.setAttribute('aria-label', 'فيديو الترحيب بمنصة جسور');

  overlay.innerHTML = `
    <div class="intro-stage">
      <div class="intro-bar top"></div>
      <div class="intro-bar bottom"></div>

      <video
        class="intro-video"
        data-intro-video
        muted
        playsinline
        preload="auto"
        src="${INTRO_VIDEO_SRC}"
      ></video>

      <div class="intro-scrim"></div>

      <div class="intro-loading" data-intro-loading>
        <img class="mark" src="assets/josour-logo.png" alt="" />
        <span>جاري تجهيز الترحيب...</span>
      </div>

      <div class="intro-progress-track">
        <div class="intro-progress-fill" data-intro-progress></div>
      </div>

      <div class="intro-brand">
        <img src="assets/josour-logo.png" alt="" />
        <span>جسور</span>
      </div>

      <button
        class="intro-skip"
        data-intro-skip
        type="button"
        aria-label="تخطي فيديو الترحيب"
      >
        <svg class="ring" width="20" height="20" viewBox="0 0 20 20">
          <circle class="ring-bg" cx="10" cy="10" r="9"></circle>
          <circle
            class="ring-fg"
            data-intro-ring
            cx="10"
            cy="10"
            r="9"
            stroke-dasharray="${INTRO_RING_CIRCUMFERENCE.toFixed(1)}"
            stroke-dashoffset="0"
          ></circle>
        </svg>
        تخطي
      </button>

      <button
        class="intro-sound"
        data-intro-sound
        type="button"
        aria-label="تشغيل الصوت"
      >
        ${ICON_SOUND_OFF}
      </button>

      <span class="intro-sound-hint" data-intro-sound-hint>
        اضغط لتشغيل الصوت 🔊
      </span>

      <div class="intro-captions">
        <span class="intro-caption-eyebrow" data-intro-eyebrow></span>
        <h2 class="intro-caption-title" data-intro-title></h2>
      </div>
    </div>
  `;

  return overlay;
}

function openIntro(opts = {}) {
  const forceReplay = !!opts.forceReplay;

  if (introOverlayEl) return; // فيه أوفرلاي شغال فعلًا
  if (!forceReplay && hasSeenIntro()) return;

  // نسجّل إنه اتشاف فورًا (مش بعد ما يخلص) — عشان لو الزائر عمل تخطي
  // فورًا أو قفل التاب فى نص الفيديو، الفيديو مايرجعش يظهر تاني بالغلط
  if (!forceReplay) markIntroSeen();

  const overlay = buildIntroOverlay();
  document.body.appendChild(overlay);
  introOverlayEl = overlay;
  document.body.classList.add('intro-locked');

  const video = overlay.querySelector('[data-intro-video]');
  const loadingEl = overlay.querySelector('[data-intro-loading]');
  const progressFill = overlay.querySelector('[data-intro-progress]');
  const skipRing = overlay.querySelector('[data-intro-ring]');
  const skipBtn = overlay.querySelector('[data-intro-skip]');
  const soundBtn = overlay.querySelector('[data-intro-sound]');
  const soundHint = overlay.querySelector('[data-intro-sound-hint]');
  const eyebrowEl = overlay.querySelector('[data-intro-eyebrow]');
  const titleEl = overlay.querySelector('[data-intro-title]');

  video.muted = true;

  let captionIndex = -1;
  let closed = false;
  let safetyTimer = null;
  let hintShowTimer = null;
  let hintHideTimer = null;

  function showCaption(i) {
    const c = INTRO_CAPTIONS[i];
    if (!c) return;

    eyebrowEl.textContent = c.eyebrow || '';
    eyebrowEl.classList.toggle('show', !!c.eyebrow);

    titleEl.classList.remove('show');
    titleEl.textContent = c.title || '';
    void titleEl.offsetWidth; // إعادة تشغيل الأنيميشن
    titleEl.classList.add('show');
  }

  function onTimeUpdate() {
    if (video.duration) {
      progressFill.style.width =
        (video.currentTime / video.duration) * 100 + '%';

      if (skipRing) {
        const remaining = 1 - video.currentTime / video.duration;
        skipRing.style.strokeDashoffset = String(
          INTRO_RING_CIRCUMFERENCE * Math.max(0, remaining)
        );
      }
    }

    const nextIndex = INTRO_CAPTIONS.findIndex(
      (c, i) => i > captionIndex && video.currentTime >= c.at
    );

    if (nextIndex !== -1) {
      captionIndex = nextIndex;
      showCaption(nextIndex);
    }
  }

  function onLoadedData() {
    loadingEl.classList.add('hide');
    video.classList.add('is-ready');
    clearTimeout(safetyTimer);

    video.play().catch(() => {
      // بعض المتصفحات ممكن ترفض الـ autoplay حتى وهو مكتوم — نتجاهل
      // بهدوء، الفيديو هيفضل ظاهر والمستخدم يقدر يتخطى فى أي وقت
    });
  }

  function onEnded() {
    close();
  }

  function onError() {
    // فشل تحميل الفيديو (مسار خاطئ / مشكلة شبكة) — منسكرش تجربة الموقع،
    // نقفل الأوفرلاي بهدوء زي لو المستخدم عمل تخطي
    close();
  }

  function onKeydown(e) {
    if (e.key === 'Escape') close();
  }

  function close() {
    if (closed) return;
    closed = true;

    markIntroSeen();

    clearTimeout(safetyTimer);
    clearTimeout(hintShowTimer);
    clearTimeout(hintHideTimer);

    video.removeEventListener('timeupdate', onTimeUpdate);
    video.removeEventListener('loadeddata', onLoadedData);
    video.removeEventListener('ended', onEnded);
    video.removeEventListener('error', onError);
    window.removeEventListener('keydown', onKeydown);

    overlay.classList.remove('open');
    overlay.classList.add('closing');
    document.body.classList.remove('intro-locked');

    let finished = false;

    function finish() {
      if (finished) return;
      finished = true;

      video.pause();
      overlay.remove();

      if (introOverlayEl === overlay) {
        introOverlayEl = null;
      }
    }

    overlay.addEventListener('transitionend', finish, { once: true });
    setTimeout(finish, 900); // شبكة أمان لو الـ transition مانفعش
  }

  video.addEventListener('timeupdate', onTimeUpdate);
  video.addEventListener('loadeddata', onLoadedData);
  video.addEventListener('ended', onEnded);
  video.addEventListener('error', onError);
  window.addEventListener('keydown', onKeydown);

  skipBtn.addEventListener('click', close);

  soundBtn.addEventListener('click', () => {
    video.muted = !video.muted;

    soundBtn.innerHTML = video.muted
      ? ICON_SOUND_OFF
      : ICON_SOUND_ON;

    soundBtn.setAttribute(
      'aria-label',
      video.muted ? 'تشغيل الصوت' : 'كتم الصوت'
    );

    soundHint.classList.remove('show');

    if (!video.muted) {
      video.play().catch(() => {});
    }
  });

  // تلميح لطيف لتشغيل الصوت يظهر لثوانٍ ثم يختفي من نفسه
  hintShowTimer = setTimeout(
    () => soundHint.classList.add('show'),
    1300
  );

  hintHideTimer = setTimeout(
    () => soundHint.classList.remove('show'),
    5500
  );

  // شبكة أمان: لو الفيديو محملش خلال 6 ثواني (مشكلة شبكة) نتخطى تلقائيًا
  // بدل ما نحبس الزائر قدام شاشة تحميل بلا نهاية
  safetyTimer = setTimeout(() => {
    if (video.readyState < 2) close();
  }, 6000);

  requestAnimationFrame(() => overlay.classList.add('open'));
}

function initWelcomeIntro() {
  // إعادة التشغيل اليدوي متاحة من أي عنصر بخاصية data-replay-intro
  // (الفوتر فى كل صفحة، وصفحة "من نحن") — بتشتغل حتى لو الفيديو اتشاف قبل كده
  document.addEventListener('click', e => {
    const trigger = e.target.closest('[data-replay-intro]');
    if (!trigger) return;

    e.preventDefault();
    openIntro({ forceReplay: true });
  });

  // احترام وضع "توفير البيانات" لو المتصفح بيدعمه — منحملش فيديو ترحيبي
  // على زائر فعّل توفير البيانات عمدًا
  const conn =
    navigator.connection ||
    navigator.mozConnection ||
    navigator.webkitConnection;

  if (conn && conn.saveData) {
    markIntroSeen();
    return;
  }

  if (hasSeenIntro()) return;

  // نأجل الظهور شوية لحد ما محتوى الصفحة نفسه يترسم، عشان مايبقاش فيه
  // قفزة بصرية مفاجئة قبل ما أي حاجة تتحمل
  const start = () => openIntro();

  if ('requestIdleCallback' in window) {
    requestIdleCallback(start, { timeout: 1200 });
  } else {
    setTimeout(start, 300);
  }
}


/* ==========================================================
   Centralized Application Initialization
   ========================================================== */

document.addEventListener(
  'DOMContentLoaded',
  () => {
    /*
      IMPORTANT:
      initTopbarClock() replaces the old startClock()
      so there is only ONE clock controlling [data-clock].
    */
    initTopbarClock();
    initWelcomeIntro();
    initCrisisMode();
    initCrisisDemoToggle();
    initCrisisAttentionWatcher();

    initDrawer();
    initSearchOverlay();
    renderAuthSlot();
    initSharedNavigation();
    initFooterNewsletter();
    initPollForm();
    initSeniorServicesSlider();
    initScrollReveal();
    initReadProgress();
    initVideoRail();

    if (
      typeof VIDEOS !==
      'undefined'
    ) {
      initVideoHub(VIDEOS);
    }


    /* ---------- Second Generation Cards ---------- */

    function renderSecondGeneration() {
      const grid =
        document.getElementById(
          'secondGenGrid'
        );

      if (
        !grid ||
        typeof ARTICLES ===
          'undefined'
      ) {
        return;
      }

      const items =
        typeof getArticlesByCategory ===
        'function'
          ? getArticlesByCategory(
              'الجيل الثاني',
              4
            )
          : [];

      grid.innerHTML =
        items.length
          ? items
              .map(cardGrid)
              .join('')
          : `
            <p class="empty-state">
              لا توجد أخبار حاليًا.
            </p>
          `;
    }

    renderSecondGeneration();
  }
);

/* ==========================================================================
   Modal / Confirm Dialog — shared across the public site and the CMS.
   confirmDialog() was previously called from cms.html and atmaen.html
   without ever being defined; this implements it for real instead of
   leaving those buttons silently broken.
   ========================================================================== */
function openModal({ title = '', bodyHTML = '', actions = [], wide = false } = {}) {
  closeModal();
  const overlay = document.createElement('div');
  overlay.className = 'modal-overlay';
  overlay.innerHTML = `
    <div class="modal-box ${wide ? 'wide' : ''}" role="dialog" aria-modal="true">
      <div class="modal-head">
        <h3>${title}</h3>
        <button class="modal-x" aria-label="إغلاق" onclick="closeModal()">✕</button>
      </div>
      <div class="modal-body">${bodyHTML}</div>
      <div class="modal-actions"></div>
    </div>`;
  const actionsWrap = overlay.querySelector('.modal-actions');
  actions.forEach(a => {
    const btn = document.createElement('button');
    btn.className = a.className || 'btn btn-ghost';
    btn.textContent = a.label;
    btn.addEventListener('click', () => { if (a.onClick) a.onClick(); if (a.close !== false) closeModal(); });
    actionsWrap.appendChild(btn);
  });
  overlay.addEventListener('mousedown', e => { if (e.target === overlay) closeModal(); });
  document.body.appendChild(overlay);
  document.body.style.overflow = 'hidden';
  requestAnimationFrame(() => overlay.classList.add('show'));
  return overlay;
}
function closeModal() {
  document.querySelectorAll('.modal-overlay').forEach(o => o.remove());
  document.body.style.overflow = '';
}
function confirmDialog({ title = 'تأكيد', body = '', onConfirm, confirmLabel = 'تأكيد', danger = true } = {}) {
  openModal({
    title,
    bodyHTML: `<p style="color:var(--text-mute);font-size:14px;line-height:1.8;margin:0;">${body}</p>`,
    actions: [
      { label: 'إلغاء', className: 'btn btn-ghost' },
      { label: confirmLabel, className: `btn ${danger ? 'btn-danger' : 'btn-primary'}`, onClick: onConfirm },
    ],
  });
}
document.addEventListener('keydown', e => { if (e.key === 'Escape') closeModal(); });