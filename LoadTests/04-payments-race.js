// ═══════════════════════════════════════════════════════════
//  04 — سباق مولّد الإيصالات 🎯
//  عشرات الدفعات المتزامنة — يجب ألا يتكرر رقم إيصال أبداً
//  وألا يظهر أي 500 (كان يحدث قبل إصلاح UPDLOCK)
//  التشغيل:  k6 run 04-payments-race.js  (شغّل 00-seed.js أولاً)
//  ملاحظة: ينشئ دفعات حقيقية (50 د.ل) — نظّفها لاحقاً (انظر README)
// ═══════════════════════════════════════════════════════════
import http from 'k6/http';
import { check } from 'k6';
import { BASE, login, authHeaders, firstId } from './lib.js';

export const options = {
  setupTimeout: '120s', // السيرفر قد يكون منشغلاً بصرّ طابور السيناريو السابق
  vus: 20,
  iterations: 40, // 40 دفعة متزامنة القوة القصوى
  thresholds: {
    http_req_failed: ['rate<0.01'],   // صفر 500 تقريباً — أي تكرار إيصال = 500
    // 🛡️ معايرة على أول بيانات حقيقية (بعد علاج شلل الـ WebPush): 20 كاتباً متوازياً
    // = طابور بقانون Little (20 قيد الطيران ÷ ~11 دفعة/ثانية ≈ 1.8s متوسط انتظار).
    // المرضي كان 58 ثانية/مهلات 60s — اختفى كلياً. 3 ثوانٍ تراقب العودة لمرض الطابور لا بطء كل طلب.
    'http_req_duration{name:create-payment}': ['p(95)<3000'],
  },
};

// يُنفَّذ مرة واحدة: توكن + أول عقد حقيقي من القاعدة
export function setup() {
  const token = login();
  const contractId = firstId(token, `${BASE}/api/Contracts`, 'list-contracts');
  if (!contractId) throw new Error('❌ لا توجد عقود في القاعدة — شغّل 00-seed.js أولاً');
  console.log(`==> العقد المستهدف: ${contractId}`);
  return { token, contractId };
}

export default function (data) {
  const res = http.post(`${BASE}/api/Payments`,
    JSON.stringify({
      contractId: data.contractId,
      paymentType: 1, // Rent
      amount: 50,
      paymentMethod: 1, // Cash
      notes: 'LT-اختبار ضغط المولد',
    }),
    { headers: authHeaders(data.token), tags: { name: 'create-payment' } });

  check(res, {
    'الدفعة ناجحة (2xx/201)': (r) => r.status >= 200 && r.status < 300,
  });
}
