# Ready — Antigravity Plan (from TASK-001: customerId → Worker)
Дата: 2026-02-12  
Фокус: швидкий MVP, мінімум зайвого, максимум “можна показати/продати”.

---

## Принципи виконання (для Antigravity)
- Працюємо інкрементально: **кожен таск завершується runnable demo + перевірка**.
- Не ламаємо clean layering: Application ←(абстракції)→ Infrastructure.
- Уникаємо “перфекціонізму” до першого клієнта: тільки те, що дає продуктову цінність.

---

# TASK-001: customerId має доходити до Worker (зараз “demo-customer”)
**Мета:** Multi-tenant correctness. Workflow має виконуватись у контексті конкретного клієнта.

### Зміни
1) У `Ready.Worker.Worker` після `TryDequeueAsync`:
   - підняти `ReadyDbContext` зі scope
   - `db.Documents.Find(job.DocumentId)`
   - якщо doc null → `MarkFailedAsync(jobId, ...)` і continue
   - `customerId = doc.CustomerId`
2) Передавати `customerId` в `WorkflowExecutor.ExecuteAsync(...)` замість `"demo-customer"`
3) (Optional) Додати лог в `WorkflowExecutor` на старті: `customerId=...`

### Acceptance criteria
- Ingest з `customerId=acme`
- Worker log: workflow start містить `customerId=acme`
- `/status/{documentId}` повертає `document.customerId=acme`

---

# TASK-002: Прибрати EF spam + зробити polling “людяним”
**Мета:** нормальні логи/сигнал-шум, менше шуму від SELECT’ів.

### Зміни
- `Worker`: коли `job is null` → `Delay 3s`
- `Ready.Worker/appsettings.Development.json`:
  - `Microsoft.EntityFrameworkCore.Database.Command` → `Warning`
  - `Microsoft.EntityFrameworkCore` → `Warning`

### Acceptance criteria
- У логах немає постійних EF SQL команд щосекунди
- Worker видно як “живий”, а не “шумить”

---

# TASK-003: Idempotency інджесту (CustomerId + Sha256)
**Мета:** клієнт не платить двічі за той самий файл; ingestion без дублювання.

### Зміни
1) DB: unique index на `documents`:
   - `("CustomerId", "Sha256")` (з урахуванням фактичних назв колонок)
2) Ingestion:
   - при conflict → повертати існуючий `documentId`
   - не створювати новий job, якщо документ вже оброблявся (або створювати “reprocess” окремо)

### Acceptance criteria
- Два однакові файли для одного customer → один documentId
- Повторний ingest не плодить jobs

---

# TASK-004: Result metadata (стабільний ResultType/Version)
**Мета:** не залежати від CLR type name (перейменування класів не зламає consumers).

### Зміни
1) В Application:
   - ввести `IResultEnvelope { string ResultType; string Version; object Payload; }`
2) `StepOutcome.Result` повертати як `IResultEnvelope`
3) `WorkflowExecutor`:
   - якщо `Result is IResultEnvelope env` → `SaveAsync(runId, env.ResultType, env.Version, env.Payload)`
   - fallback (якщо не envelope) — тимчасово `CLR type name`

### Acceptance criteria
- В `results` зберігається стабільний `ResultType`, `Version`
- `/status` може підхоплювати `InvoiceExtract.v1` незалежно від імені класу

---

# TASK-005: “Invoice pipeline v1” (перший реальний результат)
**Мета:** показати цінність: з PDF/фото отримуємо `InvoiceExtractV1`.

### Workflow: `invoice:v1`
Steps (мінімально):
1) `text.extract` (P0 може бути stub)
   - якщо PDF text доступний → витягнути текст
   - якщо ні → placeholder/“no text yet”
2) `invoice.extract.v1` (LLM step)
   - input: extracted text
   - output: `InvoiceExtractV1`
3) `invoice.validate.v1`
   - checks: totals, VAT, currency presence
   - output: `ValidationReport.v1` (optional)
4) `export.json` (або `export.csv` якщо хочеш)
   - зберігає JSON/CSV як result

### Acceptance criteria
- Ingest invoice PDF → через Worker з’являється `InvoiceExtract` result у `results`
- `/status/{docId}` повертає `invoiceExtractV1` (parsed payload)

---

# TASK-006: File storage abstraction (dev local, prod pluggable)
**Мета:** щоб OCR/LLM steps могли читати файли за шляхом абстрактно.

### Зміни
- `IFileStorage`:
  - `SaveAsync(...) -> (storagePath, sha256, size)`
  - `OpenReadAsync(storagePath)`
- Infrastructure:
  - `LocalFileStorage` реалізує обидва
- Steps читають файл через storage, а не File.Open напрямую

### Acceptance criteria
- Step може відкрити файл по `StoragePath` і працювати з ним

---

# TASK-007: Job params (workflow customization per customer)
**Мета:** різні клієнти — різні правила/промпти/експорт.

### Зміни
1) DB migration: `jobs.ParamsJson` nullable
2) `IJobQueue.EnqueueAsync` приймає optional params
3) `StepContext.Items` заповнюється params
4) Steps читають params (наприклад: `language`, `vendorHints`, `exportFormat`)

### Acceptance criteria
- Ingest може передати параметри (мінімально — querystring або form field)
- Workflow враховує параметри (хоча б в одному step)

---

# TASK-008: API endpoints для “продуктового” UX
**Мета:** без UI вже можна інтегруватись.

Endpoints:
1) `GET /documents/{customerId}?take=20`
2) `POST /reprocess/{documentId}` (enqueue job повторно)
3) (Optional) `GET /results/{documentId}` (або по runId)

### Acceptance criteria
- Можна подивитись останні документи клієнта і повторно запустити обробку

---

# TASK-009: Безпека MVP (API key per customer)
**Мета:** хоча б базовий захист.

### Зміни
- `X-Api-Key` header
- mapping key → customerId (in config)
- ingestion/status/docs endpoints вимагають ключ

### Acceptance criteria
- Без ключа 401
- З ключем працює

---

# TASK-010: “Audit mode” (Antigravity automated)
**Мета:** інструментальний аудит, щоб не губитись у файлах.

### Antigravity instructions
- Run a repo scan:
  - find `AddReadyApplication`, `AddReadyInfrastructure`, `WorkflowExecutor`, `Worker`, `ReadyDbContext`
- Produce:
  1) Dependency graph (who depends on DbContext)
  2) DI lifetimes list
  3) Endpoints list in API
  4) Workflows + Steps registry list
- Output “Audit Report” markdown file.

---

## Рекомендований порядок виконання (найшвидше до грошей)
1) TASK-001 (customerId)
2) TASK-003 (idempotency)
3) TASK-004 (result envelope)
4) TASK-005 (invoice extract v1)
5) TASK-008 (docs list + reprocess)
6) TASK-009 (API key)

---

## Definition of Done для MVP v0
- Ingest invoice → отримати `InvoiceExtractV1` через `/status`
- Повторний ingest того ж файлу не створює дублі
- Є list documents + reprocess
- Є API key
- Worker стабільний (retry/backoff)

---