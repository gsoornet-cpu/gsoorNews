"use client";

import { useEffect, useState } from "react";

// Ported directly from the demo's initTopbarClock() in app.js — same day/
// month name arrays, same "الاثنين 31، أغسطس، 2026 · 04:43:34 صباحًا"
// format. This is real behavior (today's actual date/time), not demo data,
// so it's reimplemented rather than loading app.js wholesale (see Slice 8's
// dashboard entry for why app.js as a whole isn't reused: most of it
// templates the demo's fake story data into the DOM, which would fight
// with the real data this page renders).
const DAYS = ["الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت"];
const MONTHS = [
  "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو",
  "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر",
];

function formatNow() {
  const now = new Date();
  const dayName = DAYS[now.getDay()];
  const dayNumber = String(now.getDate()).padStart(2, "0");
  const monthName = MONTHS[now.getMonth()];
  const year = now.getFullYear();

  let hours = now.getHours();
  const isPm = hours >= 12;
  hours = hours % 12 || 12;
  const hoursStr = String(hours).padStart(2, "0");
  const minutes = String(now.getMinutes()).padStart(2, "0");
  const seconds = String(now.getSeconds()).padStart(2, "0");
  const period = isPm ? "مساءً" : "صباحًا";

  return {
    date: `${dayName} ${dayNumber}، ${monthName}، ${year}`,
    time: `${hoursStr}:${minutes}:${seconds} ${period}`,
  };
}

export function TopbarClock() {
  // Starts null so the server-rendered markup and the first client render
  // match exactly (avoids a hydration mismatch) — the real time fills in
  // a tick after mount, same one-frame delay the demo's own script has.
  const [now, setNow] = useState<{ date: string; time: string } | null>(null);

  useEffect(() => {
    setNow(formatNow());
    const interval = setInterval(() => setNow(formatNow()), 1000);
    return () => clearInterval(interval);
  }, []);

  return (
    <div className="topbar-datetime" dir="rtl">
      <span className="date-part">{now?.date ?? ""}</span>
      <span className="datetime-separator" aria-hidden="true">•</span>
      <span className="time-part">{now?.time ?? ""}</span>
    </div>
  );
}
