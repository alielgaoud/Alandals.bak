// ═══════════════════════════════════════════════════════════
//  03 — سباق المسح (الأهم أمنياً 🎯)
//  نفس التصريح يُمسح بعشرات الطلبات في نفس اللحظة —
//  يجب ألا يدخل أكثر من maxEntries مهما كان الإقبال المتزامن
//  التشغيل:  k6 run 03-scan-race.js
//  النجاح:   count للمؤشر scan_allowed = 3 بالضبط (وليس أكثر)
// ═══════════════════════════════════════════════════════════
import http from 'k6/http';
import { check } from 'k6';
import { Counter } from 'k6/metrics';
import { BASE, login, authHeaders, createPass, scanPass } from './lib.js';

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
    setup_phase: {
      executor: 'shared-iterations',
      vus: 1, iterations: 1,
      maxDuration: '30s',
      exec: 'setupOnly',
      startTime: '0s',
    },
    race_phase: {
      executor: 'constant-arrival-rate',
      rate: 60,                // 60 مسح/ثانية — عاصفة حقيقية
      timeUnit: '1s',
      duration: '5s',          // ≈ 300 مسح متزامن على نفس التصريح
      preAllocatedVUs: 80,
      maxVUs: 150,
      startTime: '8s',
      exec: 'race',
    },
  },
};

let passCode = null;
let token = null;

export function setupOnly() {
  token = login();
  passCode = createPass(token, `LT-سباق-${Date.now()}`, MAX_ENTRIES);
  if (!passCode) throw new Error('فشل إنشاء تصريح السباق');
  console.log(`==> تصريح السباق: ${passCode} (الحد = ${MAX_ENTRIES} دخول)`);
}

export function race() {
  const result = scanPass(token, passCode);
  if (result.isSuccess === true) allowed.add(1);

  check(result, {
    'الاستجابة تحتوي قراراً واضحاً': (r) => typeof r.isSuccess === 'boolean',
  });
}

export function teardown() {
  // تحقق نهائي: محاولة إضافية يجب أن تُرفض دائماً
  const late = scanPass(token, passCode);
  console.log(`\n==> المسح بعد انتهاء السباق: ${late.isSuccess ? '❌ قبول خطير!' : '✅ رفض صحيح'} — ${late.message || ''}`);
  console.log(`==> عدد الدخول المسموح به إجمالاً (يجب أن يكون <= ${MAX_ENTRIES}): راقب مؤشر scan_allowed في الملخص`);
}
