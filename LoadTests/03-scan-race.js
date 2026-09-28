// ═══════════════════════════════════════════════════════════
//  03 — سباق المسح (الأهم أمنياً 🎯)
//  نفس التصريح يُمسح بعشرات الطلبات في نفس اللحظة —
//  يجب ألا يدخل أكثر من maxEntries مهما كان الإقبال المتزامن
//  التشغيل:  k6 run 03-scan-race.js   (شغّل 00-seed.js أولاً)
//  النجاح:   count للمؤشر scan_allowed = 3 بالضبط (وليس أكثر)
// ═══════════════════════════════════════════════════════════
import { check } from 'k6';
import { Counter } from 'k6/metrics';
import { login, createPass, scanPass, firstId } from './lib.js';

// عدّاد الدخول المسموح به — المفروض يتوقف عند 3 بالضبط
const allowed = new Counter('scan_allowed');

const MAX_ENTRIES = 3;

export const options = {
  thresholds: {
    // 🎯 المعيار الذهبي: لا دخول فوق الحد مطلقاً
    scan_allowed: ['count<=3'],
    http_req_failed: ['rate<0.05'],
  },
  scenarios: {
    race_phase: {
      executor: 'constant-arrival-rate',
      rate: 60,                // 60 مسح/ثانية — عاصفة حقيقية
      timeUnit: '1s',
      duration: '5s',          // ≈ 300 مسح متزامن على نفس التصريح
      preAllocatedVUs: 80,
      maxVUs: 150,
      startTime: '3s',
      exec: 'race',
    },
  },
};

// يُنفَّذ مرة واحدة قبل السباق: إنشاء التصريح بمعرف محل حقيقي
export function setup() {
  const token = login();
  const unitId = firstId(token, `${BASE}/api/Units`, 'list-units');
  if (!unitId) throw new Error('❌ لا توجد وحدات في القاعدة — شغّل 00-seed.js أولاً');

  const passCode = createPass(token, `LT-سباق-${Date.now()}`, MAX_ENTRIES, unitId);
  if (!passCode) throw new Error('فشل إنشاء تصريح السباق');
  console.log(`==> تصريح السباق: ${passCode} (الحد = ${MAX_ENTRIES} دخول)`);
  return { token, passCode };
}

export function race(data) {
  const result = scanPass(data.token, data.passCode);
  if (result.isSuccess === true) allowed.add(1);

  check(result, {
    'الاستجابة تحتوي قراراً واضحاً': (r) => typeof r.isSuccess === 'boolean',
  });
}

export function teardown(data) {
  // تحقق نهائي: محاولة إضافية يجب أن تُرفض دائماً
  const late = scanPass(data.token, data.passCode);
  console.log(`\n==> المسح بعد انتهاء السباق: ${late.isSuccess ? '❌ قبول خطير!' : '✅ رفض صحيح'} — ${late.message || ''}`);
  console.log(`==> راقب مؤشر scan_allowed في الملخص: يجب أن يكون <= ${MAX_ENTRIES}`);
}
