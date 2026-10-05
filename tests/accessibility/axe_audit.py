"""Automated accessibility audit (WCAG 2.1 AA) of every PawConnect page, for every role,
at desktop and mobile widths, using axe-core in a real Chromium browser.

Run locally (app running on http://localhost:5231 with demo data):
    pip install playwright && python -m playwright install chromium
    npm install axe-core@4
    python tests/accessibility/axe_audit.py http://localhost:5231

Exits with code 1 if any violation is found, so CI fails on accessibility regressions.
"""
import os, sys
from playwright.sync_api import sync_playwright

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5231"
AXE = open(os.environ.get("AXE_PATH", "node_modules/axe-core/axe.min.js")).read()
PASSWORD = os.environ.get("DEMO_PASSWORD", "Demo!Paws2026")

# (account, pages). Special steps navigate through the UI to reach pages that need an id.
PLAN = {
    None: ["/", "/Home/Browse", "/Home/Donate", "/Account/Login", "/Account/Register", "/Home/Privacy", "/no-such-page"],
    "adopter@pawconnect.demo": ["/Applications/Mine", "/Account/Me", "@profile", "@apply"],
    "priya@pawconnect.demo": ["/Volunteer/Dashboard", "@profile"],
    "admin@pawconnect.demo": ["/Admin/Dashboard", "/Admin/Animals", "@edit-animal", "/Admin/Volunteers", "/Admin/Hours",
                              "/Admin/Users", "/Admin/Shifts", "/Admin/Donations", "/Admin/Reports", "@review"],
}

def open_page(pg, step):
    if step == "@profile":
        pg.goto(BASE + "/Home/Browse?search=Biscuit"); pg.click("a:has-text('View profile')"); pg.wait_for_timeout(500)
    elif step == "@apply":
        pg.goto(BASE + "/Home/Browse?available=true"); pg.locator("a:has-text('View profile')").first.click(); pg.click("a:has-text('Apply to adopt')")
    elif step == "@edit-animal":
        pg.goto(BASE + "/Admin/Animals"); pg.locator("a:has-text('Edit')").first.click()
    elif step == "@review":
        pg.goto(BASE + "/Admin/Dashboard"); pg.locator("td.mono a").first.click()
    else:
        pg.goto(BASE + step)

failures = 0
with sync_playwright() as p:
    browser = p.chromium.launch()
    for account, steps in PLAN.items():
        for width in (1280, 375):
            ctx = browser.new_context(viewport={"width": width, "height": 900}, bypass_csp=True)  # CSP blocks injected test scripts
            pg = ctx.new_page()
            if account:
                pg.goto(BASE + "/Account/Login"); pg.fill("#Email", account); pg.fill("#Password", PASSWORD); pg.click("button[type=submit]")
            for step in steps:
                open_page(pg, step)
                pg.add_script_tag(content=AXE)
                violations = pg.evaluate("""async () => (await axe.run(document, {runOnly: ['wcag2a','wcag2aa','wcag21aa','best-practice']}))
                    .violations.map(v => ({id: v.id, impact: v.impact, help: v.help, targets: v.nodes.slice(0, 3).map(n => n.target.join(' '))}))""")
                label = f"{account or 'anonymous'} {pg.url.replace(BASE, '')} @{width}px"
                if violations:
                    failures += len(violations)
                    for v in violations:
                        print(f"FAIL {label}: [{v['impact']}] {v['id']} - {v['help']} {v['targets']}")
                else:
                    print(f"ok   {label}")
            ctx.close()
    browser.close()

print(f"\n{failures} accessibility violation(s).")
sys.exit(1 if failures else 0)
