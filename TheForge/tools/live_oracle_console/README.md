# Test Oracle without Codex

On your Dell, double-click **Start Oracle Console.cmd** in this folder.
It starts the SSH tunnel and local console, checks Oracle's health, and opens
http://127.0.0.1:18011/ in your normal browser. Codex does not need to be open.
The helper processes continue after the launcher window closes.

Double-click **Stop Oracle Console.cmd** to stop the helpers recorded by this
launcher. It does not stop Oracle's API. After a computer restart, run Start again.

The launcher uses your existing Oracle SSH key without displaying its contents.
It uses PowerShell 7 and Node.js from PATH, or the executables already installed
on this Dell. Those executables can run while the Codex app is closed.
It does not enable billing or change the Oracle service.

## What is hosted

Oracle hosts the Python API, corpus retrieval, and generation calls. This console
is a local Windows client connected to that API. A permanent hosted browser
frontend remains unfinished; localhost refers to the Dell, not Oracle.

## Use

Type an ordinary question and click **Ask Oracle**. Choose scene narration,
Focus, or Explore for a generated response, or Inspect for retrieved passages
without a generation call. Sources, exact request, and raw response are available.
Opening the page does not generate an answer.

HTTP success and a completed stream do not establish scene accuracy.

## Verified scope

The PowerShell launcher's start, reuse, health check, and stop lifecycle were
tested on ports 18012/18013 on 5 November 2026. The Windows double-click wrappers
were added on 6 November; they have not been launched through Explorer.

The actual console at port 18011 rendered, sent the original Loken/Jubal question
to Oracle, and displayed six passage groups exactly matching Oracle's raw reply.
A separate narration request streamed text and sources, but summarized surrounding
events instead of satisfying the requested rescue scene. Scene acceptance failed.

TheForge also reached Oracle through its actual browser UI and displayed a
refusal plus two source cards. Its route and SSE fixes passed 27 C# tests,
including six browser cases against a deterministic local backend. These tests
do not prove the seven requested scenes are accurately answered.

## Optional PowerShell commands

```powershell
.\Start-OracleConsole.ps1
.\Start-OracleConsole.ps1 -Stop
```

Choose other unused local ports if needed:

```powershell
.\Start-OracleConsole.ps1 -TunnelPort 18012 -ConsolePort 18013
.\Start-OracleConsole.ps1 -ConsolePort 18013 -Stop
```

Logs and process records stay in `.runtime/`. Do not paste API keys into the page.
