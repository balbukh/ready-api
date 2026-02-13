# Ready — Context Pack for Antigravity IDE
Дата: 2026-02-12  
Мета: перенести контекст розробки, зафіксувати поточний стан MVP, провести аудит і визначити наступні кроки.

---

## 0) Продуктова ідея (MVP)
Ready — сервіс для обробки документів (початково: бухгалтерія / інвойси):
- Ingest (API; пізніше Telegram) приймає файли
- Async job у черзі (Postgres)
- Worker виконує workflow (кроки/степи)
- Результати пишуться в БД як `results` (JSON)
- Є endpoint `status` для відстеження прогресу і результатів

Ключовий контракт бізнес-результату для бухгалтерії: `InvoiceExtractV1` (в Domain).  
Мета найближча: швидко вийти на демо “завантажив файл → отримав structured output”.

---

## 1) Архітектура / Solution структура
Solution: `Ready.sln`  
Проєкти:
- `src/Ready.Domain`
  - чисті доменні контракти: `Document`, `Run`, `StepRun`, `InvoiceExtractV1`, etc.
- `src/Ready.Application`
  - workflow engine: registry + executor + step abstractions
  - DI extension `AddReadyApplication()`
  - контракти для черги/ранів/результатів (IJobQueue, IRunStore, IResultStore)
- `src/Ready.Infrastructure`
  - EF Core + Postgres `ReadyDbContext`
  - entities: `DocumentEntity`, `JobEntity`, `RunEntity`, `StepRunEntity`, `ResultEntity`
  - stores: `RunStore`, `JobQueue`, `DbResultStore` (scoped)
  - DI extension `AddReadyInfrastructure(connectionString)`
- `src/Ready.Worker`
  - BackgroundService loop: dequeue job → load document → execute workflow → mark job done/failed
  - створює scope на кожну ітерацію (важливо)
  - є DbMigrator (dev) для авто-міграцій
- `src/Ready.Api`
  - API ingestion endpoint already implemented
  - status endpoint already implemented
- Tests:
  - `tests/Ready.UnitTests`
  - `tests/Ready.IntegrationTests`

---

## 2) Поточний стан (що вже працює)
### 2.1 Workflow engine
- Registry (in-memory): workflow `invoice` version `v1`
- Step `echo` виконується і повертає простий payload
- `WorkflowExecutor`:
  - створює `Run` (через `IRunStore`)
  - створює `StepRun` для кожного step
  - зберігає result в `results` (через `IResultStore`)
  - проставляє succeeded/failed

⚠️ Важливе: `WorkflowExecutor` має бути **scoped**, бо використовує scoped EF залежності.

### 2.2 Postgres queue (jobs table)
- таблиця `jobs` є
- Worker dequeues job і обробляє
- job переходить у Done
- записи з’являються в `runs`, `step_runs`, `results`

### 2.3 API Ingestion
- реалізовано `POST /ingest/{customerId}/{workflowName}` (multipart/form-data)
- створює document і enqueue job

### 2.4 Status Endpoint
- реалізовано `GET /status/{documentId}`
- повертає document + latest run + steps + results + (якщо є) invoiceExtractV1 parsed з JSON

---

## 3) Як запускати локально (Dev)
### 3.1 Postgres
`ops/docker-compose.yml`:
- postgres:16
- db/user/pass: ready/ready/ready
Запуск:
```bash
docker compose -f ops/docker-compose.yml up -d

