#!/usr/bin/env python3
"""Static inventory/contract drift fence; runtime MVC coverage test remains authoritative."""
import argparse, json, re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
API=ROOT/'Andalos.API'; DOCS=ROOT/'docs/authorization'
KEYS=json.loads((DOCS/'permission-keys.json').read_text())
# Accept the checked-in catalog's keys array or a flat list.
if isinstance(KEYS,dict): KEYS=KEYS.get('permissions',KEYS.get('keys',[]))
if KEYS and isinstance(KEYS[0],dict): KEYS=[x.get('key',x.get('name')) for x in KEYS]
constant=(API/'Constants/Permissions.cs').read_text()
classes=re.split(r'public static class (\w+)',constant)
refs={}
for i in range(1,len(classes),2):
 for action,key in re.findall(r'public const string (\w+) = "([\w.]+)"',classes[i+1]): refs[f'Permissions.{classes[i]}.{action}']=key
known=set(refs.values())
assert len(known)==len(refs), 'duplicate key values'
# JSON may be modules -> key lists (also valid Angular catalog).
if not KEYS:
 raw=json.loads((DOCS/'permission-keys.json').read_text()); KEYS=[k for ks in raw.values() for k in ks]
assert set(KEYS)==known, 'permission JSON drift'
cat=(API/'Security/EndpointSecurityCatalog.cs').read_text()
rules=dict(re.findall(r'\["(\w+\.\w+)"\] = ([^\n]+)',cat))
def role_intersection(*parts):
 result=None
 for p in parts:
  if not p: continue
  r=set(p.split(','));result=r if result is None else result&r
 return sorted(result) if result is not None else []
def context(action,scope,cap):
 c,m=action.split('.')
 if scope in ('Tenant','TenantOwner'):
  return 'هوية DB النشطة؛ tenantId في المسار يجب أن يساوي هوية الجلسة؛ معرف المورد يعود للمستأجر؛ '+('مالك فقط' if scope=='TenantOwner' else 'Tenant أو TenantStaff مع capability='+str(cap))
 if c=='ProtectedFiles':return 'metadata DB + owning Tenant/capability أو exact file permission؛ لا مسارات traversal/symlink؛ legacy غير المراجع ممنوع'
 if c in ('Notifications','NotificationPreferences','PushSubscriptions'):return 'current user/tenant فقط؛ لا test IDs/headers؛ قراءة الإشعار تخضع كذلك لمفتاح نوعه؛ لا takeover/تعديل group broadcast'
 if c=='VisitorWallet' and ('Shift' in m or m in ('CreatePaidPass','AddBalance','ProcessPurchase')):return 'actor/shift من الهوية؛ pass اليوم/active/blacklist؛ purchase tenant+unit+active contract؛ handover ليس نفس cashier؛ مبالغ/ledger/replay'
 if c in ('Payments','Refunds','BankTransfers','TenantAccounts','Maintenance'):return 'staff global بالنسبة للمفتاح؛ tenant/contract/charge/purpose من DB متوافق؛ مبالغ وحالة وتخصيص ومحفظة؛ صلاحيات مالية شرطية إضافية'
 if c=='PermissionPackages' or c=='Users':return 'role fence لا تمنح المفتاح؛ mutations بواسطة SA حالي فقط؛ لا self-elevation/tenant grants؛ expected revision/version عند توفيره؛ reconciliation مراجع'
 if c=='Auth':return 'login منفصل staff/tenant؛ refresh/logout/password لصاحب الجلسة؛ register = 410؛ current DB identity/stamp'
 if c=='VisitorPasses':return 'staff global للمفتاح؛ ownership ثابت عند الإنشاء؛ blacklist/limits؛ scan مفتاح مستقل'
 if c=='Settings':return 'المفاتيح السرية لا تعرض/تعدل هنا؛ reset SA + password؛ لا defaults recovery'
 if c=='Reports':return 'صلاحيات أنواع البيانات كلّها مطلوبة (AND)؛ dashboard ليس مفتاح المالية/الإشغال'
 if c=='LegacyPortal':return 'legacy raw SQL مغلق 403؛ استخدم TenantPortal المملوك'
 return 'staff global بالنسبة للمفتاح؛ معرفات وعلاقات من DB؛ لا حقوق من role/module؛ 404 بعد السماح للمورد غير الموجود'
rows=[]
for f in sorted((API/'Controllers').glob('*.cs')):
 s=f.read_text(encoding='utf-8-sig'); decl=re.search(r'public (?:sealed )?class (\w+)Controller',s); assert decl, f; c=decl.group(1)
 prefix_m=re.search(r'\bRoute\("([^"]+)"\)',s);prefix=prefix_m.group(1) if prefix_m else ''
 prefix=prefix.replace('[controller]',c)
 classpart=s[:decl.start()]
 cr=','.join(re.findall(r'Authorize\(Roles\s*=\s*"([^"]+)"',classpart))
 pattern=r'((?:^[ \t]*\[[^\n]+\](?:[ \t]*//[^\n]*)?\s*)+)\s*public\s+(?:async\s+)?[\w<>?,]+\s+(\w+)\s*\('
 for attrs,m in re.findall(pattern,s,re.M):
  http=re.search(r'\[Http(Get|Post|Put|Patch|Delete|Head|Options)(?:\((.*?)\))?\]',attrs)
  if not http:continue
  name=c+'.'+m; assert name in rules,'unclassified '+name
  r=rules[name];keys=[refs[ref] for ref in re.findall(r'Permissions\.\w+\.\w+',r)]
  ident=re.search(r'Identity\(IdentityPolicies\.(\w+)(?:, "([^"]+)")?',r)
  scope='Public' if 'Public()' in r else 'Denied' if 'Denied()' in r else ident.group(1) if ident else 'Staff'
  cap=ident.group(2) if ident else None
  rr=re.search(r'WithRoles\("([^"]+)"\)',r)
  ar=','.join(re.findall(r'Authorize\(Roles\s*=\s*"([^"]+)"',attrs))
  inherent={'Staff':'SuperAdmin,Admin,Accountant,GateKeeper','Tenant':'Tenant,TenantStaff','TenantOwner':'Tenant'}.get(scope,'')
  roles=role_intersection(inherent,cr,ar,rr.group(1) if rr else '') if scope!='Public' else []
  arg=http.group(2) or ''; tm=re.match(r'"([^"]*)"',arg);template=tm.group(1) if tm else ''
  route=template if template.startswith('/') else '/'+('/'.join(p for p in (prefix,template) if p))
  annotations=re.findall(r'HasPermission\((Permissions\.\w+\.\w+)\)',attrs)
  assert all(refs[k] in keys for k in annotations),'stale permission annotation '+name
  rows.append(dict(http=http.group(1).upper(),route=route,action=name,scope=scope,permissionKeys=keys,roles=roles,
                   capability=cap,idempotency='IsIdempotent = true' in r,resource=context(name,scope,cap)))
seen={r['action'] for r in rows};assert seen==set(rules),'inventory mismatch '+str(set(rules)-seen)
json_text=json.dumps(rows,ensure_ascii=False,indent=2)+'\n'
md='''# جدول جميع endpoints الفعلية — API / Angular\n\nمولّد من controllers و`EndpointSecurityCatalog`. الصياغة لا تحل محل اختبار MVC عند التشغيل. المفاتيح في نفس الخانة **AND** لا OR؛ SuperAdmin الحالي يتجاوز المفاتيح المعروفة فقط لا checks المورد/الحالة. Role fence قيد إضافي لا منحة. `Identity.*` owner-only ليست صلاحية إدارية.\n\n- الموظف لديه نطاق المجمع كله للمفتاح الممنوح، وليس نطاق مستأجر مخفيًا. ليست هناك بنية فروع/تفويض جزئي جديدة.\n- `I` تعني `Idempotency-Key` إلزاميًا؛ كل mutation الأخرى أيضًا transactional لكن بلا replay. GET إنشاء PDF ليس replay transaction.\n- Angular: اربط القائمة/الزر/route guard بالمفتاح المحدد كاملًا؛ لا تستعمل module ولا role بديلاً. القرار النهائي في API. تحديثات DTO في `ANGULAR.ar.md`.\n\n| HTTP | المسار | Permission (كلها) / scope | role إضافي فعلي | resource/context + Angular | I |\n|---|---|---|---|---|---|\n'''
for r in rows:
 keys=' + '.join(r['permissionKeys']) or ('portal:'+str(r['capability']) if r['capability'] else r['scope'])
 md+='| '+r['http']+' | `'+r['route']+'` | `'+keys+'` | '+(', '.join(r['roles']) or 'live authenticated / public حسب scope')+' | '+r['resource']+' | '+('✓' if r['idempotency'] else '—')+' |\n'
md+='''\n## غير MVC\n\n- `/hubs/notifications` (negotiate/websocket): authenticated live identity؛ `RegisterAdmin(userId)` و`RegisterTenant(tenantId,userId)` يطابقان DB owner؛ invocations غير مصنّفة تُمنع؛ type permission/capability يفحص عند الإرسال. مجموعات user+stamp+version لا مجموعات role يختارها العميل.\n- Swagger في Development فقط؛ لا static uploads ولا database init/recovery public. `WeatherForecast` sample مغلق. Scheduled jobs داخل process موثوق، ليست endpoints ولا تستمد صلاحيات JWT من عميل.\n- download demand يطلب `Demands.GeneratePdf` للموظف أو owning tenant مع statement capability؛ مرفق حوالة BankTransfers.View / owning tenant payments؛ metadata legacy default-deny.\n- المالي المشروط: إنشاء payment on-account/overflow يحتاج DepositAdvance، charge allocation يحتاج SettleCharge؛ maintenance billing/expense تحتاج مفاتيح مالية/expense مستقلة. BankTransfers.Review يحتاج CreatePayment/DepositAdvance/SettleCharge حسب النتيجة المالية. لا يبرر View أي side effect.\n'''
parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');args=parser.parse_args()
for path,content in ((DOCS/'endpoints.json',json_text),(DOCS/'ENDPOINTS.ar.md',md)):
 if args.check:assert path.exists() and path.read_text()==content,'outdated '+str(path)
 else:path.write_text(content)
print(f'{len(rows)} actions classified; {len(known)} exact keys; static contract parity OK')
