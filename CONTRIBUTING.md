# Contributing to PawConnect

We follow a **simplified Gitflow** (design doc 8.1). The rubric marks "branching and merge discipline throughout development", so every change goes through this flow.

## Branches
| Branch | Purpose | Who merges |
|---|---|---|
| `main` | What's live in production. Protected. | Team leader, via PR from `develop` (deploys after approval) |
| `develop` | Integration branch. Every merge deploys to **staging**. | Anyone, via a reviewed PR |
| `feature/<area>-<short-name>` | New work, e.g. `feature/shifts-cancel-signup` | — |
| `fix/<short-name>` | Bug fixes, e.g. `fix/apply-form-validation` | — |
| `hotfix/<short-name>` | Urgent production fix, branched from `main` and merged into both `main` and `develop` | Team leader |

## Everyday workflow
```bash
git checkout develop && git pull
git checkout -b feature/admin-shift-notes
# ...work, commit little and often...
git push -u origin feature/admin-shift-notes
# open a Pull Request into develop on GitHub → CI runs → a teammate reviews → squash-merge
```

## Commit messages ([Conventional Commits](https://www.conventionalcommits.org/))
```
feat(shifts): let volunteers cancel a sign-up before the shift starts
fix(apply): keep the draft when validation fails
test(adoption): cover rejection returning the animal to Available
docs(readme): add Azure setup steps
ci: run accessibility audit on pull requests
refactor(reports): move CSV export into ReportService
```
Keep each commit focused on one thing, and describe *what* and *why*, not "update" or "fixes".

## Pull requests
- Fill in the PR template (what, how tested, checklist).
- CI must be green: build, tests, vulnerability scan, migration check and accessibility audit.
- At least **one approving review** from a teammate. Reviewers check behaviour, tests and naming.
- Keep PRs small (under ~400 changed lines) so they're easy to review.

## One-time repository setup (team leader)
1. Create the GitHub repository and push `main`, then create `develop` from it: `git checkout -b develop && git push -u origin develop`.
2. **Settings → Branches → Add rule** for `main` and `develop`: *Require a pull request* (1 approval), *Require status checks* (`Build, test & scan`, `Accessibility audit (WCAG 2.1 AA)`), *Do not allow bypassing*.
3. **Settings → Environments**: `staging` and `production` (production gets *Required reviewers*).
4. Run `scripts/azure-setup.sh` (see `docs/DEPLOYMENT.md`).

## Suggested ownership (adjust to your group)
Each member owns an area. They make their own feature branches and PRs for it, and review a teammate's area.

| Area | Main files | Good first PRs |
|---|---|---|
| Public site and adoption | `HomeController`, `ApplicationsController`, `Views/Home`, `Views/Applications`, `site.js` | Browse filter improvements, application form copy |
| Volunteer workspace | `VolunteerController`, `ShiftService`, `HourLogService` | Shift reminders, hours export |
| Admin and reports | `AdminController`, `ReportService`, `Views/Admin` | Extra report charts, animal search in admin |
| API, security and data | `Controllers/Api`, `Security`, `Infrastructure` | New endpoint + tests, migration for a new field |
| DevOps and QA | `.github/workflows`, `infra`, `tests` | Coverage threshold, extra smoke checks, deployment |

## Running checks locally
```bash
dotnet test                                                   # unit + service + integration tests
docker compose up -d db && dotnet run --project src/PawConnect.Web
python tests/accessibility/axe_audit.py http://localhost:5231 # needs: pip install playwright; npm i axe-core@4
```
