# DECISIONS.md — architecture decision log (English)

Format: fork → options → what we chose and why.

Companion to the Russian original: [DECISIONS.md](DECISIONS.md).

Carried from the prototype into the rebuild (`ai-doc-assistant`): same architecture, stack on **.NET 10**, Blazor Web App host in `ai-doc-assistant/`.

---

## Phase 0

### 1. Solution shape: Clean Architecture (Core / Infrastructure / Web)

**Options:** single-project monolith · Clean Architecture · Vertical Slice Architecture.

**Chose Clean Architecture.** The core idea is swappable abstractions (`ILlmProvider`, vector store interfaces): contracts live in Core, implementations (DeepSeek, pgvector) in Infrastructure — swapping a provider is a new class + one DI line. That is the model-agnostic design. It is also the .NET enterprise pattern interviewers recognize. We deliberately skip an extra Application layer; business logic stays in Core. Vertical Slice is great for large CQRS teams but weakens the “pluggable AI” story. A monolith is not portfolio-level. Rebuild layout: `src/AiDocAssistant.Core`, `src/AiDocAssistant.Infrastructure`, host `ai-doc-assistant/`, plus `src/AiDocAssistant.Mcp` and `tests/AiDocAssistant.Tests`.

### 2. Vector store: pgvector (not Pinecone / Qdrant / Weaviate)

**Options:** pgvector · dedicated vector DBs (Pinecone cloud, Qdrant/Weaviate self-hosted).

**Chose pgvector.** Relational data and vectors in one database: one transaction, one backup, one Docker container, familiar Postgres. Portfolio scale (thousands of chunks) has headroom. Dedicated vector DBs pay off at tens of millions of vectors or for specialized features (hybrid search out of the box). Pinecone also fails the “runs from RU / no paid cloud” constraint.

### 3. Postgres + pgvector in Docker: official `pgvector/pgvector:pg16`

**Options:** custom Dockerfile on `postgres` · official pgvector image · managed cloud.

**Chose the official image** — stock Postgres with the extension already built. `CREATE EXTENSION vector` runs in an EF migration (see #4), not a compose init script: the extension is per-database and versioned with the schema.

### 4. EF migrations: apply automatically on startup

**Options:** manual `dotnet ef database update` · migrate on startup · dedicated migration container.

**Chose auto-migrate** (`db.Database.Migrate()` in `Program.cs`): the demo must come up with one `docker compose up`. Trade-off is explicit: multiple replicas race; production should move migrations to CI/CD. We run a single replica.

### 5. Initial migration written by hand

The assistant environment had no .NET SDK, so the first migration (enable `vector`, empty model) was hand-written in the shape `dotnet ef migrations add` produces. Later table migrations are generated with `dotnet ef` on the developer machine — hand-editing the snapshot with a live model is error-prone.

---

## Phase 1

### 6. PDF text: PdfPig

**Options:** iText 7 (AGPL/paid) · Aspose.PDF (paid) · PDFium wrappers (native DLLs) · PdfPig (Apache 2.0, pure C#).

**Chose PdfPig:** exact fit (text extraction), free license, no native deps — same behavior on Windows and Linux containers.

### 7. OCR: Tesseract via CLI

**Options:** cloud Vision APIs · LLM vision · local Tesseract (.NET native wrapper vs CLI).

**Chose Tesseract CLI:** free, local (documents stay on the server), rus+eng. CLI avoids fragile native bindings: installer on Windows, `apt-get install tesseract-ocr` in Docker. Scanned PDFs are rasterized with `pdftoppm` (poppler) and OCR’d page by page.

### 8. LLM access: thin HttpClient behind `ILlmProvider`

**Options:** Semantic Kernel · Microsoft.Extensions.AI (`IChatClient`) · OpenAI SDK with BaseUrl swap · custom client (~100 lines).

**Chose a custom client:** `ILlmProvider` in Core is our swap point (DeepSeek → Ollama = one class), full control over telemetry (tokens/latency — foundation for Phase 4 metrics), zero extra frameworks. DeepSeek is OpenAI-compatible — one POST.

### 9. Strict JSON from the LLM: JSON mode + prompt schema + validate + 1 retry

**Options:** Structured Outputs with JSON Schema (OpenAI feature; DeepSeek lacks it) · function calling · JSON mode + our validation.

**Chose JSON mode + validation:** `response_format: json_object` guarantees syntax; required fields are checked in code; on failure — one retry with the error text. Most portable pattern across models. Nuance: JSON mode wants the word “json” in the prompt; field set is not guaranteed without validation.

### 10. Files: local disk behind `IFileStorage`

**Options:** `bytea` in Postgres · S3/MinIO · disk + Docker volume.

**Chose disk.** Stored names are Guids — user filenames never enter the path (path-traversal safe). MinIO stays on the “what’s next” list.

### 11. Document processing: synchronous in the request

**Options:** background queue (BackgroundService/Hangfire) · sync.

**Chose sync:** seconds per document, clearer demo. `Document.Status` is already ready for a future async pipeline.

---

## Phase 2 — RAG

### 12. Chunking: recursive splitter with overlap

**Fork:** how to cut extracted text for embeddings and retrieval.

**Options:** fixed size · structure-only (paragraphs/sentences) · recursive separator hierarchy · semantic boundaries via embeddings.

**Why not the others:** fixed size splits mid-line/amounts; pure structure yields wildly uneven chunks; semantic chunking needs extra model calls at index time — premature.

**Chose recursive splitter + overlap** (RecursiveCharacterTextSplitter style). Hierarchy `\n\n → \n → ". " → " " → char`; merge small pieces up to the limit. Overlap (~15%) keeps facts on boundaries. Sized in **characters**, not tokens — stays model-agnostic. Defaults `MaxChars=1000`, `Overlap=150`. Strategy behind `ITextChunker` for later A/B evals.

### 13. Embeddings: local model via OpenAI-compatible endpoint (Ollama, bge-m3)

**Fork:** how to turn a chunk into a vector.

**Options:** DeepSeek · cloud embedding APIs · local Ollama (`bge-m3` / multilingual-e5).

**Why not the others:** DeepSeek has no solid documented embeddings API. Cloud APIs bill on every chunk of every document and are awkward from RU.

**Chose Ollama `bge-m3` (1024-d, multilingual, strong on Russian).** Local, free marginal cost, private. Behind `IEmbeddingProvider` over `/v1/embeddings` — swap to cloud by config. Cost: run Ollama and `ollama pull bge-m3`.

### 14. Vector storage and search: pgvector + EF Core, HNSW + cosine

**Fork:** how to store and query nearest neighbors.

**Options:** separate vector DB · pgvector. **Index:** HNSW vs IVFFlat. **Distance:** cosine vs L2 vs inner product.

**Chose pgvector + EF Core.** Column `vector(1024)`, C# `Pgvector.Vector`, `CosineDistance` (`<=>`). **HNSW** (`vector_cosine_ops`) — better read quality than IVFFlat without a training pass. Cosine is the default for text embeddings. Conscious purity trade-off: `Vector` sits on the `Chunk` entity (tiny Core dependency on Pgvector) so EF can translate the query; EF wiring stays in Infrastructure.

### 15. RAG answer: retrieve → augment → generate, citations from retrieval

**Fork:** how to answer questions over documents.

**Options:** BM25 only · hybrid BM25+vector · classic RAG · GraphRAG / agentic multi-step.

**Chose classic RAG:** question → embedding → top-K chunks → prompt → DeepSeek. Last N session messages for follow-ups. **Citations are ground truth from retrieval**, not model free-text — API returns the chunks that were searched (file, ordinal, excerpt, distance).

### 16. Chat sessions: Postgres, not in-memory

**Fork:** where to store the dialogue.

**Options:** in-memory · Redis · Postgres.

**Chose Postgres:** `ChatSession` + `ChatMessage`, citations as jsonb. One DB, EF migrations. `IChatSessionStore` / `EfChatSessionStore`.

### 17. Search scope: all documents + optional filter

**Fork:** global index vs single document.

**Chose both:** default top-K over all chunks; optional `documentId` filters in SQL before OrderBy (HNSW still used).

---

## Phase 3 — agent tool-use

### 18. Agent API: explicit tool first, then goal

**Fork:** how the client invokes the agent.

**Options:** only `{ tool, documentIds }` · only `{ goal }` · both.

**Chose explicit tool first** (`POST /api/agent/tasks`) with `IAgentTool` + registry — easier to test. Goal routing came next in the same phase.

### 19. `generate_report` format: xlsx (ClosedXML)

**Options:** CSV · xlsx · PDF.

**Chose xlsx** — closest to back-office expectations. CSV fallback; PDF later.

### 20. Tool data source and order: `ExtractionResult`, reconcile first

**Fork:** where tools read data; what to build first.

**Chose `ExtractionResult.Json`** — already structured, no re-OCR/LLM. Order: scaffold → **reconcile** → summarize → generate_report. RAG is for Q&A, not amount reconciliation.

### 21. `summarize`: compact fields + LLM, not the full PDF

**Options:** RAG chunks · re-parse PDF · structured JSON → LLM · template without LLM.

**Chose compact text from extraction JSON** (item count, not full lines) → one `ILlmProvider` call. Reconcile stays deterministic; summarize is the first LLM tool.

### 22. `generate_report`: xlsx from JSON, two sheets, no LLM

**Chose** `DocumentReportService` + ClosedXML writer: sheets for documents and line items; file in `IFileStorage`; download `GET /api/agent/tasks/{id}/report`.

### 23. Goal mode: JSON router, not native tool-calling

**Options:** native OpenAI tools · JSON-mode router (`{ tool, reasoning }`) · keyword heuristics only.

**Chose JSON-mode router:** one call fits three tools; easy to fake in tests. Native tools reserved for multi-step loops. `POST /api/agent/goals` → router → `AgentTaskService`. Explicit tasks remain for debug.

---

## Phase 4 — evals and metrics

### 24. LLM telemetry: decorator + Postgres, not logs alone

**Options:** `ILogger` only · Prometheus/OTel · **`LlmUsageEvents` table + API**.

**Chose** `MeteringLlmProvider` decorator; operations tagged (`extraction`, `rag_chat`, `summarize`, `goal_router`); `LlmCostEstimator` from configurable USD/1M; `GET /api/metrics/summary`.

### 25. Evals v1: deterministic cases without a live LLM in CI

**Options:** e2e live LLM · offline golden JSON · **deterministic logic + fixture JSON**.

**Chose** `EvalSuiteService` — reconcile match/mismatch + extraction field checks. Live LLM accuracy later.

### 26. Golden extraction eval + latency p50/p95

**Chose** `ExtractionGoldenEval` over seven fields; invoice A/B goldens; `goldenFieldAccuracyPercent`; latency percentiles in the metrics summary.

### 27. Evals v2: recorded fixtures, RAG retrieval, agent heuristic

**Chose** embedded expected/actual JSON (normalize ООО/OOO); `RagRetrievalEval` on recorded hits; `AgentGoalHeuristic` as offline goal→tool baseline. **14** cases on `/api/metrics/evals`.

---

## Phase 5 — Blazor UI and deploy

### 28. Blazor in the same host project

**Options:** separate React SPA · Blazor WASM + host · **Blazor in `ai-doc-assistant/`**.

**Chose one process / one Docker image** — API + UI on `:8080`. UI talks REST via typed clients; Swagger stays for debugging.

**Rebuild note (Windows + VS F5):** the system file Browse dialog under Interactive Server can kill the process (`exited -1`). Prefer drag-and-drop under the debugger. Pages that must stay simple use classic form POST (SSR) where possible.

### 29. RAG chat UI: session in the URL + document filter

**Chose** REST via chat API; route `/chat/{sessionId}`; optional `?documentId=` for scoped retrieval and deep links from document details.

### 30. Agent UI: explicit tool + goal on one page — plus agent chat

**Chose** classic `/agent` (checkboxes + tool/goal radio) for debug and explicit runs.

**Later:** conversational **`/agent/chat`** — one user message ≈ one goal/tool run, results rendered in a chat lane (reconcile discrepancies, summary text, Excel download). Same backend (`AgentGoalService`); in-memory thread store for the demo. Home CTA points here — clearer portfolio demo than MCP-only.

### 31. Metrics dashboard + prod compose

**Chose** Blazor `/metrics` (LLM aggregates, p50/p95, DB counts, 14 eval rows) + `docker-compose.prod.yml` (`Production`, `restart: unless-stopped`). Dev compose unchanged.

---

## Phase 6 — MCP server

### 32. Separate stdio MCP project on the ModelContextProtocol SDK

**Options:** HTTP MCP inside Web · **stdio console host** · thin HTTP proxy to REST.

**Chose** `AiDocAssistant.Mcp` with stdio transport — native for Cursor, no Kestrel mix with API logs. Same Core/Infrastructure DI (no Blazor/Swagger). Tools: `list_documents`, `get_document`, `reconcile`, `summarize`, `generate_report`, `run_agent_goal`, `get_metrics_summary`, `list_agent_tools`, `get_agent_task`. Logs on stderr; secrets via env / user-secrets.
