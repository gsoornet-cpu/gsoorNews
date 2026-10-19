import type { Metadata } from "next";
import CmsWorkspace from "@/components/cms/CmsWorkspace";
import "@/styles/cms.css";

export const metadata: Metadata = {
  title: "لوحة التحرير — جسور",
  robots: { index: false, follow: false }, // the CMS must never be indexed
};

export default function CmsLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <main id="main" className="cms">
      <CmsWorkspace>{children}</CmsWorkspace>
    </main>
  );
}
