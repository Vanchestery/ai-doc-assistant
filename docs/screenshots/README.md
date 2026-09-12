# Скриншоты для GitHub README

Положи готовые PNG в **эту папку** (`docs/screenshots/`). README ссылается на файлы ниже — имена **строго** как указано.

Сейчас в репо лежат снимки с прототипа (тот же UI-flow). После polish/agent-chat можно переснять и заменить файлы с теми же именами.

---

## Минимум (4 шота — хватит для портфолио)

| Файл | Что снять | URL / где |
|------|-----------|-----------|
| **`01-documents.png`** | Список документов: несколько строк, статусы **Extracted**, видны schet A/B | http://localhost:8080/documents |
| **`03-agent-reconcile.png`** | **Предпочтительно Agent chat:** 2 счёта отмечены, в ленте цель + reconcile с расхождениями (112700 vs 115000). Альтернатива: классический `/agent` | http://localhost:8080/agent/chat |
| **`04-metrics.png`** | Dashboard: LLM stats + таблица eval-кейсов (14 cases) | http://localhost:8080/metrics |
| **`05-mcp-cursor.png`** | Cursor: MCP **Connected** + фрагмент чата с `list_documents` или `reconcile` | Cursor Settings → MCP + чат |

## Рекомендуется (+2)

| Файл | Что снять |
|------|-----------|
| **`02-extraction.png`** | Детали документа: JSON extraction (номер, сумма, контрагент) |
| **`06-rag-chat.png`** | Чат: вопрос + ответ + блок **citations** (нужна Ollama + проиндексированный doc) |

## Опционально

| Файл | Что снять |
|------|-----------|
| **`07-swagger.png`** | Swagger UI с раскрытым `/api/documents` или `/api/agent` |
| **`00-banner.png`** | Широкий кроп главной `/` или коллаж — для social preview (см. ниже) |

---

## Подготовка перед съёмкой

```powershell
cd C:\Users\ivan-\source\repos\ai-doc-assistant
docker compose up -d
# Проверь: http://localhost:8080/health → Healthy
```

1. **Данные:** в БД уже есть schet A/B — не чисти volume.
2. **Секреты:** на скринах **не должно** быть API keys, `.env`, содержимого `.cursor/mcp.json`.
3. **Окно:** браузер **1280×720** или ширина ~1400 px.
4. **Тема:** одна на всех UI-скринах.
5. **Язык UI:** русский интерфейс — ок для портфолио в RU.

### Agent reconcile (шаги)

**Agent chat (предпочтительно):**

1. `/agent/chat` → отметить **schet A** и **schet B**.
2. Цель: «Сверь счета и покажи расхождения» → Send.
3. Скрин: лента You + Agent с расхождениями total/vat.

**Классический `/agent`:**

1. `/agent` → отметить два счёта → Goal или Explicit reconcile → Run.
2. Скрин: результат с **2 расхождениями**.

### MCP (шаги)

1. MCP toggle **ON**, Local **Connected**.
2. В чате виден вызов tool и таблица/JSON.
3. Обрежь окно: Settings MCP + кусок чата.

### RAG (если снимаешь 06)

1. Ollama: `ollama pull bge-m3`, ollama running.
2. Документ Extracted → проиндексирован.
3. `/chat` → вопрос «какая итоговая сумма в счёте?» → видны **citations**.

---

## Как снять (Windows)

| Способ | Как |
|--------|-----|
| **Win + Shift + S** | Область → сохранить → переименовать в `01-documents.png` |
| **Snipping Tool** | Win, набери «Ножницы» |
| **Chrome full page** | F12 → Ctrl+Shift+P → `Capture full size screenshot` |

Сохраняй как **PNG**.

---

## После съёмки

```powershell
dir docs\screenshots\*.png
git add docs/screenshots/
git commit -m "docs: refresh README screenshots"
git push
```

---

## Social preview (GitHub)

**Settings → General → Social preview** — загрузи **`00-banner.png`** (1280×640).

---

## Чеклист качества

- [ ] Текст читаемый
- [ ] Нет ключей / паролей / личной почты
- [ ] Статус **Extracted**, не Failed
- [ ] Agent показывает осмысленный результат (сверка)
- [ ] Metrics: evals видны
- [ ] MCP: **Connected** зелёный
