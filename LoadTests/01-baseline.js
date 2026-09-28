// ═══════════════════════════════════════════════════════════
//  01 — خط الأساس: مستخدم واحد على أهم المسارات
//  التشغيل:  k6 run 01-baseline.js
//  (يتطلب بيانات — شغّل 00-seed.js أولاً على قاعدة جديدة)
// ═══════════════════════════════════════════════════════════
import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE, login, authHeaders, firstId } from './lib.js';

export const options = {
  vus: 1,
  iterations: 10,
  thresholds: {
    http_req_duration: ['p(95)<500'], // خط الأساس: p95 أقل من نصف ثانية
  },
};

let tenantId = null;

export default function () {
  const token = login();
  const H = authHeaders(token);

  // نجلب معرف مستأجر حقيقي من القاعدة (مرة واحدة)
  if (!tenantId) {
    tenantId = firstId(token, `${BASE}/api/Tenants`, 'list-tenants');
    if (!tenantId) {
      console.error('❌ لا يوجد مستأجرون في القاعدة — شغّل 00-seed.js أولاً');
    }
  }

  // 1) قائمة التصاريح
  let res = http.get(`${BASE}/api/VisitorPasses/paged?page=1&pageSize=20`, { headers: H, tags: { name: 'list-passes' } });
  check(res, { 'قائمة التصاريح 2xx': (r) => r.status >= 200 && r.status < 300 });

  // 2) كشف حساب المستأجر (أثقل استعلام قراءة) — بمعرف حقيقي
  if (tenantId) {
    res = http.get(`${BASE}/api/TenantAccounts/${tenantId}/statement`, { headers: H, tags: { name: 'statement' } });
    check(res, { 'كشف الحساب 2xx': (r) => r.status >= 200 && r.status < 300 });
  }

  // 3) قائمة الدفعات
  res = http.get(`${BASE}/api/Payments`, { headers: H, tags: { name: 'payments-list' } });
  check(res, { 'الدفعات 2xx': (r) => r.status >= 200 && r.status < 300 });

  sleep(1);
}
