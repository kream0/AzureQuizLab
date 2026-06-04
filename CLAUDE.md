# AzureQuizLab — Project Guide

AZ-204 training lab. One solution, two deployables:

- **`AzureQuizLab.WebApp/`** — ASP.NET Core Razor Pages (.NET 10) on Azure App Service, backed by Azure SQL via EF Core. *(TP1 + TP2)*
- **`AzureQuizLab.Functions/`** — Azure Functions (.NET 9, dotnet-isolated) with HTTP / Queue / Timer triggers. *(TP3)*

Lab instructions: https://github.com/Sybaris-Classroom/az-204 — see `TP1/`, `TP2/`, `TP3/`.

---

## Current status

| Lab | State | Verified |
|-----|-------|----------|
| **TP1** — Web App created + deployed | ✅ Done | Live at https://azurequizlab-07.azurewebsites.net |
| **TP2** — Azure SQL + EF Core + schema | ✅ Done | Home page shows `Nombre de quiz : 1`, `Nombre de questions : 7` from DB |
| **TP3** — Functions (HTTP/Queue/Timer) | ✅ Done | All 3 functions registered; full flow tested end-to-end |

**Optional / not yet done:**
- TP3 §9 — a dedicated GitHub Actions workflow to deploy the **Functions** app (only the WebApp has CI today).
- The existing WebApp CI deploys to a **different** app than the live one — see [CI/CD](#cicd) below.

---

## Repository layout

```
AzureQuizLab/                      ← repo root (= github.com/kream0/AzureQuizLab)
├── .github/workflows/             ← WebApp CI/CD (deploy on push to master)
├── AzureQuizLab.WebApp/           ← Razor Pages app (.NET 10)
│   ├── Models/                    ← EF Core entities + QuizDbContext
│   ├── Pages/                     ← Index shows DB counts
│   ├── appsettings.json           ← committed (no secrets)
│   ├── appsettings.Development.json  ← GITIGNORED (real SQL conn) — recreate locally
│   └── .gitignore                 ← also ignores appsettings.Development.json
├── AzureQuizLab.Functions/        ← Functions app (.NET 9, isolated)
│   ├── SubmitQuiz.cs              ← HTTP trigger
│   ├── ProcessQuiz.cs            ← Queue trigger
│   ├── CleanupQuizData.cs        ← Timer trigger
│   ├── Program.cs / host.json
│   └── local.settings.json        ← GITIGNORED (storage + SQL secrets) — recreate locally
├── AzureQuizLab.slnx              ← solution (references both projects)
├── AzureQuizLab.sql               ← full DB schema (6 tables, incl. Logs)
└── nuget.config                   ← forces nuget.org (see gotchas)
```

---

## Azure resources

All in subscription **`Azure subscription Az204`** (`bc02abea-1834-4e54-86f9-d9c75d8895ca`),
resource group **`RG-Student-07`** (West Europe). Set context first:

```bash
az account set --subscription bc02abea-1834-4e54-86f9-d9c75d8895ca
```

| Resource | Name | Notes |
|----------|------|-------|
| Web App | `azurequizlab-07` | Linux, Free **F1** plan `asp-azurequizlab-07` |
| Function App | `func-azurequizlab-07` | **Windows Consumption** (Y1) plan `WestEuropePlan`, runtime `dotnet-isolated` |
| Storage (Functions) | `azurequizlab07func` | Standard_LRS; hosts queue `quiz-queue` + content share `func-azurequizlab-07-content` |
| SQL Server | `sql-quizlab-00.database.windows.net` | admin login `sqladmin` (password in gitignored config) |
| SQL Database | `AzureQuizLabDB` | Basic tier; schema in `AzureQuizLab.sql` |

---

## Local setup — secret files (NEVER commit)

These are gitignored and must be recreated on a fresh clone. Get real values from the
Azure Portal / CLI (`az functionapp config appsettings list`, `az storage account keys list`).

**`AzureQuizLab.WebApp/appsettings.Development.json`**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=sql-quizlab-00.database.windows.net;Database=AzureQuizLabDB;User Id=sqladmin;Password=<SQL_PASSWORD>;Encrypt=True;TrustServerCertificate=False;"
  }
}
```

**`AzureQuizLab.Functions/local.settings.json`**
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "<STORAGE_CONNECTION_STRING>",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "SqlConnectionString": "Server=sql-quizlab-00.database.windows.net;Database=AzureQuizLabDB;User Id=sqladmin;Password=<SQL_PASSWORD>;Encrypt=True;TrustServerCertificate=False;"
  }
}
```

> The same secrets live in Azure as **app settings** on each app (the Function App also needs
> `WEBSITE_CONTENTAZUREFILECONNECTIONSTRING`, `WEBSITE_CONTENTSHARE`, `FUNCTIONS_EXTENSION_VERSION=~4`).

---

## Build & validate

**Validation command** (run before every commit — must be 0 errors):

```bash
dotnet build AzureQuizLab.slnx -c Release
```

Per-project if needed:
```bash
dotnet build AzureQuizLab.WebApp/AzureQuizLab.WebApp.csproj -c Release
dotnet build AzureQuizLab.Functions/AzureQuizLab.Functions.csproj -c Release
```

Run the Functions host locally (port 7293): `cd AzureQuizLab.Functions && func start`.

---

## Deploy

### Web App (`azurequizlab-07`)
Push to `master` triggers CI (but see the app-name caveat in [CI/CD](#cicd)). For a **manual**
deploy to `azurequizlab-07`, publish then zip-deploy via Kudu:
```bash
dotnet publish AzureQuizLab.WebApp/AzureQuizLab.WebApp.csproj -c Release -o publish_output
# zip publish_output/* and deploy via Kudu ZipDeploy / VFS API
```

### Functions (`func-azurequizlab-07`) — proven method
`func azure functionapp publish` was unreliable in this environment; the method that works is a
direct upload to the content share, because the app runs from the Azure Files share:

```bash
# 1. Publish
cd AzureQuizLab.Functions
dotnet publish -c Release -o ../func_publish_output

# 2. Stop the app (releases the SMB lock on the share), from repo root
az functionapp stop -n func-azurequizlab-07 -g RG-Student-07

# 3. Replace files on the content share (use the storage account key as --account-key)
az storage file delete-batch --account-name azurequizlab07func \
  --source func-azurequizlab-07-content --pattern "site/wwwroot/*" --account-key <KEY>
az storage file upload-batch --account-name azurequizlab07func \
  --destination func-azurequizlab-07-content --destination-path "site/wwwroot" \
  --source func_publish_output --account-key <KEY>

# 4. Start + wait ~40s, then verify
az functionapp start -n func-azurequizlab-07 -g RG-Student-07
```

`func_publish_output/` is gitignored — it's a regenerable build artifact.

---

## Critical gotchas (environment-specific — read before changing infra)

1. **Functions MUST target `net9.0`, not `net10.0`.** The Windows Consumption host has no .NET 10
   runtime. Symptom: host `Error` →
   `Could not load file or assembly 'System.Runtime, Version=10.0.0.0'`.
2. **No OpenTelemetry / Azure Monitor exporter in the Functions app.** `UseAzureMonitorExporter()`
   throws `A connection string was not found` at startup (it needs `APPLICATIONINSIGHTS_CONNECTION_STRING`)
   and kills the host. `Program.cs` is intentionally minimal; `host.json` uses default telemetry.
3. **Subscription policy limits App Service Plans to `F1` / `Y1` only.** Create the Function App with
   `az functionapp create --consumption-plan-location <region>` (auto Y1); the WebApp uses Free F1.
   Linux dynamic workers are not available here — the Function App is **Windows** Consumption.
4. **NuGet: use nuget.org directly.** The default Artifactory proxy returns **403**. `nuget.config`
   pins `nuget.org`; keep it. Install packages with `--source https://api.nuget.org/v3/index.json` if needed.
5. **Never commit secrets.** `appsettings.Development.json` and `local.settings.json` are gitignored
   (root `.gitignore` + nested `AzureQuizLab.WebApp/.gitignore`). The root `.gitignore` also blocks
   `*.publishsettings`. Verify before committing: `git diff --cached | grep -iE "Password=|AccountKey="`.

---

## The 3 functions

| Function | Trigger | Behavior |
|----------|---------|----------|
| `SubmitQuiz` | HTTP (GET/POST, `Function` auth) | Serializes a quiz object → sends to queue `quiz-queue`. Returns `"Quiz soumis!"`. |
| `ProcessQuiz` | Queue (`quiz-queue`, `AzureWebJobsStorage`) | Reads message → `INSERT INTO Logs` via `SqlConnectionString`. |
| `CleanupQuizData` | Timer (`0 */5 * * * *`, every 5 min) | `INSERT INTO Logs` a marker row. |

`AzureQuizLab.sql` defines: `Quiz`, `Question`, `Answer`, `QuizAttempt`, `QuizAttemptAnswer`, `Logs`.

---

## End-to-end test

```bash
# Function key
az functionapp keys list -n func-azurequizlab-07 -g RG-Student-07

# Trigger the HTTP function (enqueues a message)
curl "https://func-azurequizlab-07.azurewebsites.net/api/submitquiz?code=<FUNCTION_KEY>"
#   → "Quiz soumis!"

# A few seconds later, the queue + timer triggers should have written to Logs:
sqlcmd -S sql-quizlab-00.database.windows.net -d AzureQuizLabDB -U sqladmin -P "<SQL_PASSWORD>" \
  -Q "SELECT Message, LogDate FROM Logs ORDER BY LogDate DESC"

# Host / registered functions (master key from the keys list above)
curl "https://func-azurequizlab-07.azurewebsites.net/admin/host/status?code=<MASTER_KEY>"
curl "https://func-azurequizlab-07.azurewebsites.net/admin/functions?code=<MASTER_KEY>"
```

WebApp: open https://azurequizlab-07.azurewebsites.net — the home page reads quiz/question counts
live from `AzureQuizLabDB`.

---

## CI/CD

`.github/workflows/master_azurequizlab-kelallame.yml` builds + deploys the **WebApp** on push to
`master` (it targets `AzureQuizLab.WebApp.csproj` explicitly, since the repo root now holds a
2-project solution).

⚠️ **Caveat:** that workflow deploys to app **`AzureQuizLab-kelallame`** (a TP1 artifact), which is
**not** the live app `azurequizlab-07`. Before relying on CI, either repoint the workflow's
`app-name` (and the `AZUREAPPSERVICE_PUBLISHPROFILE` secret) to `azurequizlab-07`, or keep deploying
the WebApp manually. There is no CI for the Functions app yet (TP3 §9, optional).

---

## Azure skills (installed)

Relevant skills from [MicrosoftDocs/Agent-Skills](https://github.com/MicrosoftDocs/Agent-Skills) are
installed under `~/.claude/skills/`: `azure-functions`, `azure-app-service`, `azure-sql-database`,
`azure-queue-storage`, `azure-monitor`, `azure-resource-manager`. Prefer these for Azure guidance
over improvising CLI commands.
