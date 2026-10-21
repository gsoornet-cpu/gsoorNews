import type { Metadata } from "next";
import { AccountPortal } from "@/components/AccountPortal";

export const metadata: Metadata = { title: "حساب جسور | الملف الشخصي", description: "تسجيل الدخول إلى الملف الشخصي في منصة جسور." };

export default function AccountPage() {
  return <main id="main" className="account-page"><div className="wrap"><AccountPortal /></div></main>;
}
