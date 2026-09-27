# 🔐 دليل أمان المفاتيح — Andalos API

> **القاعدة الذهبية:** لا مفاتيح سرية في المستودع — أبداً.

## ما تم نقله خارج appsettings.json

| المفتاح | الاستخدام |
|---|---|
| `ConnectionStrings:DefaultConnection` | اتصال قاعدة البيانات (كلمة المرور بداخله) |
| `JwtSettings:SecretKey` | مفتاح توقيع التوكنات |
| `VapidDetails:PrivateKey` | مفتاح إشعارات الويب Push |

(`PublicKey` والـ `Subject` عامّان بطبيعتهما — يبقيان في appsettings.json)

## أثناء التطوير (جهازك) — User Secrets

1. بعد أول `git pull` شغّل مرة واحدة:
   ```powershell
   powershell -ExecutionPolicy Bypass -File Scripts\setup-dev-secrets.ps1
   ```
   السكربت يقرأ القيم من تاريخ Git ويحفظها في مخزن محلي خارج المستودع.
2. أعد البناء والتشغيل (F5) — كل شيء يعمل كالسابق.

- لتعديل قيمة يدوياً:
  ```powershell
  dotnet user-secrets set "JwtSettings:SecretKey" "القيمة" --project Andalos.API
  ```
- لعرض المخزن: `dotnet user-secrets list --project Andalos.API`
- مكان التخزين الفعلي: `%APPDATA%\Microsoft\UserSecrets\a3f8c2e1-7b4d-4e9a-9c6f-2d1b8e5a7c30\secrets.json`

## عند النشر (IIS / السيرفر) — متغيرات بيئة

بنية الاسم: استبدل `:` بـ `__` (شرطتان سفليتان):

| متغير البيئة | مثال القيمة |
|---|---|
| `ConnectionStrings__DefaultConnection` | `Server=...;Database=AndalosDB;User Id=...;Password=...;...` |
| `JwtSettings__SecretKey` | مفتاح قوي 64+ حرفاً |
| `VapidDetails__PrivateKey` | المفتاح الخاص |

**طرق الإضافة:**

- **PowerShell (مستوى النظام — الأفضل):**
  ```powershell
  [Environment]::SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Server=...;...", "Machine")
  ```
  ثم أعد تشغيل IIS (`iisreset`).
- **web.config** داخل `<aspNetCore>`:
  ```xml
  <aspNetCore processPath="dotnet" arguments=".\AndalosAPI.dll" stdoutLogEnabled="false">
    <environmentVariables>
      <environmentVariable name="ConnectionStrings__DefaultConnection" value="Server=...;..." />
      <environmentVariable name="JwtSettings__SecretKey" value="..." />
      <environmentVariable name="VapidDetails__PrivateKey" value="..." />
    </environmentVariables>
  </aspNetCore>
  ```
  حدّد صلاحيات ملف web.config ببحيث لا يقرأه غير صاحب الخدمة.

## ⚠️ مهم — تدوير المفاتيح

القيم القديمة **بقيت في تاريخ Git** (كومِتات سابقة). المستودع خاص، لكن للأمان الكامل عند النشر:

1. غيّر كلمة مرور مستخدم قاعدة البيانات `andalos` من SQL Server، وحدّثها في متغيرات البيئة على السيرفر.
2. غيّر `JwtSettings:SecretKey` إلى مفتاح جديد عشوائي — **سيُسجَّل خروج الجميع** مرة واحدة (مقبول عند النشر الأول).
3. أعد توليد مفاتيح VAPID إن رغبت (سيحتاج المتصفحون لإعادة تفعيل الإشعارات).

## للتحقق أن كل شيء سليم

```powershell
dotnet user-secrets list --project Andalos.API   # في التطوير
echo $env:ConnectionStrings__DefaultConnection   # على السيرفر
```
