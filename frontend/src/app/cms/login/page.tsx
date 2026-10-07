"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { cms, CmsError } from "@/lib/cms-client";

export default function CmsLoginPage() {
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function onSubmit(event: React.FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError(null);

    try {
      await cms.login(email.trim(), password);
      router.replace("/cms");
    } catch (e) {
      setError(e instanceof CmsError ? e.message : "تعذّر تسجيل الدخول.");
      setBusy(false);
    }
  }

  return (
    <div className="cms-card cms-narrow">
      <div className="cms-head">
        <h1>دخول هيئة التحرير</h1>
      </div>

      {error && (
        <div className="cms-msg error" role="alert">
          {error}
        </div>
      )}

      <form onSubmit={onSubmit}>
        <div className="cms-field">
          <label htmlFor="email">البريد الإلكتروني</label>
          <input id="email" type="email" autoComplete="username" required value={email} onChange={(e) => setEmail(e.target.value)} dir="ltr" />
        </div>
        <div className="cms-field">
          <label htmlFor="password">كلمة المرور</label>
          <input id="password" type="password" autoComplete="current-password" required value={password} onChange={(e) => setPassword(e.target.value)} dir="ltr" />
        </div>
        <button className="cms-btn primary" type="submit" disabled={busy}>
          {busy ? "جارٍ الدخول…" : "تسجيل الدخول"}
        </button>
      </form>
    </div>
  );
}
