// ═══════════════════════════════════════════════════════════
//  01 — خط الأساس: مستخدم واحد على أهم المسارات (نسخة تشخيصية)
//  كل نقطة لها عتبة خاصة — الملخص سيُظهر زمن كل نقطة منفصلة
//  التشغيل:  k6 run 01-baseline.js   (شغّل 00-seed.js أولاً)
// ═══════════════════════════════════════════════════════════
import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE, login, authHeaders, firstId } from './lib.js';

export const options = {
  vus: 1,
  iterations: 8,
  thresholds: {
    http_req_duration: ['p(95)<500'],
    'http_req_duration{name:statement}': ['p(95)<500'],
    'http_req_duration{name:list-passes}': ['p(95)<500'],
    'http_req_duration{name:payments-list}': ['p(95)<500'],
  },
};

export function setup() {
  const token = login();
  const tenantId = firstId(token, `${BASE}/api/Tenants`, 'list-tenants');
  if (!tenantId) throw new Error('❌ لا يوجد مستأجرون — شغّل 00-seed.js أولاً');
  console.log(`==> كشف الحساب سيُقاس على المستأجر: ${tenantId}`);
  return { token, tenantId };
}

export default function (data) {
  const H = authHeaders(data.token);

  let res = http.get(`${BASE}/api/VisitorPasses/paged?page=1&pageSize=20`, { headers: H, tags: { name: 'list-passes' } });
  check(res, { 'قائمة التصاريح 2xx': (r) => r.status < 300 });
  // 🔍 طباعة مجزئ الزمن من الاستجابة
  try {
    const b = res.json();
    if (b && b.data) {
      console.log(`🔍 DIAG EF: count=${b.data.diagCountMs} | صفوف=${b.data.diagRowsMs} | تحويل=${b.data.diagMapMs} — ADO: JOIN=${b.data.diagAdoJoinMs}ms | NOLOCK=${b.data.diagAdoNolockMs}ms | بلاJoin=${b.data.diagAdoNoJoinMs}ms`);
    }
  } catch (_) {}

  res = http.get(`${BASE}/api/TenantAccounts/${data.tenantId}/statement`, { headers: H, tags: { name: 'statement' } });
  check(res, { 'كشف الحساب 2xx': (r) => r.status < 300 });

  res = http.get(`${BASE}/api/Payments`, { headers: H, tags: { name: 'payments-list' } });
  check(res, { 'الدفعات 2xx': (r) => r.status < 300 });

  sleep(1);
}
