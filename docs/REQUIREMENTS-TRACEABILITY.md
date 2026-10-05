# Requirements traceability

Every user story, acceptance criterion and non-functional requirement from the Task 1 design document (sections 1.6, 2.2 and 2.6), mapped to where it is implemented and the automated test that proves it. Test names are in `tests/PawConnect.Tests`; the accessibility audit is in `tests/accessibility`.

## User stories

### Adopter / public
| # | User story | Implemented in | Verified by |
|---|---|---|---|
| A1 | Browse adoptable animals with photos and bios | `/Home/Browse` with live filters via `GET /api/animals`; `/Home/Profile/{id}` gallery | `ApiTests.Public_can_list_and_filter_animals` |
| A2 | Submit an adoption application online | Multi-step form with autosave, `AdoptionService.SubmitAsync`, `POST /api/applications` | `AdoptionServiceTests.Submitting_creates_application_marks_animal_pending_and_emails_the_applicant`, `Incomplete_applications_are_rejected`, `ApiTests.Full_adoption_workflow_through_the_api` |
| A3 | Track the status of my application | `/Applications/Mine` progress tracker, `GET /api/applications/mine` | `ApiTests.Full_adoption_workflow_through_the_api` |
| A4 | Donate or sponsor a specific animal | `/Home/Donate`, `DonationService.PledgeAsync`, `POST /api/donations`, receipt page | `DonationServiceTests.General_pledge_gets_receipt_number_and_receipt_email`, `Sponsoring_an_animal_links_the_donation`, `Adopted_animals_cannot_be_sponsored` |
| A5 | Email when my application is received and approved | `NotificationTemplates` + `NotificationService` (Strategy) | `AdoptionServiceTests.Submitting_creates…emails_the_applicant`, `Approval_is_only_allowed_after_review_starts` (asserts the approval email) |

### Volunteer
| # | User story | Implemented in | Verified by |
|---|---|---|---|
| V1 | View submitted applications for my branch | `/Volunteer/Dashboard` (branch-filtered for volunteers, all branches for admins); `BranchAccess` enforces the same rule on the application page, every review action and the API | `ApiTests.Html_pages_render_for_each_role`, `AuthorizationAndErrorTests.A_volunteer_from_another_branch_cannot_see_or_decide_an_application` |
| V2 | Log a medical record after a vet visit | Profile → `POST /api/medical-records`, `MedicalRecordService` | `ApiTests.Volunteers_can_log_medical_records_but_adopters_cannot`, `MedicalRecordServiceTests.*` |
| V3 | Sign up for an open shift | `/Volunteer/Dashboard`, `ShiftService.SignUpAsync`, `POST /api/shifts/{id}/signup` | `ShiftServiceTests.Volunteer_can_sign_up_and_gets_an_urgent_confirmation`, `ApiTests.Shift_sign_up_via_api_and_capacity_is_enforced` |
| V4 | See which shifts are still unfilled | Shift list shows filled/capacity; `GET /api/shifts?openOnly=true` | `ApiTests.Shift_sign_up_via_api_and_capacity_is_enforced` |
| V5 | Log my volunteer hours *(from the prototype)* | `HourLogService.LogAsync` | `HourLogServiceTests.*` |

### Shelter administrator
| # | User story | Implemented in | Verified by |
|---|---|---|---|
| M1 | Record a new animal's intake details | `/Admin/Animals`, `AnimalService.CreateAsync`, photo upload | `AnimalServiceTests.New_animals_start_available_in_the_default_branch`, `Kennel_numbers_cannot_be_shared…`, `Photos_are_validated…` |
| M2 | Dashboard of adoptions, donations and volunteer hours | `/Admin/Dashboard`, `ReportService.GetDashboardAsync` | `ReportServiceTests.Dashboard_counts_animals_in_care`, `Monthly_totals_group_received_donations_by_shelter_month` |
| M3 | Add or deactivate volunteer and staff accounts | `/Admin/Users` (create, grant or remove roles, deactivate) | `AccountTests.Admin_can_create_a_volunteer_and_deactivate_them` |
| M4 | Export a donations report for a date range | `/Admin/Reports` → CSV, `GET /api/reports/donations.csv` | `ReportServiceTests.Summary_splits_donations_by_type…`, `ApiTests.Admin_only_pages_and_reports_are_protected`, `HelperTests.Csv_fields_are_quoted_and_formula_injection_is_neutralised` |
| M5 | Approve volunteer hours *(from the prototype)* | `/Admin/Hours`, `HourLogService.ReviewAsync` | `HourLogServiceTests.Admin_review_counts_once_and_only_approved_hours_are_summed` |

## Acceptance criteria (design doc 2.2)
| Criterion | Where | Test |
|---|---|---|
| Submitting creates an application with status Submitted, shows a confirmation and sends an email | `AdoptionService.SubmitAsync`, `/Applications/Confirmation` | `AdoptionServiceTests.Submitting_creates_application…` |
| An animal already Pending or Adopted must not show an active Apply button | `Profile.cshtml` (button disabled); the server also refuses (`SubmitAsync`) plus a partial unique index | `AdoptionServiceTests.A_pending_animal_cannot_receive_a_second_application` |
| A medical record is linked to the animal and visible in its history to staff | `MedicalRecordService`, `GET /api/animals/{id}?include=history` | `ApiTests.Medical_history_is_staff_only`, `MedicalRecordServiceTests.Logging_a_vaccination…` |
| State diagram: Approved only from Under Review; Adopted only after home visit + fee; Rejected can never become Adopted | `ApplicationStateMachine`, `AdoptionService.TransitionAsync` guards | `ApplicationStateMachineTests.*`, `AdoptionServiceTests.Completing_an_adoption_requires_home_visit_and_fee…`, `Rejecting_returns_the_animal_to_available_and_cannot_be_undone` |

## Non-functional requirements (design doc 2.6)
| NFR | How it is met | Evidence |
|---|---|---|
| **Performance:** API < 500 ms | Indexed queries, `AsNoTracking`, projections, caching, compression | Measured p95 ≤ 109 ms locally (`docs/ARCHITECTURE.md` §6); Application Insights once deployed |
| **Availability:** 99% | App Service SLA, health-check-based instance replacement, zero-downtime slot swaps with automatic rollback | `deploy.yml`, `scripts/smoke-test.sh` |
| **Security:** TLS 1.2+, salted hashes, RBAC on every endpoint | HTTPS only + HSTS, Identity PBKDF2, `[Authorize]` everywhere, CSRF, lockout, rate limits, CSP | `ApiTests.Responses_carry_security_headers`, `Cookie_authenticated_api_calls_need_the_csrf_header`, `Medical_history_is_staff_only`, `AccountTests.Account_locks_after_five_failed_sign_ins`, `AuthorizationAndErrorTests` (ownership and branch checks, deactivated-account enumeration) |
| **Data integrity** (ERD, data dictionary) | Concurrency tokens, CHECK constraints, partial unique indexes, restricted deletes | `PostgresIntegrityTests` (13 tests on PostgreSQL 16 in CI) |
| **Usability:** apply in under 5 minutes | Three-step form, pre-filled from the account, autosave, live filters | E2E walkthrough; demo in the presentation |
| **Scalability:** 1 → 3 branches without redesign | `ShelterBranches` table; animals, shifts and staff carry `BranchId`; volunteers see their branch | Data model (`docs/ARCHITECTURE.md` §4) |
| **Compatibility:** latest Chrome/Safari/Edge; 360–1920 px | Standards-based HTML/CSS and plain JavaScript (no build step), responsive layout, pages work without JavaScript | Accessibility audit runs at 375 px and 1280 px for every page |
| **Maintainability:** ≥ 70% test coverage on the API layer | 132 automated tests (incl. 13 on real PostgreSQL); CI publishes a coverage report on every run and **fails below 70%** | `ci.yml` coverage summary and "Enforce minimum line coverage" step |
| **Legal / POPIA** | Consent checkbox with timestamp, privacy notice, data minimisation, download my data, no third-party sharing | `AccountTests.Registration_requires_popia_consent…`, `Users_can_download_all_their_data` |
| **Backup & recovery:** daily, 7-day point-in-time restore, RTO 8 h | Azure PostgreSQL automated backups (`backupRetentionDays: 7`) | `infra/main.bicep` |
| **Cost:** ≤ R1,500/month | B1ms PostgreSQL, one App Service plan (S1 for blue-green, or B1 / free F1), free Key Vault / Insights tiers, email instead of SMS; a free Render + Neon option | `docs/DEPLOYMENT.md` cost note, `docs/DEPLOYMENT-FREE.md` |
| **Accessibility** *(front-end rubric)* | WCAG 2.1 AA: contrast, headings, landmarks, labels, keyboard, focus, `aria-live` | `tests/accessibility/axe_audit.py`: **0 violations**, runs in CI |

## Success criteria (design doc 1.6)
| Criterion | How PawConnect supports it |
|---|---|
| ≥ 70% of applications online within 2 months | Online application from every profile; staff capture phone applications in the same system |
| Decision time from 5 days to under 2 days | Review queue on the volunteer dashboard; the report shows **average time to decision** against the 2-day target |
| Zero shift double bookings | Capacity, duplicate and overlap checks + optimistic concurrency + unique index (`ShiftServiceTests.*`) |
| Report for any date range in under 10 s | `/Admin/Reports` + CSV export, indexed queries (well under a second locally) |
| UAT with no critical defects | 132 automated tests + accessibility audit + smoke tests on every deployment |
| Hosting within R1,500/month | See the cost note in `docs/DEPLOYMENT.md` |
