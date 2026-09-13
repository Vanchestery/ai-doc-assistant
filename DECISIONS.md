# DECISIONS.md — дневник архитектурных решений

Формат: развилка → варианты → что выбрали и почему.

English version for reviewers: **[DECISIONS.en.md](DECISIONS.en.md)**.

Решения перенесены из исходного прототипа в rebuild (`ai-doc-assistant`): та же архитектура, стек обновлён до **.NET 10**, UI-проект — Blazor Web App в папке `ai-doc-assistant/`.

---

## Фаза 0

### 1. Структура solution: Clean Architecture (Core / Infrastructure / Web)

**Варианты:** монолит одним проектом · Clean Architecture · Vertical Slice Architecture.

**Выбрали Clean Architecture.** Ядро проекта — подменяемые абстракции (`ILlmProvider`, `IVectorStore`): интерфейсы живут в Core, реализации (DeepSeek, pgvector) в Infrastructure, замена провайдера = новый класс + строчка DI. Это и есть model-agnostic дизайн. Плюс это стандарт .NET-энтерпрайза — узнаваем на собеседованиях. Сознательно НЕ плодим лишние слои (отдельного Application-проекта нет, бизнес-логика в Core). Vertical Slice отклонили: хорош для больших команд/CQRS, но хуже показывает нашу главную идею. Монолит — не уровень портфолио. В rebuild: `src/AiDocAssistant.Core`, `src/AiDocAssistant.Infrastructure`, host `ai-doc-assistant/`, плюс `src/AiDocAssistant.Mcp` и `tests/AiDocAssistant.Tests`.

### 2. Векторное хранилище: pgvector (не Pinecone/Qdrant/Weaviate)

**Варианты:** pgvector · выделенные векторные БД (Pinecone — облачная, Qdrant/Weaviate — self-hosted).

**Выбрали pgvector.** Реляционные данные и векторы в одной БД: одна транзакция, один бэкап, один docker-контейнер, знакомый Postgres. Для объёмов портфолио-проекта (тысячи чанков) производительности с запасом. Выделенные векторные БД оправданы на десятках миллионов векторов или при специфичных фичах (гибридный поиск из коробки и т.п.). Pinecone дополнительно отпадает: облако, платно, доступ из РФ.

### 3. Postgres+pgvector в Docker: готовый образ `pgvector/pgvector:pg16`

**Варианты:** свой Dockerfile поверх `postgres` · готовый образ pgvector · managed-облако.

**Выбрали готовый образ** — это стандартный postgres с уже скомпилированным расширением. `CREATE EXTENSION vector` выполняется EF-миграцией (см. п. 4), а не init-скриптом compose: расширение ставится на базу, не на сервер, и через миграцию оно версионируется вместе со схемой.

### 4. Применение EF-миграций: автоматически при старте приложения

**Варианты:** вручную `dotnet ef database update` · автомиграция на старте · отдельный migration-контейнер.

**Выбрали автомиграцию** (`db.Database.Migrate()` в Program.cs): демо должно подниматься одной командой `docker-compose up`. Trade-off осознан: при нескольких репликах это гонка, в проде миграции выносят в отдельный шаг CI/CD. У нас реплика одна.

### 5. Начальная миграция написана вручную

В среде разработки ассистента нет .NET SDK, поэтому первая миграция (включение расширения vector, модель пустая) написана руками в формате, который генерирует `dotnet ef migrations add`. Дальнейшие миграции с реальными таблицами будем генерировать командой `dotnet ef` на машине разработчика — ручная поддержка snapshot-файла при живой модели ошибкоопасна.

---

## Фаза 1

### 6. PDF-текст: PdfPig

**Варианты:** iText 7 (AGPL/платно) · Aspose.PDF (платно) · PDFium-обёртки (нативные dll, заточены под рендеринг) · PdfPig (Apache 2.0, чистый C#).

**Выбрали PdfPig:** ровно наш сценарий (извлечение текста), свободная лицензия, ноль нативных зависимостей — одинаково работает на Windows-машине разработчика и в Linux-контейнере.

### 7. OCR: Tesseract через CLI-процесс

**Варианты:** облачные Vision API (точнее, но платно/недоступно из РФ, данные уходят наружу) · LLM-vision · Tesseract локально. Внутри Tesseract: .NET-обёртка (нативные dll, боль на Linux) vs вызов CLI.

**Выбрали Tesseract CLI:** бесплатно, локально (документы не покидают сервер), rus+eng. CLI вместо обёртки — меньше хрупких нативных зависимостей: на Windows инсталлятор, в Docker `apt-get install tesseract-ocr`. Скан-PDF растеризуется в PNG утилитой pdftoppm (poppler) и распознаётся постранично.

### 8. Доступ к LLM: свой тонкий HttpClient за ILlmProvider

**Варианты:** Semantic Kernel (комбайн, скрывает механику) · Microsoft.Extensions.AI (готовая абстракция IChatClient) · OpenAI SDK с подменой BaseUrl · свой клиент (~100 строк).

**Выбрали свой клиент:** ILlmProvider в Core — спроектированная нами точка подмены модели (DeepSeek -> Ollama = один класс), полный контроль над телеметрией (токены/latency — фундамент метрик Фазы 4), ноль лишних зависимостей. DeepSeek OpenAI-совместим, протокол — один POST.

### 9. Строгий JSON от LLM: JSON-mode + схема в промпте + валидация + 1 retry

**Варианты:** Structured Outputs с json-схемой (гарантия структуры, но это фича OpenAI — у DeepSeek нет) · function calling · JSON-mode + валидация на своей стороне.

**Выбрали JSON-mode + валидацию:** `response_format: json_object` гарантирует синтаксис, состав полей проверяем кодом (обязательные ключи), при браке — один retry с текстом ошибки. Самый переносимый паттерн — работает с любой моделью. Нюансы: JSON-mode требует слова «json» в промпте; валидация обязательна, т.к. состав полей не гарантирован.

### 10. Файлы: локальный диск за IFileStorage

**Варианты:** bytea в Postgres (раздувает БД) · S3/MinIO (прод-паттерн, но лишний контейнер сейчас) · диск + docker volume.

**Выбрали диск.** Имена файлов на диске генерируются (Guid), пользовательское имя не участвует в пути — защита от path traversal. MinIO — кандидат в «что дальше».

### 11. Обработка документа: синхронно в запросе

**Варианты:** фоновая очередь (BackgroundService/Hangfire) · синхронно.

**Выбрали синхронно:** секунды на документ, демо нагляднее. Поле Document.Status уже готово к переводу в фоновую обработку — записано в «что дальше».

## Фаза 2 — RAG

### 12. Чанкинг: рекурсивный сплиттер по разделителям с перекрытием

**Развилка:** как резать извлечённый текст документа на фрагменты под эмбеддинги и векторный поиск.

**Варианты:** фиксированный размер (по токенам/символам) · по структуре (абзацы/предложения) · рекурсивный сплиттер по иерархии разделителей · семантический (границы по эмбеддингам).

**Почему не они:** фиксированный режет по счётчику и рвёт строки/суммы посреди — теряется смысл на границе; чисто структурный уважает границы, но даёт чанки очень неравной длины (одна строка vs целый раздел) и может превысить лимит эмбеддера; семантический точнее всех, но требует лишних вызовов модели на этапе индексации — дорого и преждевременно (кандидат в Фазу 4).

**Выбрали рекурсивный сплиттер + overlap** (принцип RecursiveCharacterTextSplitter, дефолт индустрии). Идёт по иерархии `\n\n → \n → ". " → " " → символ`: режет по самой крупной естественной границе, что влезает в лимит, мелкие куски склеивает до нужного размера. Для наших документов (текст извлечён построчно: позиции, суммы, реквизиты) это держит связанные строки вместе. Overlap (перекрытие ~15%) переносит хвост предыдущего чанка в начало следующего, чтобы факт на стыке не потерялся при поиске.

**Размер в символах, не в токенах:** без внешнего токенайзера, остаётся model-agnostic (эмбеддер ещё не выбран). Стартовые значения `MaxChars=1000, Overlap=150` (~250–300 токенов). Точный подсчёт токенов — кандидат в «что дальше» (Фаза 4, оптимизация). Стратегия за интерфейсом `ITextChunker` — можно будет A/B-сравнить в evals.

### 13. Эмбеддинги: локальная модель через OpenAI-совместимый эндпоинт (Ollama, bge-m3)

**Развилка:** чем превращать чанк в вектор.

**Варианты:** тот же DeepSeek · облачный embeddings-API (OpenAI text-embedding-3, Voyage, Jina) · локальная модель через Ollama (bge-m3 / multilingual-e5).

**Почему не они:** у DeepSeek нет надёжного, задокументированного embeddings-эндпоинта (официальная дока — только chat; «embeddings» встречается лишь в неофициальных статьях и открытом feature-request) — на этом нельзя строить. Облачные API качественные, но часть недоступна/неудобна из РФ по оплате, и главное — эмбеддинги считаются на КАЖДОМ чанке КАЖДОГО документа, счёт быстро растёт.

**Выбрали локальную модель через Ollama, `bge-m3` (1024-мерный, мультиязычный, сильный на русском).** Плюсы для нас: доступно из РФ (всё локально, без внешнего API), бесплатно (нулевая маржинальная стоимость — хорошая история для метрик Фазы 4), приватно (текст документа не уходит наружу — важно). Доступ спрятан за `IEmbeddingProvider` и реализован через OpenAI-совместимый `/v1/embeddings`, поэтому подменить на облако = сменить BaseUrl/Model в конфиге. Минус (осознанный): нужна запущенная Ollama и `ollama pull bge-m3` — отдельный шаг настройки, как Tesseract в Фазе 1.

### 14. Хранение и поиск вектора: pgvector + EF Core, HNSW + косинус

**Развилка:** как хранить вектор в Postgres и искать ближайшие.

**Варианты хранилища:** отдельная векторная БД (Pinecone/Qdrant/Weaviate) · pgvector в том же Postgres. **Индекс:** HNSW vs IVFFlat. **Оператор:** косинус vs L2 vs inner product.

**Почему не отдельная БД:** Postgres уже в стеке — не тащим лишний сервис ради нашего масштаба; pgvector даёт векторный поиск в той же транзакции и тех же миграциях. Pinecone/Qdrant оправданы на десятках миллионов векторов — не наш случай.

**Выбрали pgvector + EF Core.** Тип колонки `vector(1024)`, тип в C# — `Pgvector.Vector` (пакет `Pgvector.EntityFrameworkCore`, `UseVector()` в Npgsql). Поиск — `CosineDistance` (оператор `<=>`), EF транслирует в SQL. Индекс — **HNSW** (`vector_cosine_ops`): точнее и быстрее IVFFlat на чтении, не требует предварительного обучения на данных (IVFFlat нужно строить уже поверх наполненной таблицы). **Косинус** — стандарт для текстовых эмбеддингов (нормализованные векторы, важно направление, а не длина). Нюанс чистоты: тип `Vector` живёт в сущности `Chunk` (Core ссылается на крошечный пакет `Pgvector`) — сознательное отступление от «чистого POCO» ради EF-транслируемого поиска; EF-интеграция при этом только в Infrastructure.

### 15. RAG-ответ: retrieve → augment → generate, цитаты из поиска

**Развилка:** как собрать ответ на вопрос по документам.

**Варианты:** только keyword/BM25 · гибридный поиск (BM25 + vector) · классический RAG (vector top-K → промпт → LLM) · GraphRAG / agentic RAG с несколькими шагами поиска.

**Почему не они:** BM25 без векторов хуже на перефразированных вопросах («сколько всего» vs «итоговая сумма»); гибрид и GraphRAG — следующий уровень сложности, оправдан при больших корпусах и evals (Фаза 4); agentic RAG — преждевременно до tool-use в Фазе 3.

**Выбрали классический RAG:** вопрос → эмбеддинг → top-K чанков из pgvector → фрагменты в промпт → ответ DeepSeek. История сессии (последние N сообщений) добавляется в контекст для уточняющих вопросов. **Цитаты** — не «на слово» от модели, а **ground truth**: в ответ API возвращаем ровно те чанки, которые нашли поиском (документ, номер фрагмента, excerpt, distance). Модель может ссылаться [1][2] в тексте, но источник прозрачен для пользователя независимо от неё.

### 16. Сессии чата: Postgres, не in-memory

**Развилка:** где хранить диалог.

**Варианты:** in-memory (Dictionary) · Redis · Postgres (те же таблицы, что Documents/Chunks).

**Почему не in-memory/Redis:** in-memory теряется при перезапуске — плохо для демо; Redis — лишний сервис в docker-compose ради портфолио-масштаба.

**Выбрали Postgres:** `ChatSession` + `ChatMessage`, цитаты assistant-сообщений — jsonb. Одна БД, одна транзакция, миграции EF. Интерфейс `IChatSessionStore` в Core, реализация `EfChatSessionStore` в Infrastructure.

### 17. Область поиска: все документы + опциональный фильтр

**Развилка:** искать по всему индексу или только по одному документу.

**Варианты:** только глобальный поиск · только per-document · оба режима через параметр запроса.

**Выбрали оба:** по умолчанию top-K по всем `Chunks` (вопросы вроде «сколько всего за март?»); опциональный `documentId` в теле запроса — «только этот PDF». Фильтр на уровне SQL до OrderBy, индекс HNSW по-прежнему используется.

## Фаза 3 — agent tool-use

### 18. API агента: сначала явный tool, потом goal

**Развилка:** как клиент вызывает агента.

**Варианты:** только `{ tool, documentIds }` · только `{ goal: "сверь..." }` · оба режима.

**Почему не сразу goal:** tool calling DeepSeek нужно обкатать; reconcile — детерминированный C# по JSON, явный API проще тестировать и дебажить; goal-оркестратор добавим вторым шагом той же фазы.

**Выбрали явный tool** (`POST /api/agent/tasks`). Реестр `IAgentTool` + `AgentToolRegistry`. Goal → LLM выбирает tool — позже.

### 19. Отчёт generate_report: xlsx (ClosedXML)

**Развилка:** формат отчёта.

**Варианты:** CSV · xlsx · PDF.

**Выбрали xlsx** — ближе к бэк-офису и ТЗ; CSV — fallback; PDF — «что дальше». Реализация tool — следующий коммит после summarize.

### 20. Данные для tools и порядок: ExtractionResult, reconcile первым

**Развилка:** откуда tools читают данные и с чего начать.

**Варианты:** повторный парсинг PDF · JSON из ExtractionResult · RAG-чанки.

**Выбрали ExtractionResult.Json** — уже структурировано, без LLM/OCR. RAG — для поиска, не для сверки цифр.

**Порядок:** каркас (`AgentAction`, `AgentTaskService`) + **reconcile** → summarize → generate_report.

### 21. Tool summarize: компактные поля + LLM, не полный PDF

**Развилка:** как собрать сводку по нескольким документам.

**Варианты:** RAG по чанкам · повторный парсинг PDF · **структурированный JSON** из ExtractionResult → LLM · шаблон без LLM (string.Format).

**Почему не RAG/PDF:** для сводки нужны уже извлечённые поля (сумма, контрагент, дата) — они есть в JSON; RAG добавляет шум и стоимость; шаблон без LLM не гибок для формулировок «на русском, по делу».

**Выбрали:** `DocumentSummarizeService` собирает **компактный текст** из JSON каждого документа (без items целиком — только count), один вызов `ILlmProvider`. Результат: `summary` + метаданные (documentCount, totalAmountSum) в `resultJson`. Reconcile остаётся детерминированным; summarize — первый tool с LLM в агенте.

### 22. Tool generate_report: xlsx из JSON, два листа, без LLM

**Развилка:** что класть в отчёт и как отдавать файл.

**Варианты:** CSV (проще) · **xlsx два листа** (документы + позиции) · PDF · base64 в JSON.

**Выбрали:** `DocumentReportService` + `DocumentReportXlsxWriter` (ClosedXML). Лист «Документы» — ключевые поля extraction; лист «Позиции» — строки items. Файл сохраняется в `IFileStorage`, скачивание — `GET /api/agent/tasks/{id}/report`. Детерминированно, без LLM; источник — тот же `ExtractionResult.Json`.

### 23. Goal-mode: JSON-router, не native tool-calling

**Развилка:** как LLM выбирает tool по цели пользователя.

**Варианты:** native OpenAI tool/function calling · **JSON-mode router** (один вызов → `{ tool, reasoning }`) · keyword-эвристики без LLM.

**Почему не native tool-calling сразу:** `ILlmProvider` уже покрывает chat + JSON-mode; для трёх tools достаточно одного вызова-маршрутизатора; проще тестировать (FakeLlm с JSON). Native tools — при multi-step цикле (несколько tools подряд).

**Выбрали:** `POST /api/agent/goals` `{ goal, documentIds }` → `AgentGoalRouterService` → `AgentTaskService.RunAsync`. В `inputJson` задачи сохраняются `goal` и `routingReason`. Явный `POST /api/agent/tasks` остаётся для тестов и отладки.

## Фаза 4 — evals и метрики

### 24. Телеметрия LLM: декоратор + Postgres, не только логи

**Развилка:** где хранить токены/latency/стоимость.

**Варианты:** только `ILogger` · Prometheus/OpenTelemetry · **таблица `LlmUsageEvents` + API**.

**Выбрали:** `MeteringLlmProvider` — декоратор вокруг `DeepSeekLlmProvider`; каждый вызов с `LlmRequest.Operation` (`extraction`, `rag_chat`, `summarize`, `goal_router`). Стоимость — `LlmCostEstimator` по конфигурируемым USD/1M tokens. `GET /api/metrics/summary` — агрегаты + счётчики БД.

### 25. Evals v1: детерминированные кейсы без LLM в CI

**Развилка:** с чего начать evals при отсутствии golden LLM-run в CI.

**Варианты:** end-to-end с live LLM · offline golden JSON · **детерминированная логика + fixture JSON**.

**Выбрали:** `EvalSuiteService` — reconcile (match/mismatch) + проверка ключевых полей extraction JSON (invoice A/B). `GET /api/metrics/evals` и блок evals в summary. LLM-accuracy evals — следующий шаг (offline fixtures или recorded responses).

### 26. Golden extraction eval + latency p50/p95

**Развилка:** как мерить точность extraction без live LLM в CI.

**Варианты:** e2e upload PDF · **expected vs actual JSON** (recorded LLM output) · human review.

**Выбрали:** `ExtractionGoldenEval` — 7 полей (doc_type, number, date, total, currency, counterparty name/inn); golden-кейсы invoice A/B в `EvalSuiteService`; `goldenFieldAccuracyPercent` в ответе evals. Плюс **latency p50/p95** в `LlmUsageSummary` (общий и по operation) — следующий уровень после суммарного `totalLatencyMs`.

### 27. Evals v2: recorded fixtures, RAG retrieval, agent heuristic

**Развилка:** как расширить evals без live LLM в API.

**Варианты:** только e2e upload · **JSON fixtures + recorded hits** · Prometheus alerts.

**Выбрали:** embedded golden JSON (`expected` vs `actual`, нормализация «ООО»/«OOO»); `RagRetrievalEval` на recorded top-K hits (invoice A/B totals); `AgentGoalHeuristic` — offline baseline goal→tool для eval и регрессии router. **14** кейсов в `/api/metrics/evals`. Фаза 4 закрыта для портфолио.

## Фаза 5 — Blazor UI и деплой

### 28. Blazor Interactive Server в том же Web-проекте

**Развилка:** как добавить UI к уже готовому REST API.

**Варианты:** отдельный SPA (React) · Blazor WASM + отдельный хост · **Blazor Interactive Server в том же host-проекте** (`ai-doc-assistant/`).

**Выбрали:** один Docker-контейнер и один процесс — API + UI на `:8080`. UI ходит в REST через `HttpClient` (`DocumentsApiClient`), Swagger остаётся для отладки. Шаг 1: shell, главная, список/загрузка/детали документов. Дальше — чат, agent, metrics, деплой.

**Нюанс rebuild (Windows + VS F5):** системный диалог Browse file под Interactive Server иногда роняет процесс (`exited -1`). Для демо загрузки в отладчике предпочтителен drag-and-drop; полный conversational agent chat UI — отдельный шаг после тестов/доков.

### 29. RAG-чат UI: сессия в URL + фильтр по документу

**Развилка:** как встроить чат в Blazor без дублирования `RagChatService`.

**Варианты:** вызывать `RagChatService` из компонента · **REST через `ChatApiClient`** · WASM + отдельный BFF.

**Выбрали:** `ChatApiClient` → `POST/GET api/chat/sessions*`. Маршрут `/chat/{sessionId}` — история в URL; опциональный `?documentId=` для фильтра retrieval и deep link «Спросить по документу» со страницы деталей. После ответа — перезагрузка сессии, цитаты из API.

### 30. Agent UI: явный tool + goal-mode на одной странице

**Развилка:** как дать UI для трёх tools и goal-router.

**Варианты:** три отдельные страницы · wizard · **одна `/agent` с переключателем режима**.

**Выбрали:** `/agent` — чекбоксы документов (Extracted), radio «явный tool / goal»; `AgentApiClient` → `api/agent/tasks|goals`. Результат: status, JSON, ссылка на xlsx для `generate_report`. Ollama не нужна — только DeepSeek.

### 31. Metrics dashboard + prod compose

**Развилка:** как закрыть Фазу 5 (UI + деплой).

**Варианты:** отдельный Grafana · только Swagger · **Blazor `/metrics` + `docker-compose.prod.yml`**.

**Выбрали:** страница `/metrics` — LLM-агрегаты, p50/p95, счётчики БД, таблица 14 eval-кейсов (`MetricsApiClient` → summary). Деплой: `.env.example` + override `docker-compose.prod.yml` (`Production`, `restart: unless-stopped`). Dev-поток (`docker compose up --build`) без изменений.

## Фаза 6 — MCP-сервер

### 32. Отдельный stdio MCP-проект на ModelContextProtocol SDK

**Развилка:** как открыть AiDocAssistant для Cursor/Claude как MCP tools.

**Варианты:** HTTP MCP внутри Web (`ModelContextProtocol.AspNetCore`) · **отдельный console stdio-сервер** · thin HTTP-прокси к REST.

**Почему не HTTP в Web сразу:** stdio — нативный транспорт Cursor; отдельный процесс не тянет Kestrel и не смешивает MCP stdout с логами API. Общая бизнес-логика остаётся в Core/Infrastructure.

**Выбрали:** `AiDocAssistant.Mcp` — `Host` + `AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()`. DI дублирует Web (без Blazor/Swagger), те же `AgentTaskService`, `MetricsService`, `AppDbContext`. Tools: `list_documents`, `get_document`, `reconcile`, `summarize`, `generate_report`, `run_agent_goal`, `get_metrics_summary`, `list_agent_tools`, `get_agent_task`. Логи — stderr (протокол на stdout). Конфиг: `appsettings.json` + env/`user-secrets` для `DeepSeek:ApiKey`. HTTP MCP на Web — опционально позже.
