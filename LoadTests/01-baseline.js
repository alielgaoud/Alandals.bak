// ═══════════════════════════════════════════════════════════
//  01 — خط الأساس: مستخدم واحد على أهم المسارات
//  الغرض: قياس زمن الاستجابة "السليم" قبل أي ضغط
//  التشغيل:  k6 run 01-baseline.js
// ═══════════════════════════════════════════════════════════
import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE, login, authHeaders } from './lib.js';

export const options = {
  vus: 1,
  iterations: 10,
  thresholds: {
    http_req_duration: ['p(95)<500'], // خط الأساس: p95 أقل من نصف ثانية
  },
};

export default function () {
  const token = login();
  const H = authHeaders(token);

  // 1) قائمة التصاريح
  let res = http.get(`${BASE}/api/VisitorPasses`, { headers: H, tags: { name: 'list-passes' } });
  check(res, { 'قائمة التصاريح 2xx': (r) => r.status >= 200 && r.status < 300 });

  // 2) كشف حساب المستأجر (أثقل استعلام قراءة)
  res = http.get(`${BASE}/api/TenantAccounts/1/statement`, { headers: H, tags: { name: 'statement' } });
  check(res, { 'كشف الحساب 2xx': (r) => r.status >= 200 && r.status < 300 });

  // 3) قائمة الدفعات
  res = http.get(`${BASE}/api/Payments`, { headers: H, tags: { name: 'payments-list' } });
  check(res, { 'الدفعات 2xx': (r) => r.status >= 200 && r.status < 300 });

  sleep(1);
}
