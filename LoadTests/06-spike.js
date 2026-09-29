// ═══════════════════════════════════════════════════════════
//  06 — اختبار الذروة المفاجئة (Spike)
//  هدوء → قفزة 100 مستخدم دفعة واحدة → عودة للهدوء
//  يحاكي: بداية دوام البوابة / صرف رواتب / إشعار جماعي
//  التشغيل:  k6 run 06-spike.js
// ═══════════════════════════════════════════════════════════
import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE, login, authHeaders } from './lib.js';

export const options = {
  stages: [
    { duration: '30s', target: 5 },   // حركة عادية
    { duration: '10s', target: 100 }, // ⚡ القفزة المفاجئة
    { duration: '40s', target: 100 }, // ثبات على الذروة
    { duration: '20s', target: 5 },   // عودة للطبيعي
    { duration: '20s', target: 0 },
  ],
  thresholds: {
    http_req_failed: ['rate<0.05'],   // السماح ببعض الأخطاء في الذروة القصوى
    http_req_duration: ['p(95)<3000'],
  },
};

export default function () {
  const token = login();
  const H = authHeaders(token);

  const res = http.get(`${BASE}/api/VisitorPasses/paged?page=1&pageSize=20`, { headers: H, tags: { name: 'list-passes' } });
  check(res, { 'القائمة 2xx': (r) => r.status < 300 });

  sleep(0.7);
}
