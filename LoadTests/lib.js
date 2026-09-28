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

  // status = 0 يعني فشل الاتصال أصلاً (السيرفر غير مشغّل)
  if (res.status === 0) {
    throw new Error(`❌ لا يمكن الوصول إلى الـ API على ${BASE} — شغّل السيرفر أولاً (Ctrl+F5) ثم أعد السكربت`);
  }

  const body = res.json();
  if (!body || !body.data || !body.data.token) {
    throw new Error(`فشل الدخول (${res.status}) — تأكد أن الـ API يعمل وبيانات frames صحيحة`);
  }
  return body.data.token;
}

/** جلب أول id من أي قائمة (وحدات/مستأجرين/عقود) — يجعل السكربتات مستقلة عن أي قاعدة */
export function firstId(token, url, tag) {
  const res = http.get(url, { headers: authHeaders(token), tags: { name: tag || 'list' } });
  if (res.status === 0) return null;
  let body = null;
  try { body = res.json(); } catch (_) { return null; }
  const arr = body && body.data;
  if (Array.isArray(arr) && arr.length > 0 && arr[0] && arr[0].id != null) return arr[0].id;
  return null;
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

/** إنشاء تصريح زائر — يرجع passCode (أو null عند الفشل) */
export function createPass(token, name, maxEntries = 2, unitId = 1) {
  const res = http.post(`${BASE}/api/VisitorPasses`,
    JSON.stringify({
      visitorName: name,
      visitorPhone: '0910000000',
      unitId: unitId,
      validDate: today(),
      maxEntries: maxEntries,
      purpose: 'اختبار ضغط',
      notes: 'LT-بيانات اختبار',
    }),
    { headers: authHeaders(token), tags: { name: 'create-pass' } });
  if (res.status === 0) return null;
  let body = null;
  try { body = res.json(); } catch (_) { return null; }
  return body && body.data ? body.data.passCode : null;
}

/** مسح تصريح من البوابة */
export function scanPass(token, passCode) {
  const res = http.post(`${BASE}/api/Gate/scan`,
    JSON.stringify({ passCode: passCode, gateName: 'بوابة الاختبار' }),
    { headers: authHeaders(token), tags: { name: 'scan-pass' } });
  if (res.status === 0) {
    return { isSuccess: false, message: '❌ السيرفر غير مشغّل (فشل الاتصال)' };
  }
  let body = null;
  try { body = res.json(); } catch (_) {
    return { isSuccess: false, message: `استجابة غير صالحة (HTTP ${res.status})` };
  }
  return body && body.data ? body.data : { isSuccess: false, message: `HTTP ${res.status}` };
}
