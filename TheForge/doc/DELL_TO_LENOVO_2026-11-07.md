# Dell to Lenovo handover — 7 November 2026

## Repositories and pending work

- **TheForge** remains the separate .NET 8 frontend repository. The prepared frontend
  is on `codex/oracle-frontend-integration-20261106`, not `main`.
- **OmnissiahCore** is the shared backend repository for Oracle and local GPU machines.
  Its standalone workflow is pushed as `fc828891c15b08a7da556c2b715e6f1f6a2f5d61`
  on `codex/standalone-workflow-20261106`.
  Draft PR: https://github.com/Onoduchiagozie/OmnissiahCore/pull/1
- The user confirmed Lenovo contains unpushed changes. They have **not** been inspected,
  reconciled or backed up by this Dell session. Do not claim all source is online yet.
- The requested backend workflow is temporary branches, merge tested changes into
  `main`, publish an identifiable release to Oracle, then remove merged temporary
  branches. Machine differences belong in external private settings, not permanent
  Oracle/Lenovo code branches.
- The backend workflow PR is not merged or deployed. Its 23 workflow tests passed on
  Windows/Python 3.12 and Ubuntu/Python 3.10 in GitHub Actions run `42249442359`.
  These tests do not establish retrieval accuracy or verify Lenovo's GPU/data setup.

## Begin on Lenovo without losing work

1. Open this conversation on Lenovo and supply the OmnissiahCore project folder.
2. Inspect branch, Git status, tracked diffs and untracked source before checkout,
   pull, reset, clean or merge. Preserve a private local backup of uncommitted work.
3. Back up the corpus/databases/model assets before changing branches. The workflow
   PR stops tracking generated data; pulling it into another checkout can remove
   files that Git previously tracked. They must be preserved outside Git first.
4. Compare Lenovo's code with the workflow branch and Oracle changes. Merge the
   actual source changes into a temporary review branch; keep keys, `.env`, private
   configuration, databases, books, model files and logs out of commits and uploads.
5. Configure the local GPU profile and test with Lenovo's real dependencies/data.
   Only then finish the `main` merge, release verification, Oracle publish and branch
   cleanup. Oracle production changes still require the user's explicit approval
   of the tested change and backup.

Dell's private setup is outside Git under the user's `.config/omnissiahcore` directory.
Transfer private settings through a private route; do not put their values in this
document, chat output, commits or PRs. A fresh clone also needs Python/.NET, packages,
private settings and the separate data/model assets before the whole system runs.

## Frontend publish repair

The public site is https://legion.runasp.net/ and the user publishes through Rider.
The user confirmed Ctrl+Shift+R restored the broken layout on 7 November. The browser
had cached an older unversioned stylesheet after the new app was uploaded.

The new source fix hashes each stylesheet's contents into its URL, including the
Blazor scoped CSS bundle. It also fixes HTML-encoded font quotes and stray text at
the start of the logo CSS. Local diagnostic/backend folders and development
settings are excluded from website publish output.

Validation: 29 frontend regression tests passed. An additional Chromium check of
an actual Release publish running in **Production** passed: CSS status/content type,
content-hash URLs, applied fonts and layout, gold logo, and Scene Archive. No lore
queries were submitted by the publish check. The production publish folder was
tested locally; the CSS prevention fix was **not uploaded to MonsterASP** by this
session and reaches the website on the next Rider publish.

## User settings, API keys and usage limits are unfinished

The frontend currently has a placeholder `/settings` page. `Program.cs` has user
settings, usage services and authentication middleware disabled. The authentication
and settings controllers/middleware are commented out or placeholders. Entity types
for users, keys and tiers alone do not provide working enforcement. Do not enable
this by merely uncommenting code: missing services, persistence, identity flow and
tests must be completed.

Before calling this implemented, verify user-owned settings, API-key creation and
revocation, server-side tier checks, per-user/key rate and quota enforcement,
consistent enforcement for every query mode (including streaming), and the trust
boundary between the public frontend and Oracle. Browser-visible secrets must not
be used to protect a shared backend. Agree actual access levels and limits with the
user before making those product decisions.

## Retrieval accuracy remains separate

The Horus refusal was traced to passages that mention betrayal without explaining
Horus's own motives. Connectivity, provider fallback and CSS fixes do not solve that.
The proposed retrieval repair has not been established as an accuracy improvement
or deployed as such. Preserve its staged work and evaluate real queries on Lenovo.
