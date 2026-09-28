// ═══════════════════════════════════════════════════════════
//  00 — تعبئة بيانات تجريبية (Seeder) — لقاعدة بيانات جديدة/فارغة
//  ينشئ عبر الـ API: وحدات → مستأجرين → عقود → تصاريح → دفعات
//  كل البيانات مميزة ببادئة LT- لتنظيفها لاحقاً بسهولة
//
//  التشغيل (والسيرفر يعمل):      k6 run 00-seed.js
//  أعداد مخصصة:
//    k6 run -e SEED_UNITS=30 -e SEED_TENANTS=60 -e SEED_PASSES=30 -e SEED_PAYMENTS=50 00-seed.js
// ═══════════════════════════════════════════════════════════
import http from 'k6/http';
import { check } from 'k6';
import { BASE, login, authHeaders, today } from './lib.js';

const UNITS    = Number(__ENV.SEED_UNITS    || 12);
const TENANTS  = Number(__ENV.SEED_TENANTS  || 25);
const PASSES   = Number(__ENV.SEED_PASSES   || 15);
const PAYMENTS = Number(__ENV.SEED_PAYMENTS || 20);

export const options = {
  vus: 1,
  iterations: 1,
};

const stamp = Date.now(); // فريد لكل تشغيل — لا تصادم عند الإعادة
let created = { units: 0, tenants: 0, contracts: 0, passes: 0, payments: 0 };
let failed = 0;

function postJSON(token, url, payload, name) {
  const res = http.post(url, JSON.stringify(payload),
    { headers: authHeaders(token), tags: { name } });
  let body = null;
  try { body = res.json(); } catch (_) {}
  const ok = res.status >= 200 && res.status < 300 && body && body.success !== false;
  if (!ok) {
    failed++;
    console.error(`✗ ${name} → HTTP ${res.status}: ${res.body ? res.body.substring(0, 200) : ''}`);
  }
  return { ok, body };
}

function monthsAgo(m) {
  const d = new Date();
  d.setMonth(d.getMonth() - m);
  return d.toISOString().substring(0, 10);
}
function plusYears(iso, y) {
  const d = new Date(iso);
  d.setFullYear(d.getFullYear() + y);
  return d.toISOString().substring(0, 10);
}

export default function () {
  const token = login();
  console.log(`==> تعبئة: ${UNITS} وحدة، ${TENANTS} مستأجر، عقود = min(وحدات، مستأجرين)، ${PASSES} تصريح، ${PAYMENTS} دفعة`);

  // ── 1) الوحدات ──
  const unitIds = [];
  for (let i = 1; i <= UNITS; i++) {
    const r = postJSON(token, `${BASE}/api/Units`, {
      unitNumber: `LT-${stamp}-U${i}`,
      area: 20 + (i % 8) * 5,
      floor: String(i % 3),
      building: `LT-B${i % 2 + 1}`,
      description: 'وحدة بيانات تجريبية (LT)',
      notes: 'LT-seed',
    }, 'create-unit');
    if (r.ok && r.body?.data?.id) { unitIds.push(r.body.data.id); created.units++; }
  }

  // ── 2) المستأجرين ──
  const tenantIds = [];
  const names = ['أحمد', 'محمد', 'فاطمة', 'خالد', 'مريم', 'عمر', 'سارة', 'يوسف', 'ليلى', 'حسن'];
  for (let i = 1; i <= TENANTS; i++) {
    const r = postJSON(token, `${BASE}/api/Tenants`, {
      fullName: `LT-${stamp}-مستأجر ${names[i % names.length]} ${i}`,
      nationalId: `LT${stamp}${i}`,
      phone: `091${String(1000000 + i).slice(-7)}`,
      maxAllowedEntriesPerPass: 2,
      notes: 'LT-seed',
    }, 'create-tenant');
    if (r.ok && r.body?.data?.id) { tenantIds.push(r.body.data.id); created.tenants++; }
  }

  // ── 3) العقود (وحدة ↔ مستأجر) — تواريخ في الماضي ليتكدس الإيجار ──
  const contractIds = [];
  const pairs = Math.min(unitIds.length, tenantIds.length);
  for (let i = 0; i < pairs; i++) {
    const start = monthsAgo((i % 6) + 1); // من شهر واحد إلى 6 أشهر خلف
    const r = postJSON(token, `${BASE}/api/Contracts`, {
      tenantId: tenantIds[i],
      unitId: unitIds[i],
      startDate: start,
      endDate: plusYears(start, 1),
      rentAmount: 800 + ((i * 137) % 2200),
      rentCycle: 1, // Monthly
      depositAmount: (i % 3) * 250,
      activityType: 1, // Restaurant (الإدخال يقبل أرقاماً فقط)
      tradeName: `LT-نشاط-${i + 1}`,
      autoRenew: false,
      contractFees: [],
      notes: 'LT-seed',
    }, 'create-contract');
    if (r.ok && r.body?.data?.id) { contractIds.push(r.body.data.id); created.contracts++; }
  }

  // ── 4) تصاريح الزوار (على الوحدات المنشأة) ──
  const passCodes = [];
  for (let i = 1; i <= PASSES && unitIds.length; i++) {
    const r = postJSON(token, `${BASE}/api/VisitorPasses`, {
      visitorName: `LT-${stamp}-زائر ${i}`,
      visitorPhone: `092${String(1000000 + i).slice(-7)}`,
      unitId: unitIds[i % unitIds.length],
      validDate: today(),
      maxEntries: (i % 3) + 1,
      purpose: 'اختبار ضغط (LT)',
      notes: 'LT-seed',
    }, 'create-pass');
    if (r.ok && r.body?.data?.passCode) { passCodes.push(r.body.data.passCode); created.passes++; }
  }

  // ── 5) دفعات إيجار على العقود ──
  for (let i = 1; i <= PAYMENTS && contractIds.length; i++) {
    postJSON(token, `${BASE}/api/Payments`, {
      contractId: contractIds[i % contractIds.length],
      paymentType: 1, // Rent
      amount: 100 + ((i * 53) % 400),
      paymentMethod: i % 2 ? 1 : 2, // Cash / Transfer
      notes: 'LT-seed',
    }, 'create-payment');
    // نحتسب النجاح ضمنياً من failed — الأهم ألا تنكسر السلسلة
    created.payments++;
  }

  // ── الملخص ──
  console.log('========================================');
  console.log(`==> نتيجة التعبئة: وحدات ${created.units}/${UNITS} | مستأجرون ${created.tenants}/${TENANTS} | عقود ${created.contracts}/${pairs} | تصاريح ${created.passes}/${PASSES} | دفعات ${created.payments}/${PAYMENTS} | أخطاء ${failed}`);
  check(created.contracts, { 'عقود جاهزة للاختبار': (c) => c >= 3 });
  check(failed, { 'لا أخطاء في التعبئة': (f) => f === 0 });
}
