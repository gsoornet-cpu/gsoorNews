"use client";

import { useState, type CSSProperties, type FormEvent } from "react";

export function HomePollCard() {
  const [answer, setAnswer] = useState("");
  const [message, setMessage] = useState("");

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!answer) {
      setMessage("اختر إجابة أولاً.");
      return;
    }
    setMessage("هذا الاستطلاع للعرض فقط؛ التصويت الدائم غير مفعّل بعد.");
  }

  return (
    <div className="interactive-poll-card">
      <div className="poll-header">
        <span className="poll-badge">⚡ استطلاع اليوم</span>
        <h3 className="poll-question">هل تؤيد زيادة الاعتماد على المعاملات الرقمية بالكامل للمغتربين؟</h3>
      </div>
      <form className="poll-options" onSubmit={submit}>
        {[["digital", "نعم، تسهل الإجراءات كثيراً", "72%"], ["traditional", "لا، أُفضل الطرق التقليدية", "28%"]].map(([value, label, percent]) => (
          <label className="poll-option" key={value}>
            <input type="radio" name="home-poll" value={value} checked={answer === value} onChange={() => setAnswer(value)} />
            <span className="opt-label">{label}</span>
            <span className="opt-bar" style={{ "--percent": percent } as CSSProperties} />
            <span className="opt-percent">{percent}</span>
          </label>
        ))}
        <button type="submit" className="btn-vote">تصويت</button>
      </form>
      <div className="poll-footer"><span>استطلاع تجريبي</span></div>
      {message && <p className="home-widget-message" role="status">{message}</p>}
    </div>
  );
}

export function NewsletterForm() {
  const [message, setMessage] = useState("");
  return (
    <form className="cta-form" onSubmit={(event) => { event.preventDefault(); setMessage("الاشتراك البريدي غير مفعّل بعد."); }}>
      <label className="sr-only" htmlFor="home-newsletter">بريدك الإلكتروني</label>
      <input id="home-newsletter" type="email" required placeholder="بريدك الإلكتروني" />
      <button type="submit">اشترك</button>
      {message && <span className="home-widget-message" role="status">{message}</span>}
    </form>
  );
}
