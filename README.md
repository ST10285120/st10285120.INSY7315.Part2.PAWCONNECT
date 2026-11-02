# PawConnect 

**Animal shelter adoption and volunteer management platform for Hope & Paws Animal Shelter**
INSY7315 – Work Integrated Learning · Task 3 (final submission)

PawConnect lets the public browse adoptable animals, apply to adopt online and donate or sponsor an animal. Volunteers review applications, sign up for shifts, log hours and record medical care. The shelter manager handles intake, users, donations and funder reporting.

---

## Contents
1. [Quick start](#quick-start)
2. [Demo accounts](#demo-accounts)
3. [Features by role](#features-by-role)
4. [Architecture](#architecture)
5. [Project structure](#project-structure)
6. [REST API](#rest-api)
7. [Security](#security)
8. [Testing](#testing)
9. [Git workflow & CI/CD](#git-workflow--cicd)
10. [Hosting on Azure](#hosting-on-azure)
11. [Documentation & presentation](#documentation--presentation)
12. [What changed for Task 3](#what-changed-for-task-3)

---

## Quick start

**You need:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for PostgreSQL).

```bash
# 1. Start PostgreSQL in Docker
docker compose up -d db

# 2. Run the web app (applies migrations and loads demo data automatically)
dotnet run --project src/PawConnect.Web
```

Open **http://localhost:5231**. In Visual Studio open `PawConnect.sln` and run the **PawConnect.Web** `http` profile.

> **No Docker?** Install PostgreSQL 16 locally and create a `pawconnect` user and database with password `pawconnect` or change `ConnectionStrings:PawConnect` in `src/PawConnect.Web/appsettings.Development.json`.

**Run everything in containers instead:** `docker compose --profile app up --build` and then open http://localhost:8080.

## Demo accounts

Development and staging load demo data. Every demo account uses the password **`Demo!Paws2026`**.

| Role | Email | Lands on |
|---|---|---|
| Shelter administrator | `admin@pawconnect.demo` | Shelter dashboard |
| Volunteer | `priya@pawconnect.demo` (also `sipho@`, `emma@`, `thabo@`) | Volunteer dashboard |
| Adopter | `adopter@pawconnect.demo` | Home page |

You can also register a new account. Self registered users are adopters. Production never loads demo data. Only the first administrator is created from `Seed:AdminEmail` / `Seed:AdminPassword`.

### Demo animal photos

The nine demo animals come with real pet photos stored in `src/PawConnect.Infrastructure/Data/SeedPhotos/` and embedded in the build. They are attached on first start and also to an existing local database the next time the app starts so you dont need to reset anything. Sources and licences are in [`SeedPhotos/CREDITS.md`](src/PawConnect.Infrastructure/Data/SeedPhotos/CREDITS.md). Production never loads them admins upload real photos under **Admin → Animals → Edit**.

## Features by role

| | Feature | Where |
|---|---|---|
| **Public / adopter** | Browse animals with live filters | `/Home/Browse` → `GET /api/animals` |
| | Animal profile with photo gallery and vaccination badge | `/Home/Profile/{id}` |
| | Multi step adoption application with **autosave** prefilled from the account | `/Applications/Apply` |
| | Track applications and withdraw | `/Applications/Mine` |
| | Donate or sponsor an animal and get a receipt  | `/Home/Donate` |
| | Email confirmations, and SMS (Twilio) for urgent shift messages with email as fallback | Strategy pattern |
| | My account: edit details and donation history, **download my data (POPIA)**, **two-step verification** | `/Account/Me`, `/Account/TwoFactor` |
| **Volunteer** | Review applications: start review, approve or reject with a note, home visit and adoption fee | `/Volunteer/Application/{id}` |
| | Shift roster with sign up / cancel. **No double booking**: capacity, duplicate and overlap checks with optimistic concurrency | `/Volunteer/Dashboard` |
| | Log volunteer hours | `/Volunteer/Dashboard` |
| | Medical history and **log a medical record** | Profile page → `/api/medical-records` |
| **Administrator** | Dashboard: animals in care, adoptions, volunteer hours, donations, work queues and 6 month donation chart | `/Admin/Dashboard` |
| | Animal intake, edit, status and photo upload  | `/Admin/Animals` |
| | Create shifts; approve or reject hours volunteer directory | `/Admin/Shifts`, `/Admin/Hours`, `/Admin/Volunteers` |
| | Confirm pledged donations | `/Admin/Donations` |
| | Funder report for any date range plus **CSV export** | `/Admin/Reports` |
| | Users: create staff and volunteer accounts, grant or remove roles deactivate | `/Admin/Users` |

## Architecture

A **layered 3-tier modular monolith** (design doc 6.2) in one solution:

```
 Browser ──HTTPS──► PawConnect.Web            MVC pages + REST API (/api), Identity and security middleware
                         │ uses
                         ▼
                    PawConnect.Core            Entities, business rules, services and interfaces
                         ▲ implements
                         │
                    PawConnect.Infrastructure  EF Core + PostgreSQL, repositories, seeding and email/SMS
                         │
                         ▼
                    Azure Database for PostgreSQL
```

**Design patterns (design doc 6.1)**

| Pattern | Where | Why |
|---|---|---|
| **Repository** + Unit of Work | `Core/Interfaces/IRepositories.cs`, `Infrastructure/Repositories` | Services never touch EF Core directly so business rules are testable without a database |
| **Strategy** | `Core/Notifications` (`INotificationStrategy`, `NotificationStrategySelector`) | Email by default and SMS for urgent shift messages with retry, exponential backoff and fallback. Adding a channel needs no service changes |
| **Factory** | `Core/Receipts/DonationReceiptFactory.cs` | Receipt numbers and wording live in one place |
| **State machine** | `Core/Services/ApplicationStateMachine.cs` | Enforces the adoption lifecycle from the state diagram  |

**Beyond basic CRUD**
- Optimistic concurrency plus retry and PostgreSQL *partial unique indexes* and CHECK constraints so two people clicking at once can't overbook, double apply or overwrite each others decision.
- Branch scoped authorisation shared by the pages and the API.
- Error kinds carried from the services to the API so every failure gets the right HTTP status.
- Interval overlap check to stop a volunteer booking clashing shifts.
- All times are stored in UTC and shown in South African time.
- Caching of the public stats  `AsNoTracking` read queries and projections so photo bytes are only loaded when a photo is served and database indexes behind every filter.
- Progressive enhancement: every page works without JavaScript and JS adds live filtering, autosave and API calls.

## Project structure

```
PawConnect.sln
├── src/
│   ├── PawConnect.Core/            Entities, Enums, Services , Notifications and Receipts
│   ├── PawConnect.Infrastructure/  AppDbContext, Configurations, Migrations, Repositories and DataSeeder
│   └── PawConnect.Web/             Program.cs, Controllers, Views, ViewModels, Security and wwwroot
├── tests/PawConnect.Tests/         Unit, service, API integration and PostgreSQL tests 
├── tests/accessibility/            axe-core WCAG 2.1 AA audit of every page 
├── infra/main.bicep                Azure infrastructure as code
├── .github/workflows/              ci.yml, deploy.yml and keep awake.yml 
├── scripts/                        azure-setup.sh, smoke test.sh and wait for version.sh
├── render.yaml                     Free hosting blueprint and see docs/DEPLOYMENT-FREE.md
├── docs/                           ARCHITECTURE.md, REQUIREMENTS-TRACEABILITY.md, DEPLOYMENT.md, DEPLOYMENT-FREE.md,
│                                   presentation/
├── CONTRIBUTING.md                 Branching, commit and PR rules
├── Dockerfile, docker-compose.yml
└── PawConnect.http                 Ready made API requests for Visual Studio
```

**Adding a database change:** edit the entity then
```bash
dotnet tool install --global dotnet-ef --version 8.0.8   # once
dotnet ef migrations add <Name> --project src/PawConnect.Infrastructure --startup-project src/PawConnect.Web --output-dir Data/Migrations
```
Migrations apply automatically when the app starts.

## REST API

JSON over HTTPS. Browser JavaScript authenticates with the login cookie and must send the `X-CSRF-TOKEN` header on writes. Other clients call `POST /api/auth/token` and send `Authorization: Bearer <token>`. Try it all with **`PawConnect.http`**.

**Error contract:** every error is RFC 7807 `application/problem+json` with a status code that says what went wrong:
`400` invalid input, `401` not signed in, `403` wrong role, `404` doesn't exist *or isn't yours*, `409` valid but clashes with the current state and `429` too many sign in attempts. Enums are sent by name. Numbers are rejected.

| Method | Endpoint | Who | Purpose |
|---|---|---|---|
| POST | `/api/auth/token` | anyone | Email + password (+ `twoFactorCode` when 2FA is on) → bearer token |
| GET | `/api/animals?species=&size=&available=&kids=&pets=&search=` | anyone | Search animals |
| GET | `/api/animals/{id}` | anyone | Animal profile |
| GET | `/api/animals/{id}?include=history` | volunteer, admin | Profile + medical history |
| PATCH | `/api/animals/{id}/status` | admin | Change status |
| POST | `/api/medical-records` | volunteer, admin | Log a medical record  |
| GET | `/api/medical-records/{id}` | volunteer, admin | One medical record |
| GET | `/api/applications/mine` | adopter | My applications |
| POST | `/api/applications` | adopter | Submit an application |
| GET | `/api/applications/{id}` | owner, or staff at the animal's branch | Application detail |
| POST | `/api/applications/{id}/transitions` | volunteer, admin | `{ "status": "UnderReview" / "Approved" / "Rejected" / "Adopted", "note": "" }` |
| POST | `/api/applications/{id}/withdraw` | owner | Withdraw |
| GET | `/api/shifts?openOnly=` | volunteer, admin | Upcoming shifts |
| POST / DELETE | `/api/shifts/{id}/signup` | volunteer, admin | Sign up / cancel |
| POST | `/api/donations` | signed in | Pledge a donation or sponsorship |
| GET | `/api/donations/{id}` | donor or staff | One donation |
| GET | `/api/donations/mine` | signed in | My donations |
| GET | `/api/reports/summary?from=&to=` | admin | Funder report numbers |
| GET | `/api/reports/donations.csv?from=&to=` | admin | Donations CSV |
| GET | `/health` | anyone | Health check |
| GET | `/version` | anyone | The running commit and environment|

## Security

Mapped to the OWASP Top 10 table in the design doc:

- **Access control:** role-based `[Authorize]` on every controller plus server side ownership checks and **branch scoping**. Other peoples records answer 404 so ids can't be probed. The UI hides buttons too but the server never relies on that.
- **Data integrity:** optimistic concurrency on applications, animals and shifts. CHECK constraints and partial unique indexes in PostgreSQL.
- **Two-step verification (MFA):** any user can add an authenticator app (with single-use recovery codes). It is **required for administrators in production**, and API tokens need the code too.
- **Authentication:** ASP.NET Core Identity with salted PBKDF2 password hashes and a password policy **lockout after 5 failed attempts**, **rate limiting** on login and register, account deactivation and security stamp revalidation.
- **CSRF:** anti forgery tokens on every form POST and a header token for cookie authenticated API calls.
- **Injection / XSS:** EF Core parameterised queries only. Razor HTML encodes output. JS inserts text with `textContent`. CSV export neutralises spreadsheet formulas.
- **Headers:** a strict Content Security Policy, X Frame-Options, nosniff, a Referrer Policy, HSTS and HTTPS redirection.
- **Uploads:** image type is detected from the files bytes with a 2 MB limit and a cap on photos per animal.
- **Secrets:** no secrets in source control. Azure Key Vault supplies them through the managed identity.
- **POPIA:** a consent checkbox with timestamp, a privacy notice, "download my data" and data minimisation.

## Testing

```bash
dotnet test
```

There are **151 tests** in `tests/PawConnect.Tests`:
- **Unit:** the state machine, notification strategy selection with retry and fallback, the Twilio SMS channel and phone number formatting. The receipt factory, CSV injection protection, time zone ranges and image detection.
- **Service:** every business service runs against the real repositories on the EF Core in memory database. Covered: the adoption workflow and its guards, shift booking, hours rules, donations, medical records, animal intake and reports.
- **Integration:** `WebApplicationFactory` boots the whole app to test API auth, bearer tokens, CSRF enforcement, the full adoption workflow through the API, shift capacity, security headers, admin protection, CSV export and every page rendering for each role. It also covers registration with POPIA consent, data export, account lockout, admin creating and deactivating users, and two-step verification (setup, the login code step, API tokens and the admin requirement).
- **Access control & API errors:** another adopter or another branches volunteer gets 404. Every error is problem+json with the right status code (400/401/404/409)Ddeactivated accounts aren't revealed without the password.
- **Database (PostgreSQL):** 13 tests build a fresh PostgreSQL database from the real migrations and prove the CHECK constraints, partial unique indexes and concurrency rules with two simultaneous users. Two adopters applying for the last animal, approve vs. withdraw at the same moment a double clicked adoption fee and six volunteers racing for a two place shift. Two more upgrade an old database full of data to check the migration repairs what it safely can and stops with a clear message otherwise. They run in CI; locally they run when `PAWCONNECT_TEST_POSTGRES` is set  and are skipped otherwise.
- **Accessibility:** `tests/accessibility/axe_audit.py` audits every page for every role at desktop and mobile widths against WCAG 2.1 AA. Current result: **0 violations**.

CI runs all of them  with code coverage, posts the coverage summary on each workflow run and **fails below 70% line coverage**.

## Git workflow & CI/CD

**Branching **
- `main`: production. Protected changes arrive only by PR from `develop`.
- `develop`: integration branch which deploys automatically to **staging**.
- `feature/<short-name>` / `fix/<short-name>`: branched from `develop` and merged back through a pull request once CI passes. PawConnect is a one-person project, so I review my own pull requests: the required CI checks are the gate, not a second reviewer.

**Branch protection (GitHub → Settings → Branches)**: for `main` and `develop`, tick *Require a pull request before merging*  and *Require status checks to pass* .

**Pipelines**

| Workflow | Trigger | What it does |
|---|---|---|
| `ci.yml` | every PR and feature branch push | restore → build → **151 tests with coverage** → **≥70% coverage gate** → **vulnerable package scan** → EF migration check against a real PostgreSQL → publish artifact → **accessibility audit (WCAG 2.1 AA)** |
| `deploy.yml` | push to `develop` | CI → deploy to the **staging** slot → smoke test |
| `deploy.yml` | push to `main` | CI → **manual approval** → deploy to staging slot → smoke test → **blue-green slot swap** → health check → **automatic swap back** on failure |
| `deploy.yml` | as above, with `HOSTING_PLATFORM=render` | CI → (approval) → Render deploy hook → wait until `/version` shows the new commit → smoke test |
| `keep-awake.yml` | every 10 min in the day opt in | keeps a free Render service awake during demo week |
| Dependabot | weekly | PRs for NuGet and GitHub Actions updates |

## Hosting on Azure

One command sets up Azure **and** the GitHub pipeline secrets:

```bash
az login && gh auth login      # the GitHub CLI is optional; without it the script prints the values to paste
./scripts/azure-setup.sh --app pawconnect-<student-number> --repo <user>/<repo> --email <you@example.com>
```

This creates App Service, PostgreSQL Flexible Server, Key Vault, Application Insights and alert rules from `infra/main.bicep`, plus a passwordless  identity for GitHub Actions. Then push to `develop` → staging and merge to `main` → production. Details and troubleshooting: **[docs/DEPLOYMENT.md](docs/DEPLOYMENT.md)**.

| Option | Cost | Releases |
|---|---|---|
| Azure, `--sku S1` (default) | ~US$70/month (Azure for Students credit) | blue green slot swap with automatic rollback |
| Azure, `--sku F1` | free  | staging app anbd direct deploy after approval |
| Render + Neon ([docs/DEPLOYMENT-FREE.md](docs/DEPLOYMENT-FREE.md)) | free | staging and production services deploy hook after CI |

**Why Azure App Service:** fully managed .NET hosting with TLS, health-checked instances, zero-downtime slot swaps, managed identity to Key Vault and Application Insights in South Africa North, matching the Task 1 design. The full rationale and alternatives are in [docs/ARCHITECTURE.md §5–6].

## Documentation & presentation

| Document | Contents |
|---|---|
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | System and cloud diagrams, layers, sequence and ER diagrams, technology and hosting rationale measured performance, security and accessibility |
| [docs/REQUIREMENTS-TRACEABILITY.md](docs/REQUIREMENTS-TRACEABILITY.md) | Every user story, acceptance criterion and NFR → implementation → test |
| [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) | Azure deployment, pipeline setup, email and troubleshooting |
| [docs/DEPLOYMENT-FREE.md](docs/DEPLOYMENT-FREE.md) | Free alternative: Render + Neon with the same pipeline |
| [docs/user-guide/PawConnect_User_Guide.docx](docs/user-guide/PawConnect_User_Guide.docx) | User Guide for adopters, volunteers, administrators and the technical volunteer, with annotated screenshots, setup and FAQ |
| [docs/presentation/PawConnect_EXPO_Presentation.pptx](docs/presentation/PawConnect_EXPO_Presentation.pptx) | Task 3 EXPO deck with speaker notes |
| [CONTRIBUTING.md](CONTRIBUTING.md) | Git workflow, commit convention and PR rules |


**Live URLs** 
- Production: `pawconnect-st10285120-fbfcdpfnf5g2h0bk.southafricanorth-01.azurewebsites.net`
- Staging (demo data, password `Demo!Paws2026`): `pawconnect-st10285120-staging-g0dzbfbtc8atbfcw.southafricanorth-01.azurewebsites.net`

### Demo accounts (staging only)

Staging: https://pawconnect-st10285120-staging-g0dzbfbtc8atbfcw.southafricanorth-01.azurewebsites.net

All demo accounts use the password **`Demo!Paws2026`**.

| Role | Email | Use it to show |
|---|---|---|
| Administrator | `admin@pawconnect.demo` | Animals, users, donations, reports, CSV export |
| Volunteer | `priya@pawconnect.demo` | Reviewing and approving applications, medical records, shifts, hours |
| Volunteer | `sipho@pawconnect.demo` | A second volunteer (e.g. the shift-overlap demo) |
| Volunteer | `emma@pawconnect.demo` | Another volunteer |
| Volunteer | `thabo@pawconnect.demo` | Another volunteer |
| Adopter | `adopter@pawconnect.demo` | Applying for an animal, tracking status, donating |
| Deactivated volunteer | `aisha@pawconnect.demo` | Login is refused, which shows deactivated accounts are blocked |

Production has no demo accounts. It only holds real shelter data, and its administrator is created from the
`Seed__AdminEmail` / `Seed__AdminPassword` app settings in Azure.

## What changed for Task 3

Each change answers a point in the Task 2 feedback. Each one went through its own branch and pull request, in small commits.

| Feedback | Change | Where |
|---|---|---|
| Replace inline styles with the shared CSS variables | 106 inline `style=""` attributes in 21 views replaced by utility classes in `site.css`. Only data-driven values (chart bar heights, animal colour gradients) stay inline | `refactor/shared-css` |
| Give each class its own file | 20 grouped files split into one file per type | `refactor/one-type-per-file` |
| Replace the placeholder SMS sender | `TwilioSmsStrategy` sends real SMS for urgent shift messages, falling back to email | `feature/sms-provider`, [DEPLOYMENT.md](docs/DEPLOYMENT.md#text-messages-sms) |
| Add MFA for admin accounts | Authenticator-app two-step verification with recovery codes, required for admins in production | `feature/admin-mfa` |
| Fix the failing production deploy | Deploy failures now print the live `/version` and `/health` and hints, to find the cause | `fix/deploy-diagnostics` |
| Commit history and solo workflow | Descriptive commits made as the work happened. The docs no longer claim team reviews | this file, [CONTRIBUTING.md](CONTRIBUTING.md) |

