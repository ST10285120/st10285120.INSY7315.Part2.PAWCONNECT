# Contributing to PawConnect

We follow a **simplified Gitflow** (design doc 8.1). The rubric marks "branching and merge discipline throughout development", so every change goes through this flow.

## Branches
| Branch | Purpose | Who merges |
|---|---|---|
| `main` | What's live in production. Protected. | Via PR from `develop` (deploys after the production approval gate) |
| `develop` | Integration branch. Every merge deploys to **staging**. | Anyone, via a reviewed PR |
| `feature/<area>-<short-name>` | New work, e.g. `feature/shifts-cancel-signup` | — |
| `fix/<short-name>` | Bug fixes, e.g. `fix/apply-form-validation` | — |
| `hotfix/<short-name>` | Urgent production fix, branched from `main` and merged into both `main` and `develop` | Via PR |

## Everyday workflow
```bash
git checkout develop && git pull
git checkout -b feature/admin-shift-notes
# ...work, commit little and often...
git push -u origin feature/admin-shift-notes
# open a Pull Request into develop on GitHub → CI runs → review the diff → merge when the checks pass
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
- PawConnect is a **one-person project**, so I review my own PR diff before merging. The required CI checks (build, tests, coverage, package scan, accessibility) are what actually blocks a bad change. In a team, require one approving review from a teammate as well.
- Keep PRs small (under ~400 changed lines) so they're easy to review.

## One-time repository setup
1. Create the GitHub repository and push `main`, then create `develop` from it: `git checkout -b develop && git push -u origin develop`.
2. **Settings → Branches → Add rule** for `main` and `develop`: *Require a pull request* (0 approvals for a solo project, 1 in a team), *Require status checks* (`Build, test & scan`, `Accessibility audit (WCAG 2.1 AA)`), *Do not allow bypassing*.
3. **Settings → Environments**: `staging` and `production` (production gets *Required reviewers*).
4. Run `scripts/azure-setup.sh` (see `docs/DEPLOYMENT.md`).

## Running checks locally
```bash
dotnet test                                                   # unit + service + integration tests
docker compose up -d db && dotnet run --project src/PawConnect.Web
python tests/accessibility/axe_audit.py http://localhost:5231 # needs: pip install playwright; npm i axe-core@4
```
