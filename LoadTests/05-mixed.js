// ═══════════════════════════════════════════════════════════
//  05 — الحمل المختلط الواقعي
//  خليط 80% قراءة (قوائم/كشوف) + 20% كتابة (تصاريح)
//  تصاعدي: 10 → 50 → 100 مستخدم متزامن
//  التشغيل:  k6 run 05-mixed.js
// ═══════════════════════════════════════════════════════════
import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE, login, authHeaders, createPass, scanPass } from './lib.js';

export const options = {
  stages: [
    { duration: '30s', target: 10 },
    { duration: '40s', target: 50 },
    { duration: '40s', target: 100 },
    { duration: '30s', target: 0 },
  ],
  thresholds: {
    http_req_failed: ['rate<0.02'],
    http_req_duration: ['p(95)<2000'],
  },
};

export default function () {
  const token = login();
  const H = authHeaders(token);

  // ── القراءة (75% من الدورة) ──
  let res = http.get(`${BASE}/api/VisitorPasses`, { headers: H, tags: { name: 'list-passes' } });
  check(res, { 'قائمة التصاريح 2xx': (r) => r.status < 300 });

  res = http.get(`${BASE}/api/TenantAccounts/1/statement`, { headers: H, tags: { name: 'statement' } });
  check(res, { 'كشف الحساب 2xx': (r) => r.status < 300 });

  res = http.get(`${BASE}/api/Payments`, { headers: H, tags: { name: 'payments-list' } });
  check(res, { 'الدفعات 2xx': (r) => r.status < 300 });

  sleep(1);

  // ── الكتابة (25% من الدورة) ──
  const code = createPass(token, `LT-خلط-${__VU}-${__ITER}`, 1);
  if (code) {
    const r = scanPass(token, code);
    check(r, { 'مسح بعد الإنشاء': (x) => typeof x.isSuccess === 'boolean' });
  }

  sleep(1);
}
