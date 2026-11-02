"""Captures the annotated screenshots used in the PawConnect User Guide.

Signs in as each demo role, opens every page the guide describes, and saves a PNG with numbered
callouts drawn on the controls the guide's steps refer to. Re-run it whenever the UI changes so
the guide never shows out-of-date screens.

Run locally (app running with demo data, e.g. `dotnet run --project src/PawConnect.Web`):
    pip install playwright pillow && python -m playwright install chromium
    python tests/screenshots/capture_user_guide.py http://localhost:5231

Images go to docs/user-guide/screenshots/. A callout whose element is not on the page is skipped
with a warning, so one renamed button never stops the whole run.
"""
import os, sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
from playwright.sync_api import sync_playwright

BASE = (sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5231").rstrip("/")
PASSWORD = os.environ.get("DEMO_PASSWORD", "Demo!Paws2026")
OUT = Path(os.environ.get("OUT_DIR", "docs/user-guide/screenshots"))
ADOPTER, VOLUNTEER, ADMIN = "adopter@pawconnect.demo", "priya@pawconnect.demo", "admin@pawconnect.demo"

# name, account, page (or @step), callout selectors in the order the guide numbers them,
# optional: width (default 1440), focus (selector to crop around instead of the top of the page).
SHOTS = [
    ("home", None, "/", ["a.btn:has-text('Browse animals')", "a.btn:has-text('Donate or sponsor')",
                          "nav a:has-text('Log in')", "nav a:has-text('Register')", ".stats-strip", "a.btn:has-text('Meet')"]),
    ("browse", None, "/Home/Browse", ["select#species, select[name=species]", "select#size, select[name=size]",
                                       "label:has-text('Available only')", "input[name=search]", "a:has-text('View profile')", ".stamp"]),
    ("browse-mobile", None, "/Home/Browse", [".hamburger", "select[name=species]", "button:has-text('Search')"], 375),
    ("register", None, "/Account/Register", ["#FirstName", "#Email", "#Password", "#AcceptPrivacy", "button[type=submit]"]),
    ("login", None, "/Account/Login", ["#Email", "#Password", "#RememberMe", "button[type=submit]"]),
    ("profile", ADOPTER, "@profile-available", [".stamp", "a:has-text('Apply to adopt')", "a:has-text('Sponsor')"]),
    ("apply", ADOPTER, "@apply", ["label:has-text('Full name')", "label:has-text('Why would you like')", "button:has-text('Next: Your home')"]),
    ("my-applications", ADOPTER, "/Applications/Mine", [".stamp", "ol.track", "button:has-text('Withdraw')"]),
    ("donate", ADOPTER, "/Home/Donate", ["label:has-text('One-time')", "label:has-text('Choose an amount')",
                                         "label:has-text('Sponsor a specific animal')", "button:has-text('Pledge')"]),
    ("my-account", ADOPTER, "/Account/Me", ["h2:has-text('My details')", "h2:has-text('My donations')",
                                            "a:has-text('Download my data')", "a:has-text('Manage two-step')"]),
    ("two-factor", ADOPTER, "/Account/TwoFactor", [".setup-steps", ".setup-key", "#Code", "button:has-text('Turn on')"]),
    ("volunteer-dashboard", VOLUNTEER, "/Volunteer/Dashboard", ["h2:has-text('Applications to review')", "h2:has-text('Upcoming shifts')",
                                                               "button:has-text('Sign up')", "h2:has-text('Log hours')", ".mini-stat"]),
    ("medical-record", VOLUNTEER, "@profile", ["h2:has-text('Medical history')", "#medicalForm select", "#medicalForm label:has-text('What was done')",
                                           "#medicalForm label:has-text('Date')", "#medicalForm button[type=submit]"],
     1440, "#medicalForm"),
    ("application-review", ADMIN, "@review", ["h2:has-text('History')", "h2:has-text('Next step')"]),
    ("admin-dashboard", ADMIN, "/Admin/Dashboard", [".admin-subnav", ".stat-cards", ".stat-card:has-text('Pledges awaiting')", "h2:has-text('Recent applications')", "a.btn:has-text('Reports')"]),
    ("admin-animals", ADMIN, "/Admin/Animals", ["button:has-text('Save')", "a:has-text('Edit')", "h2:has-text('Record a new arrival')", "button:has-text('Add animal')"]),
    ("admin-edit-animal", ADMIN, "@edit-animal", ["button:has-text('Save details')", "h2:has-text('Status')", "h2:has-text('Photos')", "button:has-text('Upload')"]),
    ("admin-shifts", ADMIN, "/Admin/Shifts", ["table", "h2:has-text('Create a shift')", "button:has-text('Create shift')"]),
    ("admin-volunteers", ADMIN, "/Admin/Volunteers", ["table", "a:has-text('Add volunteer')"]),
    ("admin-hours", ADMIN, "/Admin/Hours", ["button:has-text('Filter')", "button:has-text('Approve')", "h2:has-text('Approved hours by volunteer')"]),
    ("admin-users", ADMIN, "/Admin/Users", ["button:has-text('Make')", "button:has-text('Deactivate')", "h2:has-text('Add a staff member')", "button:has-text('Create account')"]),
    ("admin-donations", ADMIN, "/Admin/Donations", ["button:has-text('Mark received')", "h2:has-text('Received in the last 30 days')", "a:has-text('Full report')"]),
    ("funder-report", ADMIN, "/Admin/Reports", ["input[type=date]", "button:has-text('Run report')", "a:has-text('Export donations')",
                                                ".stat-cards", "h2:has-text('Donations by type')", "h2:has-text('Service levels')", "h2:has-text('Donations in range')"]),
]

def open_page(pg, step):
    if step == "@profile":
        pg.goto(BASE + "/Home/Browse?search=Milo"); pg.locator("a:has-text('View profile')").first.click()
    elif step == "@profile-available":
        pg.goto(BASE + "/Home/Browse?available=true"); pg.locator("a:has-text('View profile')").first.click()
    elif step == "@apply":
        pg.goto(BASE + "/Home/Browse?available=true"); pg.locator("a:has-text('View profile')").first.click()
        pg.click("a:has-text('Apply to adopt')")
    elif step == "@edit-animal":
        pg.goto(BASE + "/Admin/Animals"); pg.locator("a:has-text('Edit')").first.click()
    elif step == "@review":
        pg.goto(BASE + "/Admin/Dashboard"); pg.locator("td.mono a").first.click()
    else:
        pg.goto(BASE + step)
    pg.wait_for_load_state("networkidle")

def callout_boxes(pg, selectors):
    boxes = []
    for sel in selectors:
        loc = pg.locator(sel).first
        try:
            box = loc.bounding_box(timeout=1500) if loc.count() else None
        except Exception:
            box = None
        if not box:
            print(f"  warning: no element for {sel!r}")
        boxes.append(box)
    return boxes

def draw(path, boxes, offset_y=0):
    im = Image.open(path).convert("RGB"); d = ImageDraw.Draw(im)
    try: font = ImageFont.truetype("DejaVuSans-Bold.ttf", 19)
    except OSError: font = ImageFont.load_default()
    clay = (181, 72, 47); r = 17
    for n, b in enumerate(boxes, 1):
        if not b: continue
        x0, y0, x1, y1 = b["x"], b["y"] - offset_y, b["x"] + b["width"], b["y"] + b["height"] - offset_y
        if y1 < 0 or y0 > im.height: continue
        d.rounded_rectangle((x0 - 4, y0 - 4, x1 + 4, y1 + 4), radius=6, outline=clay, width=3)
        cx, cy = max(r + 4, x0 - r - 8), max(r + 4, min(im.height - r - 4, (y0 + y1) / 2))
        d.ellipse((cx - r - 3, cy - r - 3, cx + r + 3, cy + r + 3), fill="white")
        d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=clay)
        t = str(n); d.text((cx - d.textlength(t, font=font) / 2, cy - 12), t, fill="white", font=font)
    im.save(path, optimize=True)

OUT.mkdir(parents=True, exist_ok=True)
with sync_playwright() as p:
    browser = p.chromium.launch()
    for shot in SHOTS:
        name, account, step, selectors = shot[:4]
        width = shot[4] if len(shot) > 4 else 1440
        focus = shot[5] if len(shot) > 5 else None
        ctx = browser.new_context(viewport={"width": width, "height": 900})
        pg = ctx.new_page()
        if account:
            pg.goto(BASE + "/Account/Login"); pg.fill("#Email", account); pg.fill("#Password", PASSWORD)
            pg.click("button[type=submit]"); pg.wait_for_load_state("networkidle")
        print(f"{name}: {account or 'anonymous'} {step}")
        open_page(pg, step)
        pg.add_style_tag(content=".toast{display:none !important}")  # hide the "Welcome back" toast
        pg.evaluate("window.scrollTo(0, 0)")
        boxes = callout_boxes(pg, selectors)
        path = OUT / f"{name}.png"
        top = 0
        if focus:
            fb = pg.locator(focus).first.bounding_box()
            top = max(0, int(fb["y"]) - 120) if fb else 0
        page_h = pg.evaluate("document.documentElement.scrollHeight")
        height = min(1300 if width > 500 else 1500, page_h - top)
        pg.screenshot(path=str(path), full_page=True, clip={"x": 0, "y": top, "width": width, "height": height})
        draw(path, boxes, offset_y=top)
        ctx.close()
    browser.close()
print(f"Saved {len(SHOTS)} screenshots to {OUT}/")
