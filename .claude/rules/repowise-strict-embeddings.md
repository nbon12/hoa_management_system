# Repowise — strict semantic search (no silent BM25 fallback)

This repo uses **Ollama** (`mxbai-embed-large`) for Repowise vector search. Repowise can silently fall back to keyword search when embeddings fail. **Do not treat fallback results as semantic search.**

## After every Repowise retrieval call

Inspect `_meta` and per-result fields before acting on hits.

### Hard stop — alert the user and do not proceed with Repowise results

Stop and tell the user to fix Ollama/embeddings when **any** of these are true:

| Signal | Meaning |
|--------|---------|
| `_meta.embedder_degraded: true` | MCP is using mock vectors, not Ollama |
| `_meta.embedder_warning` present | Embedder failed at MCP startup |
| `search_method: "bm25"` on **any** `search_codebase` result | Vector search failed; keyword fallback was used |
| `get_answer` retrieval hits have `_sources` with only `"fts"` (no `"vector"`) on a conceptual question | Hybrid retrieval lost the vector leg |

**Do not** silently continue with BM25 hits, mock embeddings, or FTS-only retrieval for conceptual discovery.

### What to tell the user

1. Semantic search is degraded — results may be keyword-only or mock vectors.
2. Check Ollama: `ollama list`, `curl -s http://localhost:11434/api/tags`
3. Restart Cursor/Claude after fixing (MCP loads embedder config at startup).
4. Reindex if needed after wiki changes (see project Repowise/Ollama setup).

### Recovery checklist (run or suggest)

```bash
# Ollama desktop app must be running (menubar icon); enable Start at login in app settings
ollama list | grep mxbai-embed-large
curl -s http://localhost:11434/api/tags
repowise doctor
```

## When fallback is OK

- **Exact identifier lookup** after Repowise already narrowed scope — use **Grep**, not degraded `search_codebase`.
- Repowise MCP server is **entirely unavailable** — say so explicitly, then use Grep/Read (not BM25 pretending to be semantic search).

## Healthy signals

- `search_codebase` → `"search_method": "embedding"` on results
- `_meta.embedder_degraded` absent or `false`
- Vector search latency is normal (~40–50ms warm after Ollama is up)
