// ═══════════════════════════════════════════════════════════
//  مكتبة مشتركة لسيناريوهات اختبار الضغط — Andalos API
// ═══════════════════════════════════════════════════════════
import http from 'k6/http';

export const BASE = __ENV.BASE_URL || 'http://localhost:5264';

// بيانات الدخول (SuperAdmin) — يمكن تغييرها من متغيرات البيئة
export const USER = __ENV.K6_USER || 'frames';
export const PASS = __ENV.K6_PASS || '528888';

const JSON_HEADERS = { 'Content-Type': 'application/json' };

/** تسجيل دخول وإرجاع التوكن */
export function login() {
  const res = http.post(`${BASE}/api/Auth/login`,
    JSON.stringify({ username: USER, password: PASS }),
    { headers: JSON_HEADERS, tags: { name: 'login' } });
  const body = res.json();
  if (!body || !body.data || !body.data.token) {
    throw new Error(`فشل الدخول (${res.status}) — تأكد أن الـ API يعمل وبيانات frames صحيحة`);
  }
  return body.data.token;
}

/** ترويسة Authorization جاهزة */
export function authHeaders(token) {
  return {
    'Content-Type': 'application/json',
    Authorization: `Bearer ${token}`,
  };
}

/** تاريخ اليوم بصيغة YYYY-MM-DD (تصريحات صالحة اليوم) */
export function today() {
  return new Date().toISOString().substring(0, 10);
}

/** إنشاء تصريح زائر — يرجع passCode */
export function createPass(token, name, maxEntries = 2) {
  const res = http.post(`${BASE}/api/VisitorPasses`,
    JSON.stringify({
      visitorName: name,
      visitorPhone: '0910000000',
      unitId: 1,
      validDate: today(),
      maxEntries: maxEntries,
      purpose: 'اختبار ضغط',
      notes: 'LT-بيانات اختبار',
    }),
    { headers: authHeaders(token), tags: { name: 'create-pass' } });
  const body = res.json();
  return body && body.data ? body.data.passCode : null;
}

/** مسح تصريح من البوابة */
export function scanPass(token, passCode) {
  const res = http.post(`${BASE}/api/Gate/scan`,
    JSON.stringify({ passCode: passCode, gateName: 'بوابة الاختبار' }),
    { headers: authHeaders(token), tags: { name: 'scan-pass' } });
  const body = res.json();
  return body && body.data ? body.data : { isSuccess: false, message: `HTTP ${res.status}` };
}
