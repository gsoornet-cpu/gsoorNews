"use client";

import { useMemo, useState } from "react";
import type { PublicDiasporaMapItem } from "@/lib/news-api";

export function DiasporaMapExplorer({ items }: { items: PublicDiasporaMapItem[] }) {
  const [country, setCountry] = useState("all");
  const countries = useMemo(() => [...new Map(items.map((item) => [item.countryId, item.countryNameAr])).entries()].map(([id, nameAr]) => ({ id, nameAr })), [items]);
  const visible = useMemo(() => country === "all" ? items : items.filter((item) => item.countryId === country), [country, items]);
  const plotted = visible.filter((item) => item.cityId && item.latitude !== null && item.longitude !== null);
  return <>
    <div className="diaspora-map-toolbar"><div><span className="diaspora-map-eyebrow">المواقع والبيانات المنشورة</span><h2>مجتمعات المصريين حول العالم</h2></div><label><span>تصفية حسب الدولة</span><select value={country} onChange={(event) => setCountry(event.target.value)}><option value="all">كل الدول</option>{countries.map((item) => <option key={item.id} value={item.id}>{item.nameAr}</option>)}</select></label></div>
    {visible.length > 0 ? <>
      {plotted.length > 0 && <div className="diaspora-coordinate-map" role="img" aria-label={`مواقع المدن المسجلة جغرافيًا: ${plotted.map((item) => item.cityNameAr).join("، ")}`}>
        <div className="diaspora-coordinate-map__axis"><span>الغرب</span><span>خطوط إحداثية للمدن المسجلة</span><span>الشرق</span></div>
        <svg viewBox="0 0 1000 420" preserveAspectRatio="xMidYMid meet" aria-hidden="true">
          {[1, 2, 3, 4].map((index) => <line key={`lat-${index}`} x1="0" x2="1000" y1={index * 84} y2={index * 84} />)}
          {[1, 2, 3, 4, 5, 6, 7, 8, 9].map((index) => <line key={`lon-${index}`} y1="0" y2="420" x1={index * 100} x2={index * 100} />)}
          {plotted.map((item) => <g key={item.id} transform={`translate(${((Number(item.longitude) + 180) / 360) * 1000},${((90 - Number(item.latitude)) / 180) * 420})`}><circle className="diaspora-coordinate-map__halo" r="18" /><circle className="diaspora-coordinate-map__point" r="6" /></g>)}
        </svg>
        <p>تموضع النقاط مستمد من إحداثيات المدن المدخلة في دليل جسور؛ العرض إرشادي ولا يمثل حدودًا سياسية أو مواقع أفراد.</p>
      </div>}
      <div className="diaspora-map-grid">{visible.map((item) => <article className="diaspora-location-card" key={item.id}>
        <div className="diaspora-location-card__title"><span aria-hidden="true">⌖</span><div><h3>{item.cityNameAr ? `${item.cityNameAr}، ${item.countryNameAr}` : item.countryNameAr}</h3><small>{item.cityNameAr ? "مؤشر على مستوى المدينة" : "بيانات على مستوى الدولة"}</small></div></div>
        <div className="diaspora-location-card__metrics"><div><span>تقدير منشور</span><strong>{item.estimatedPopulation.toLocaleString("ar-EG")}</strong><small>مصدر خارجي موثق</small></div><div><span>مؤشر مجتمع جسور</span><strong>{item.communitySignalCount === null ? "—" : item.communitySignalCount.toLocaleString("ar-EG")}</strong><small>{item.communitySignalCount === null ? "غير معروض لحماية الخصوصية" : "مشاركات موقع مجمّعة"}</small></div></div>
        <div className="diaspora-location-card__source"><span>مصدر التقدير: {item.sourceName} · {new Intl.DateTimeFormat("ar-EG", { dateStyle: "medium", timeZone: "UTC" }).format(new Date(`${item.sourcePublishedOn}T00:00:00Z`))}</span><a href={item.sourceUrl} target="_blank" rel="noopener noreferrer">الاطلاع على المصدر ↗</a></div>
      </article>)}</div>
    </> : <div className="diaspora-map-empty"><span aria-hidden="true">⌖</span><h3>{items.length ? "لا توجد بيانات لهذه الدولة" : "لا توجد تقديرات منشورة حتى الآن"}</h3><p>{items.length ? "اختر دولة أخرى من القائمة." : "سيتم عرض البيانات بعد إدخال تقديرات موثقة واعتمادها من فريق جسور. لا نعرض أرقامًا افتراضية."}</p></div>}
  </>;
}
