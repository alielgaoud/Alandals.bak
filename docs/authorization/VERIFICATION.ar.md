# التحقق والاختبارات والقياس

## ما نُفّذ فعليًا في هذه البيئة

| تحقق | النتيجة الفعلية |
|---|---|
| وجود .NET SDK/compiler | غير متاح؛ `dotnet: command not found` |
| SDK download | فشل HTTPS إلى Microsoft/NuGet؛ لم يُثبت SDK |
| C# tree-sitter | آخر تشغيل: 262 ملفًا دون syntax errors (0)؛ ليس type check/compile |
| static controller inventory | 181 action، 80 exact keys، جميعها مصنّفة وcatalog JSON مطابق |
| frozen designer/snapshot source body | مطابق نصيًا + metadata discovery موجودة؛ ليس EF runtime parity |
| XML/JSON + git whitespace | static checks؛ لا startup/DI/SQL validation |
| build/xUnit/SQL/migration/app startup | **لم تُنفّذ** |
| production source provenance/reconciliation | **لم تُفحص** |
| authorization/load p50/p95/p99/revoke/cache/db results | **غير مقاسة**؛ لا أرقام مختلقة |

CI workflow مضاف لكنه لم يشغّل/تُرصد نتيجته هنا. كل test مصدري غير منفذ؛ قد يكشف compile/model/runtime bugs ويتعين إصلاحها قبل release.

## تشغيل reproducible

```bash
pip install tree-sitter==0.26.0 tree-sitter-c-sharp==0.23.5
python tools/validate_authorization_source.py
# regenerate documentation intentionally if an endpoint changes:
python tools/authorization_inventory.py

# .NET8؛ .sln التقليدي مضاف للتوافق مع SDK8/WebApplicationFactory؛ .slnx محفوظ.
dotnet restore Andalos.API.Tests/Andalos.API.Tests.csproj
dotnet build Andalos.API.Tests/Andalos.API.Tests.csproj -c Release --no-restore
dotnet test Andalos.API.Tests/Andalos.API.Tests.csproj -c Release --no-build \
  --logger 'trx;LogFileName=authorization.trx' --results-directory artifacts/tests
```

بدون ANDALOS_TEST_SQL اختبارات SQL Server **Skipped** وليست Pass. CI ينشئ SQL Server2022 disposable بأسرار عشوائية في runtime، ويتطلب تشغيل tests؛ أخرج نتائج TRX والـSQL generation وbench smoke artifacts. راجع log restore/dependencies وimage digest/runtime hardware قبل مقارنة قياسات؛ image tag في CI ليس شهادة version للإنتاج. tests تعزل DB names `AndalosAuthorizationTests_*` وتزيلها فقط؛ لا production DB schema/data.

لتشغيل SQL tests محليًا: جهّز SQL Server disposable ثم احقن `ANDALOS_TEST_SQL` connection string من secret store خارج repo. تطبيق tests يستخدم **database جديدة عشوائية**، ويحتاج حق CREATE/DROP DATABASE؛ لا تستخدم credential إنتاج. لا تضع password في CLI/history أو ترفع artifacts مع أسرار.

```bash
dotnet test Andalos.API.Tests/Andalos.API.Tests.csproj -c Release \
 --filter FullyQualifiedName~SqlServerTests --logger 'trx;LogFileName=sql.trx' --results-directory artifacts/sql
# deployment config المؤقتة من secret manager كذلك لازمة لـEF design commands (JWT/valid connection string).
dotnet tool install --global dotnet-ef --version 8.0.26
dotnet ef migrations has-pending-model-changes --project Andalos.API
dotnet ef migrations script 20260926230000_AddChargeApprovalWorkflow 20260929190000_DetailedAuthorization \
 --idempotent --project Andalos.API -o artifacts/authorization-up.sql
```

## مصفوفة القبول الموجودة في tests

| شرط مطلوب | اختبار/مكان source |
|---|---|
| Admin Units.View read-only / View≠History | HttpAuthorizationTests.AdminWithOnlyUnitsViewCannotWriteByDirectHttp |
| empty grant/role/module deny، 401≠403 | RoleAndModuleNamesGiveNoEmployeeGrants, AnonymousIs401AndAuthenticatedMissingPermissionIs403 |
| login permissions arrays وعدم default | LoginAlwaysIncludesActualPermissionArraysAndUtcExpiration |
| Visitors.View لا scan/create | VisitorsViewDoesNotAllowCreationScanOrGateLogs |
| settle/top-up الاتجاهان | SettlementAndTopupKeysAreIndependent |
| payment reader لا create/delete/refund/review | PaymentReaderCannotCreateCancelRefundReviewOrReadBalances |
| DeletePayment لا DeleteRefund | PaymentCancellationNeverImpliesRefundCancellation |
| tenant subtype data keys/dashboard sensitivity | TenantViewDoesNotExposeNestedFinanceContractsOrVisitors, DashboardKeyAloneCannotDiscloseFinancialData |
| overlap/direct بعد سحب/تعطيل باقات؛ session حي | PackageWithdrawalAndDeactivationPreserveIndependentSourcesAndInvalidateOldSession |
| activity layers | InactivePackageLinkAndItemNeverGrant |
| Admin لا self elevation/out-of-scope/package elevation | AdminCannotPromoteSelfManageOtherUsersOrCreateEscalatingPackage + SuperAdminCannotEditOwnRoleOrPermissionsAndRejectsUnknownKeys |
| privileged register ممنوع | PublicRoleBearingRegistrationIsDisabled |
| lock/role JWT stale | PermanentLockRejectsExistingTokenAndCannotBeUndoneByLogin, ARoleClaimNotMatchingTheLiveDatabaseCannotBypassPermissions |
| tenant isolation foreign URL/body/header/file | TenantCannotChangeIdReadOthersFilesImpersonateHeadersOrReceiveEmployeePermissions, DifferentTenantChargeIdIsRejectedDespiteCorrectTenantIdInUrl |
| portal staff isolation | TenantStaffCannotAcquireEmployeeGrantsOrCreateMoreStaff |
| جميع endpoint classifications/new action deny | CatalogAndUnitTests + RuntimeMvcDescriptorsExactlyMatchSecurityCatalogue + static drift tool |
| explicit []/omitted/null/modules semantics | PackageRequestPresenceHasUnambiguousMeaning, EmptyKeysNeverExpandModulesAndNullUnknownKeysAreRejected |
| passwords/redaction | PasswordHashesAreSaltedAndLegacyHashesUpgrade, PasswordsAndStampsAreNeverWrittenIntoAuditValues |
| notification malformed group/data isolation | MalformedAllTenantsAndForeignDirectNotificationRowsDoNotLeak |
| SSRF provider fence | PushEndpointsDoNotBecomeArbitrarySsrf |
| upgrade/discovery/model and no legacy guessing | SqlServerTests.AllMigrationsApplyAndFrozenSnapshotMatchesCurrentModel + UpgradePreservesMixedLegacyRowsButDoesNotInventDirectGrantsOrHistoricalCash |
| concurrency direct/package بنفس witness وDB-down مع cache دافئ | ConcurrentDirectAndPackageReplacementWithSameWitnessCannotBothCommit, WarmPermissionCacheNeverAllowsAccessWhenIdentityDatabaseIsUnavailable |
| transaction/replay concurrency وblacklist/amount | ConcurrentSameKeyTopupCreditsExactlyOnceAndChangedRequestIs409, BlacklistedPassCannotBeToppedUpAndInvalidAmountsLeaveNoEffects |

SQLite HTTP tests فحص سياسة ومورد خفيف؛ لا تثبت decimal aggregations/locking/translation المالي على SQL Server. coverage تتبع descriptor الفعلي عند startup وتمنع action غير مصنّفة. unitReflection لا يغني عنها. لازم توسيع finance posting/reversal/refund sums/file integration/hub multi-node/load/in-flight race على restored dataset؛ الأدوات والـsource ليست تغطية تشغيل كاملة لكل سياسة تجارية.

## قياس authorization باثنتين من API replicas

```bash
# same secret-injected ANDALOS_TEST_SQL pointing to disposable server
mkdir -p artifacts
dotnet run -c Release --project tools/Authorization.Bench -- 100 10000 32 > artifacts/authorization-bench.json
# different user/cache pressures and contention:
dotnet run -c Release --project tools/Authorization.Bench -- 1000 100000 64 > artifacts/authorization-bench-1000.json
dotnet run -c Release --project tools/Authorization.Bench -- 4500 100000 128 > artifacts/authorization-bench-eviction.json
```

Harness يستعمل real JWT/DI/DB query/version invalidation، API test hosts اثنين فوق SQL DB واحدة عشوائية؛ يزيل DB الاختبار فيfinally. seeds/warmup لا ضمن sample. يقرأ `/api/Units` مع Units.View، يخرج p50/p95/p99 لـ:
- `authorizationDecisionMs`: بعد framework signature validation وقبل controller، يشمل live identity/role/exact keys/cache وDB miss؛ لا يشمل JWT signature crypto.
- `permissionCheckMs`: key handler فقط، لا identity query؛ قد يتكرر إذا عدة keys/annotations.
- `inProcessHttpMs`: كامل HTTP test pipeline/business read؛ ليس شبكة/TLS/proxy الإنتاج.
- logical identity/union counters وcache hits/misses/hit ratio؛ **ليست كل SQL commands** للbusiness/audit. استخدم SQL Extended Events/Query Store/command interceptor في بيئة القياس لعدد commands الفعلي وتأكيد union واحد لا N+1.
- commit-to-both-denied latency للتوكن نفسه بعد سحب grant، ويرفض النتيجة إن لم تكن 403 في العقدتين. smoke CI لا يثبت capacity/realtime multi-node.

Warm steady-state من المصدر متوقع identity=طلبات authenticated، union≈0/hit، مع miss+identity≈2 للقرار؛ هذه **توقعات** لا نتائج. قم بقياس cold cache/revoked/denied/role bypass والـpackage disable كبيرة والجداول الكبيرة وفشل DB (مع cache دافئ يجب ألا يجيز أي طلب). لا يوجد latency SLA يُدعى تحققه.

## HTTP/network load على staging مماثل للإنتاج

جهّز comptes اختبار متعددة بحزم/direct معروفة بلا سجل إنتاج؛ tokens fixture JSON خارج repo بحقوق0600 لا password. كل run تعرف expected status/key، المسار الذي تختبره يجب أن يناسب التوكنات، target path مطلوب حتى لا تختبر مجرد self identity بدل action permission.

```bash
TOKENS_FILE=/restricted/fixtures/tokens.json BASE_URL=https://staging.example.invalid \
TARGET_PATH=/api/Units EXPECTED_STATUS=200 VUS=32 DURATION=120s \
k6 run --summary-export artifacts/k6-unit-read.json scripts/authorization/load.js
# negative readers/no key, route-specific financial deny expected403, revoked sessions expected401:
# أعد الإعداد بتوكنات/مسار وstatus صحيح، لا تستخدم token privileged لجميع الحالات.
```

اجمع runtime/CPU/RAM/DB version+collation+row counts/index plans/concurrency/replica routing/network والبواقي metrics من Meter `Andalos.Authorization` عبر exporter/diagnostic listener خارجي؛ المشروع لا يضيف endpoint metrics عامًا بلا حماية. قِس load مع writer revocations أثناء الطلبات، سجل وقت commit و**كل** قرار لاحق؛ in-flight precommit reads لا تقارن بوعد ما بعد commit. DB-down test يجب أن يفشل مغلقًا رغم cache دافئ. ولا network/staging/load command نُفّذ هنا.
