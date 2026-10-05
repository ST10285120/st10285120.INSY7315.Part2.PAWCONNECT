# Design document updates for Task 3

Task 3 asks for the documentation to be improved based on feedback and to match what was built. The Task 1 design doc describes a React + Express + Azure AD B2C design, but the team built the prototype and the final system in **ASP.NET Core**. These are the sections to update so the report matches the code.

| Section | Task 1 says | Update to |
|---|---|---|
| 6.2 Architecture | React SPA + Express/Node.js API | ASP.NET Core 8 MVC (server-rendered Razor views) plus a REST API (`/api/*`) in the same app. Still a layered 3-tier modular monolith: Web → Core → Infrastructure → PostgreSQL. Reason: builds on the Task 1 prototype, one language (C#) across layers, one deployment. |
| 5.1 / 6.1 ORM | "parameterized statements via the ORM" | Entity Framework Core 8 with the Npgsql provider; migrations in `Infrastructure/Data/Migrations`. |
| 7.1 Authentication | Azure AD B2C issuing JWTs | ASP.NET Core Identity: salted PBKDF2 hashes, lockout, cookie auth for the site, and ASP.NET Core bearer tokens for API clients. Microsoft stopped offering Azure AD B2C to new customers in 2025 (check Microsoft's current docs before citing), and Identity avoids an extra paid service. |
| 7.3 OWASP table | Joi validation | DataAnnotations model validation + validation inside every service, anti-forgery tokens, CSP headers, rate limiting. |
| 8.2 CI/CD | ESLint + Jest | `dotnet build`, xUnit (132 tests, 13 of them on a real PostgreSQL database) with Coverlet coverage and a 70% coverage gate, a vulnerable-package scan, an EF migration check against PostgreSQL and an axe-core accessibility audit. Same staging → approval → slot swap → health check → rollback flow. |
| 6.3 Cloud | Static Web Apps (front end) + containerised API on App Service | A single App Service web app (code deploy of the published output). A Dockerfile is provided for local/containers; Static Web Apps aren't needed because pages are server-rendered. |
| 6.3 / 9 Blob storage | Photos/documents in Blob Storage | Animal photos stored in PostgreSQL (`AnimalPhotos`, 2 MB cap, max 6 per animal) so every slot and instance sees them. Blob Storage can be added later behind the same service. |
| 9 Running costs | App Service Free/B1 | **Deployment slots need Standard S1 or higher**, so either budget S1 (about R1,300/month; check the pricing calculator) or use B1 / the free F1 plan with a second staging app on the same plan (no slots, no blue-green). The Bicep template and pipeline support all three. PostgreSQL B1ms is free for 12 months on a new Azure account. Update the cost table accordingly. |
| 5.2 ERD / 5.3 Data dictionary | 6 tables, snake_case | Tables are PascalCase (EF Core default). Added: `ShiftSignups` (with unique index), `HourLogs`, `ApplicationStatusChanges` (audit trail), `AnimalPhotos`, `Notifications` (message log), `DataProtectionKeys`, plus the ASP.NET Identity tables. Regenerate the ERD from the database. |
| 4.3 State diagram | Adopted requires a home visit + fee | Implemented exactly: Submitted → UnderReview → Approved → Adopted; Rejected/Withdrawn terminal; a partial unique index enforces one open application per animal, and a concurrency token stops two simultaneous decisions (e.g. approve vs. withdraw) from both being saved. |
| 5.3 Data dictionary constraints | Column types only | Add the CHECK constraints (amount > 0, hours 0–24, capacity 1–50, shift ends after start, enum values) and partial unique indexes (kennel in use, one adoption fee, one primary photo) from `EntityConfigurations.cs`. |
| 7.2 Authorisation | Role-based access | Roles plus ownership checks (404 for other people's records) and branch scoping for volunteers (`BranchAccess`). |
| 6.1 Strategy | email/SMS strategies | Email (SMTP, e.g. Azure Communication Services) is implemented; SMS is a pluggable placeholder (`LogOnlySmsStrategy`) until a provider is chosen. Retry with exponential backoff and SMS→email fallback are implemented. |
| 2.2 User stories | — | Add the volunteer "log my hours" and administrator "approve hours" stories (already in the prototype). |
| 10.5 Handover | — | Point to `README.md`, `docs/DEPLOYMENT.md` and `infra/main.bicep` as the administrator runbook. |

**Screenshots / evidence to collect for the report:** GitHub Actions runs (CI summary with coverage, deploy with approval and swap), branch protection settings, merged PRs, the live Azure URL, Application Insights, and the Key Vault secrets list (names only).
