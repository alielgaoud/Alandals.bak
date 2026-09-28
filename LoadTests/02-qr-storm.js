// ═══════════════════════════════════════════════════════════
//  02 — عاصفة QR: إنشاء تصاريح + مسح تحت ضغط متصاعد
//  التشغيل:  k6 run 02-qr-storm.js   (شغّل 00-seed.js أولاً)
// ═══════════════════════════════════════════════════════════
import { check, sleep } from 'k6';
import { BASE, login, createPass, scanPass, firstId } from './lib.js';

export const options = {
  stages: [
    { duration: '30s', target: 10 },  // إحماء
    { duration: '30s', target: 30 },  // حمل متوسط
    { duration: '30s', target: 50 },  // حمل مرتفع
    { duration: '20s', target: 0 },   // تهدئة
  ],
  thresholds: {
    http_req_failed: ['rate<0.01'],   // أخطاء أقل من 1%
    'http_req_duration{name:create-pass}': ['p(95)<1500'],
    'http_req_duration{name:scan-pass}': ['p(95)<1000'],
  },
};

// يُنفَّذ مرة واحدة قبل كل السيناريو: توكن واحد + أول محل حقيقي
export function setup() {
  const token = login();
  const unitId = firstId(token, `${BASE}/api/Units`, 'list-units');
  if (!unitId) throw new Error('❌ لا توجد وحدات في القاعدة — شغّل 00-seed.js أولاً');
  return { token, unitId };
}

let seq = 0;

export default function (data) {
  const token = data.token;

  const name = `LT-عاصفة-${__VU}-${__ITER}-${Date.now()}`;
  const code = createPass(token, name, 2, data.unitId);
  if (!check(code, { 'تم إنشاء التصريح': (c) => !!c })) return;

  const result = scanPass(token, code);
  check(result, { 'المسح الأول ناجح': (r) => r.isSuccess === true });

  // مسح ثانٍ على نفس التصريح (maxEntries = 2) — يجب أن ينجح أيضاً
  const result2 = scanPass(token, code);
  check(result2, { 'المسح الثاني ناجح': (r) => r.isSuccess === true });

  // مسح ثالث — يجب أن يُرفض (استنفاد الحد)
  const result3 = scanPass(token, code);
  check(result3, { 'المسح الثالث مرفوض': (r) => r.isSuccess === false });

  sleep(0.5);
}
