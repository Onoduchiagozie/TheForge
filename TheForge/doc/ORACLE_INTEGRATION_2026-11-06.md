# Local TheForge / Oracle integration — 6 November 2026

This branch preserves the current source of the developer's existing feature
refactor together with the Oracle integration changes. The refactor was already
present in the working tree before this integration work; this is not a claim
that the integration work authored it. It is included so the committed source
contains the same application structure that was built and tested. The original
working tree, staged changes, and main branch were left unchanged.

## Behavior

- Start TheForge.cmd builds and starts the local frontend at http://127.0.0.1:5157/.
- The frontend calls the existing Oracle ngrok API directly. It needs no Node
  test console or SSH tunnel. The app continues running when Codex closes.
- Rider's oracle-local launch profile selects the same frontend/backend pair.
- Explorer uses /query/explore, Focus uses /query/focus, and Wide Net uses the
  SSE /query/narrate endpoint. Legacy default methods use existing routes.
- SSE event boundaries, multiline data, whitespace, stages and source metadata
  are preserved. An error or stream without completion cannot become a completed
  narration. The synchronous narration adapter accumulates SSE, not JSON.
- The health model accepts llm_model so the UI displays Oracle's Gemini model.

The public backend address can change. Update the oracle-local profile and local
launcher if it does. No production code, systemd service, billing, Lenovo source,
corpus, model, or index was changed by this frontend integration.

## What the tests establish

The 6 November integration run passed 27 tests:

| Tests | Objective | Evidence and limit |
| --- | --- | --- |
| 6 HTTP-client tests | Correct routes, original query payload, SSE aggregation, whitespace, completion/error handling | Synthetic HTTP responses; no live generation |
| 5 SSE-parser tests | Multiline events, token whitespace, stage/source parsing, malformed metadata, errors, cancellation | Synthetic event streams |
| 10 workspace tests | Mode routing, filters, applying text/sources, cancellation, failure state, duplicate submission prevention | Fake API service |
| 6 browser cases | Actual Blazor UI, all modes, progressive multiline display, sources, filters, stop buttons, failed streams, responsive CSS and static routes | Real browser and local gateway; deterministic fake backend |

A separate live browser test passed against the running frontend and real
Oracle metadata. It verified ONLINE, Gemini, 498,850 chunks, 2,245 books, typed
question preservation, and mode selection. It deliberately did not click the
generation button. It is opt-in to avoid turning ordinary tests into live calls.

Earlier manual live checks reached Oracle: Inspect displayed six Horus Rising
passage groups exactly matching its raw JSON. Narration streamed text/sources
but missed the requested rescue; another frontend request returned an
insufficient-evidence refusal. These are integration observations, not scene passes.

## What remains unproven

The seven requested scenes have not passed source-verified acceptance. Historical
results were five unsupported/refused scenes and two generated answers awaiting
source review. The Loken case subsequently produced text that still did not
satisfy the rescue request. There is no completed source-verification report.

The new raw-candidate reranking repair lives separately in OmnissiahCore staging.
Its ten synthetic selection tests passed, but the real-data comparison failed
in its runner before selection results. The local runner fix was not rerun after
automatic approval review hit its usage limit. It is not deployed.

Generation quality, exhaustive filters/source combinations, all provider failure
statuses, load/concurrency behavior, production hosting, and the separate archive
database's contents are not established by the 27 frontend tests.

## Repeat checks

```powershell
dotnet test Tests/TheForge.Tests/TheForge.Tests.csproj --filter "FullyQualifiedName!~LiveOracleConnectionTests"
dotnet test Tests/TheForge.Tests/TheForge.Tests.csproj --filter "FullyQualifiedName~LiveOracleConnectionTests"
```

Start the local frontend before the explicit live test. Runtime SQLite databases,
build output, logs, process records, private keys, and provider secrets are excluded
from the source commit. The archive database must be supplied separately.
