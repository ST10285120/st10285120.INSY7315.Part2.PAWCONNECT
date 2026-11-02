// Builds the PawConnect User Guide (.docx) from the text below and the annotated screenshots.
//   1. Capture the screenshots: python tests/screenshots/capture_user_guide.py http://localhost:5231
//   2. Build:                   npm install docx@9 && node docs/user-guide/build_guide.js docs/user-guide/screenshots docs/user-guide/PawConnect_User_Guide.docx
// Any screenshot that is missing is shown as a dashed placeholder naming the file to capture.
const fs = require("fs");
const path = require("path");
const {
  Document, Packer, Paragraph, TextRun, ImageRun, Table, TableRow, TableCell, AlignmentType, HeadingLevel,
  LevelFormat, WidthType, BorderStyle, ShadingType, PageBreak, TableOfContents, Footer, Header, PageNumber,
  PositionalTab, PositionalTabAlignment, PositionalTabRelativeTo, PositionalTabLeader, ExternalHyperlink, LineRuleType,
} = require("docx");
const { imageSize } = require("./imgsize");

const SHOTS = process.argv[2];
const OUT = process.argv[3] || "PawConnect_User_Guide.docx";

const FOREST = "1F3A2E", MARIGOLD = "E8A33D", CLAY = "B5482F", KRAFT = "F7F1DE", INK = "262420", SOFT = "5B5648";
const PAGE_W = 11906, MARGIN = 1247, CONTENT_W = PAGE_W - 2 * MARGIN; // A4, 2.2 cm margins
const PROD = "https://pawconnect-st10285120-fbfcdpfnf5g2h0bk.southafricanorth-01.azurewebsites.net";
const STAGING = "https://pawconnect-st10285120-staging-g0dzbfbtc8atbfcw.southafricanorth-01.azurewebsites.net";

let figNo = 0;
const figures = [];
const missing = [];

// ---------- inline helpers ----------
// Text with **bold** and `code` spans.
function runs(text, base = {}) {
  const out = [];
  const re = /(\*\*[^*]+\*\*|`[^`]+`)/g;
  let last = 0, m;
  while ((m = re.exec(text))) {
    if (m.index > last) out.push(new TextRun({ text: text.slice(last, m.index), ...base }));
    const t = m[0];
    if (t.startsWith("**")) out.push(new TextRun({ text: t.slice(2, -2), bold: true, ...base }));
    else out.push(new TextRun({ text: t.slice(1, -1), font: "Consolas", size: 19, color: FOREST, ...base }));
    last = m.index + t.length;
  }
  if (last < text.length) out.push(new TextRun({ text: text.slice(last), ...base }));
  return out;
}
const P = (text, opts = {}) => new Paragraph({ children: runs(text), spacing: { after: 120 }, ...opts });
const H1 = (t) => new Paragraph({ heading: HeadingLevel.HEADING_1, children: [new TextRun(t)], pageBreakBefore: true });
const H2 = (t) => new Paragraph({ heading: HeadingLevel.HEADING_2, children: [new TextRun(t)] });
const H3 = (t) => new Paragraph({ heading: HeadingLevel.HEADING_3, children: [new TextRun(t)] });
const bullets = (items, ref = "bullets") => items.map((t) => new Paragraph({ numbering: { reference: ref, level: 0 }, children: runs(t), spacing: { after: 60 } }));
let listId = 0;
const steps = (items) => { const ref = `steps-${++listId}`; numberingConfigs.push(stepConfig(ref)); return items.map((t) => new Paragraph({ numbering: { reference: ref, level: 0 }, children: runs(t), spacing: { after: 80 } })); };
const numberingConfigs = [];
function stepConfig(ref) {
  return { reference: ref, levels: [{ level: 0, format: LevelFormat.DECIMAL, text: "%1.", alignment: AlignmentType.LEFT,
    style: { paragraph: { indent: { left: 500, hanging: 360 } }, run: { bold: true, color: FOREST } } }] };
}

const border = { style: BorderStyle.SINGLE, size: 4, color: "D9D2BE" };
const borders = { top: border, bottom: border, left: border, right: border };
function table(headers, rows, widths) {
  const total = widths.reduce((a, b) => a + b, 0);
  const cell = (text, i, head) => new TableCell({
    borders, width: { size: widths[i], type: WidthType.DXA },
    shading: head ? { fill: FOREST, type: ShadingType.CLEAR, color: "auto" } : undefined,
    margins: { top: 70, bottom: 70, left: 110, right: 110 },
    children: [new Paragraph({ children: runs(text, head ? { bold: true, color: "FFFFFF", size: 19 } : { size: 19 }) })],
  });
  return new Table({
    width: { size: total, type: WidthType.DXA }, columnWidths: widths,
    rows: [new TableRow({ tableHeader: true, children: headers.map((h, i) => cell(h, i, true)) }),
      ...rows.map((r) => new TableRow({ cantSplit: true, children: r.map((c, i) => cell(c, i, false)) }))],
  });
}
const gap = () => new Paragraph({ children: [], spacing: { after: 60 } });

function note(text, label = "Tip") {
  return new Table({
    width: { size: CONTENT_W, type: WidthType.DXA }, columnWidths: [CONTENT_W],
    rows: [new TableRow({ children: [new TableCell({
      width: { size: CONTENT_W, type: WidthType.DXA },
      shading: { fill: KRAFT, type: ShadingType.CLEAR, color: "auto" },
      borders: { top: { style: BorderStyle.NONE }, bottom: { style: BorderStyle.NONE }, right: { style: BorderStyle.NONE },
        left: { style: BorderStyle.SINGLE, size: 24, color: label === "Important" ? CLAY : MARIGOLD } },
      margins: { top: 100, bottom: 100, left: 180, right: 140 },
      children: [new Paragraph({ children: [new TextRun({ text: label + ": ", bold: true, color: label === "Important" ? CLAY : FOREST }), ...runs(text)] })],
    })] })],
  });
}

// A screenshot with its numbered legend. `legend` order matches the callout numbers on the image.
function figure(file, caption, legend = []) {
  figNo++;
  figures.push({ n: figNo, file, caption });
  const out = [];
  const p = path.join(SHOTS, file);
  if (fs.existsSync(p)) {
    const { width, height } = imageSize(p);
    const maxW = file.includes("mobile") ? 3000 : CONTENT_W; // DXA
    const wPx = Math.round((maxW / 1440) * 96);              // DXA -> px at 96 dpi
    let w = wPx, h = Math.round((height / width) * wPx);
    const maxH = 560; if (h > maxH) { w = Math.round(w * maxH / h); h = maxH; }
    out.push(new Paragraph({ alignment: AlignmentType.CENTER, keepNext: true, spacing: { before: 120, after: 60 },
      children: [new ImageRun({ type: "png", data: fs.readFileSync(p), transformation: { width: w, height: h },
        altText: { title: caption, description: caption, name: file } })] }));
  } else {
    missing.push(file);
    out.push(new Table({
      width: { size: CONTENT_W, type: WidthType.DXA }, columnWidths: [CONTENT_W],
      rows: [new TableRow({ height: { value: 1500, rule: "atLeast" }, children: [new TableCell({
        width: { size: CONTENT_W, type: WidthType.DXA },
        shading: { fill: "FBF8EF", type: ShadingType.CLEAR, color: "auto" },
        borders: { top: { style: BorderStyle.DASHED, size: 8, color: MARIGOLD }, bottom: { style: BorderStyle.DASHED, size: 8, color: MARIGOLD },
          left: { style: BorderStyle.DASHED, size: 8, color: MARIGOLD }, right: { style: BorderStyle.DASHED, size: 8, color: MARIGOLD } },
        verticalAlign: "center",
        children: [
          new Paragraph({ alignment: AlignmentType.CENTER, children: [new TextRun({ text: `[Screenshot: ${file}]`, bold: true, color: CLAY })] }),
          new Paragraph({ alignment: AlignmentType.CENTER, children: [new TextRun({ text: "Run tests/screenshots/capture_user_guide.py to capture this annotated screen.", size: 18, color: SOFT })] }),
        ],
      })] })],
    }));
  }
  out.push(new Paragraph({ style: "Caption", alignment: AlignmentType.CENTER, keepNext: legend.length > 0,
    children: [new TextRun({ text: `Figure ${figNo}: `, bold: true }), new TextRun(caption)] }));
  if (legend.length) {
    const ref = `legend-${++listId}`;
    numberingConfigs.push({ reference: ref, levels: [{ level: 0, format: LevelFormat.DECIMAL, text: "%1", alignment: AlignmentType.CENTER,
      style: { paragraph: { indent: { left: 560, hanging: 400 } }, run: { bold: true, color: "FFFFFF", shading: { type: ShadingType.CLEAR, fill: CLAY, color: "auto" } } } }] });
    legend.forEach((t, i) => out.push(new Paragraph({ numbering: { reference: ref, level: 0 }, keepNext: i < legend.length - 1,
      children: runs(t, { size: 19 }), spacing: { after: 40 } })));
    out.push(gap());
  }
  return out;
}

// ---------- content ----------
const body = [];
const add = (...xs) => xs.flat().forEach((x) => body.push(x));

// 1 Introduction
add(H1("1. Introduction"),
  P("PawConnect is the online system for **Hope & Paws Animal Shelter**. It replaces the paper intake cards, the shared spreadsheet and the WhatsApp shift group with one website that the public, volunteers and shelter staff all use."),
  P("This guide explains how to use every part of PawConnect, step by step, for each kind of user. It also covers the technical setup for the volunteer who looks after the system, and answers common questions."),
  H2("1.1 Who this guide is for"),
  table(["You are…", "Your role in PawConnect", "Read"], [
    ["A member of the public who wants to adopt, donate or sponsor", "Adopter (anyone who registers)", "Sections 2 and 3"],
    ["A shelter volunteer or part-time staff member", "Volunteer", "Sections 2 and 4"],
    ["The shelter manager or another administrator", "Shelter administrator", "Sections 2, 4 and 5"],
    ["The technical volunteer who runs the system", "Administrator, plus server access", "Sections 6 and 7"],
  ], [3600, 2900, 2912]),
  gap(),
  H2("1.2 What each role can do"),
  table(["Feature", "Public / adopter", "Volunteer", "Administrator"], [
    ["Browse adoptable animals", "✓ (no login needed)", "✓", "✓"],
    ["Register, manage own details, download own data", "✓", "✓", "✓"],
    ["Apply to adopt and track applications", "✓", "–", "–"],
    ["Donate or sponsor an animal", "✓", "✓", "✓"],
    ["Review, approve and reject applications", "–", "✓ (own branch)", "✓ (all branches)"],
    ["Log medical records", "–", "✓", "✓"],
    ["Sign up for shifts and log hours", "–", "✓", "✓"],
    ["Manage animals, shifts, users, donations and reports", "–", "–", "✓"],
  ], [3800, 1900, 1800, 1912]),
  gap(),
  H2("1.3 How to read this guide"),
  bullets([
    "Numbered steps tell you exactly what to click or type, in order.",
    "Screenshots have **numbered red markers**. The list under each screenshot explains what each number points to.",
    "Button and page names are shown in **bold**, exactly as they appear on screen.",
    "Boxes marked **Tip** give shortcuts. Boxes marked **Important** warn you about something that can't easily be undone.",
  ]),
);

// 2 Getting started
add(H1("2. Getting started"),
  H2("2.1 What you need"),
  table(["Item", "Requirement"], [
    ["Device", "Any computer, tablet or phone. The site adapts to screens from 360 px (small phones) to 1920 px (large monitors)."],
    ["Browser", "One of the two latest versions of Google Chrome, Microsoft Edge or Safari. Firefox also works."],
    ["Internet", "Any working connection. Pages are small and load quickly on mobile data."],
    ["Email address", "Needed to register. Confirmations and receipts are sent there."],
    ["Phone (optional)", "Volunteers who add a mobile number get urgent shift messages by SMS."],
    ["Authenticator app", "Only for two-step verification: Microsoft Authenticator or Google Authenticator. Required for administrators."],
  ], [2200, 7212]),
  gap(),
  P("PawConnect also works with JavaScript switched off, and with screen readers and keyboard-only navigation. It was checked against the WCAG 2.1 AA accessibility standard."),
  H2("2.2 Opening PawConnect"),
  table(["Site", "Address", "Use it for"], [
    ["Live site", "`pawconnect-st10285120-fbfcdpfnf5g2h0bk.southafricanorth-01.azurewebsites.net`", "Real shelter work. Holds real data."],
    ["Staging (test) site", "`pawconnect-st10285120-staging-g0dzbfbtc8atbfcw.southafricanorth-01.azurewebsites.net`", "Training and trying things out. Holds demo animals and demo accounts, and is reset with demo data."],
  ], [1700, 4900, 2812]),
  gap(),
  note("New volunteers should practise on the staging site first. Ask the shelter's technical volunteer for the staging demo account details. They are not printed in this guide."),
  gap(),
  figure("home.png", "The PawConnect home page", [
    "**Browse animals**: see every animal looking for a home.",
    "**Donate or sponsor**: give a once-off or monthly donation, or sponsor one animal.",
    "**Log in**: for people who already have an account.",
    "**Register**: create a free account (needed to apply, donate or track an application).",
    "Live numbers: adoptions this year, animals in care, active volunteers and donations this month.",
    "The featured animal. **Meet** opens its profile.",
  ]),
  H2("2.3 Creating an account"),
  P("Anyone can browse animals without an account. You need one to apply to adopt, to donate, and to track an application."),
  ...steps([
    "Click **Register** at the top right of any page.",
    "Enter your first name, last name, email address and (optionally) a phone number.",
    "Choose a password of at least **8 characters** with an **uppercase letter**, a **lowercase letter** and a **number**, and type it again to confirm it.",
    "Read the privacy notice and tick the consent box. PawConnect follows South Africa's Protection of Personal Information Act (POPIA) and can't store your details without your consent.",
    "Click **Create account**. You are signed in straight away.",
  ]),
  figure("register.png", "Creating an account", [
    "Your name. It appears on applications and receipts.",
    "Your email address. You also use it to log in.",
    "Your password (8+ characters, with upper and lower case and a number).",
    "POPIA consent. Required before any personal information is stored.",
    "**Create account**.",
  ]),
  note("Everyone who registers on the website becomes an adopter. Volunteer and administrator accounts are created by an administrator (section 5.5)."),
  H2("2.4 Logging in and out"),
  ...steps([
    "Click **Log in** at the top right.",
    "Enter your email address and password.",
    "Tick **Keep me signed in** only on your own device. Otherwise you are signed out after 8 hours without activity.",
    "Click **Log in**. Volunteers land on the Volunteer dashboard, administrators on the Shelter dashboard and adopters on the home page.",
    "To log out, click **Log out** next to your name at the top right.",
  ]),
  figure("login.png", "Logging in", ["Email address.", "Password.", "**Keep me signed in** (own devices only).", "**Log in**."]),
  note("After **5 wrong passwords in a row**, the account is locked for **15 minutes** to stop people guessing passwords. Wait 15 minutes and try again, or ask an administrator.", "Important"),
  H2("2.5 Two-step verification"),
  P("Two-step verification asks for a 6-digit code from your phone as well as your password. Even if someone learns your password, they can't log in without your phone. Any user can turn it on. **Administrators must use it** on the live site."),
  H3("Turning it on"),
  ...steps([
    "Install **Microsoft Authenticator** or **Google Authenticator** on your phone.",
    "In PawConnect, open **My account** (click your name at the top right) and click **Manage two-step verification**.",
    "In the authenticator app, choose to add an account, pick **Enter a setup key**, and type the key shown on the page. If you are using PawConnect on the same phone, tap **Open it in your authenticator app** instead.",
    "Type the 6-digit code the app now shows for PawConnect and click **Turn on two-step verification**.",
    "PawConnect shows **8 recovery codes**. Save them somewhere safe, such as a password manager. Each one lets you log in once if you lose your phone. They are only shown once.",
  ]),
  figure("two-factor.png", "Setting up two-step verification", [
    "The three set-up steps.",
    "The setup key to type into your authenticator app.",
    "The 6-digit code from the app.",
    "**Turn on two-step verification**.",
  ]),
  H3("Logging in with two-step verification"),
  P("Log in with your email and password as usual. PawConnect then asks for a **Verification code**. Type the current 6-digit code from your app (codes change every 30 seconds) and click **Verify and log in**. If you have lost your phone, type one of your recovery codes instead."),
  H2("2.6 Finding your way around"),
  P("The dark green bar at the top of every page holds the main links. What you see depends on your role: adopters see **Home**, **Browse animals** and **Donate**. Volunteers also see **Volunteer dashboard**, and administrators also see **Shelter dashboard**. On a phone the links fold into the **☰** menu button."),
  figure("browse-mobile.png", "PawConnect on a phone", [
    "The **☰** menu button opens the page links.",
    "Filters stack one above the other on small screens.",
    "**Search** works the same as on a computer.",
  ]),
);

// 3 Adopters
add(H1("3. For adopters and the public"),
  H2("3.1 Browsing adoptable animals"),
  ...steps([
    "Click **Browse animals**.",
    "Narrow the list with the filters: **Species** (dogs or cats), **Size**, **Available only**, **Good with kids** and **Good with other pets**.",
    "Type a name or breed into **Search** and press **Search**. With JavaScript on, the list updates as you change the filters.",
    "Click **View profile** on an animal to see its photos, story and vaccination status.",
  ]),
  figure("browse.png", "Browsing and filtering animals", [
    "**Species** filter.",
    "**Size** filter.",
    "Tick boxes: **Available only**, **Good with kids**, **Good with other pets**.",
    "Search by name or breed.",
    "**View profile** opens the animal's page.",
    "Status stamp: **Available**, **Pending** (someone has applied), **Adopted** or **Fostered**.",
  ]),
  H2("3.2 An animal's profile"),
  P("The profile shows the animal's photos, age, breed, size, personality, whether it is vaccinated, and whether it is good with children and other pets."),
  figure("profile.png", "An animal profile", [
    "Status stamp. Only **Available** animals can receive applications.",
    "**Apply to adopt** starts an application (shown only when the animal is available).",
    "**Sponsor** pays toward this animal's care.",
  ]),
  H2("3.3 Applying to adopt"),
  P("The form has three short steps and **saves as you type**, so you can close the page and come back later without losing anything."),
  ...steps([
    "On an available animal's profile, click **Apply to adopt**. Log in or register if asked.",
    "**Step 1, About you:** check your name, phone and email (filled in from your account), and say why you would like to adopt. Click **Next: Your home →**.",
    "**Step 2, Your home:** choose your home type, whether you own or rent, and any other pets. Add anything else the shelter should know. Click **Next: Review →**.",
    "**Step 3, Review:** check everything and click **Submit application**. Use **← Back** to change an answer.",
    "You see a confirmation page with your **reference number** (for example `PC-260817-RCKY`) and receive a confirmation email.",
  ]),
  figure("apply.png", "Step 1 of the adoption application", [
    "Your contact details, filled in from your account.",
    "Why you would like to adopt this animal.",
    "**Next: Your home →** moves to step 2.",
  ]),
  note("While your application is open, the animal shows as **Pending** and nobody else can apply for it. Each person can have one open application per animal."),
  H2("3.4 Tracking your application"),
  P("Click **My applications** in the top bar. Each application shows a progress tracker and its current status:"),
  table(["Status", "What it means", "What you do"], [
    ["Submitted", "The shelter has your application.", "Nothing yet. A volunteer will start reviewing it, usually within 2 days."],
    ["Under review", "A volunteer is checking your application.", "Answer the phone or email if the shelter contacts you."],
    ["Approved", "Your application was accepted.", "The shelter arranges a home visit and the adoption fee."],
    ["Adopted", "The adoption is complete.", "Enjoy your new companion!"],
    ["Rejected", "The application was not successful. The reason is shown.", "You can apply for another animal."],
    ["Withdrawn", "You cancelled the application.", "–"],
  ], [1700, 3900, 3812]),
  gap(),
  figure("my-applications.png", "My applications", [
    "Current status.",
    "Progress tracker: Submitted → Under review → Approved → Adopted.",
    "**Withdraw** cancels an application you no longer want.",
  ]),
  H2("3.5 Donating or sponsoring an animal"),
  ...steps([
    "Click **Donate** in the top bar, or **Sponsor** on an animal's profile.",
    "Choose **One-time** or **Monthly**.",
    "Pick a suggested amount or type your own (**R10 to R100,000**).",
    "Optionally choose an animal under **Sponsor a specific animal?** and add a message.",
    "Click **Pledge**. You get a **receipt number** straight away, on screen and by email.",
    "Pay by EFT into the shelter's account shown on the page, using your **receipt number as the payment reference**.",
    "When the shelter sees the money arrive, they mark your donation as received and you get an email receipt.",
  ]),
  figure("donate.png", "Making a donation", [
    "**One-time** or **Monthly**.",
    "Suggested amounts, or type your own.",
    "Optionally sponsor one animal.",
    "**Pledge** creates your receipt number.",
  ]),
  note("Card payments are not available yet. The shelter plans to add PayFast or Yoco later. For now every donation is an EFT, which a staff member confirms."),
  H2("3.6 Your account and your data"),
  P("Click your name at the top right to open **My account**. There you can:"),
  bullets([
    "Change your name and phone number, then click **Save changes**.",
    "See every donation you have made and its receipt number.",
    "Click **Download my data (JSON)** to get a copy of everything PawConnect holds about you. This is your right under POPIA.",
    "Turn on two-step verification (section 2.5).",
  ]),
  figure("my-account.png", "My account", ["Your details.", "Your donation history.", "**Download my data** (POPIA).", "**Manage two-step verification**."]),
);

// 4 Volunteers
add(H1("4. For volunteers"),
  P("Your account is created by the shelter manager, who will give you a temporary password. Log in and change your details under **My account**. You land on the **Volunteer dashboard** every time you log in."),
  H2("4.1 Your dashboard"),
  figure("volunteer-dashboard.png", "The volunteer dashboard", [
    "**Applications to review** for your branch.",
    "**Upcoming shifts** with how many places are filled.",
    "**Sign up** for a shift. The button shows **Full** when no places are left.",
    "**Log hours** after a shift.",
    "Your total approved volunteer hours.",
  ]),
  H2("4.2 Reviewing adoption applications"),
  P("Applications move through fixed steps, and PawConnect only offers the buttons that are allowed at each stage. A rejected application can never become an adoption."),
  ...steps([
    "On your dashboard, click **Review** next to an application.",
    "Read the applicant's answers, then click **Start review**. The status becomes **Under review** and the applicant can see that someone is looking at it.",
    "Contact the applicant if needed. Then either click **Approve** (you can add a note to the applicant) or enter a **Reason** and click **Reject**. The applicant is emailed either way, and a rejected animal becomes available again.",
    "After approval, do the home visit and type your notes under **Home visit notes**, then click **Record home visit**.",
    "When the adoption fee is paid, enter the amount under **Adoption fee received (R)** and click **Record adoption fee**.",
    "When the animal goes home, click **Mark as adopted**. This closes the application.",
  ]),
  figure("application-review.png", "Reviewing an application", [
    "**History**: every status change, who made it and when.",
    "**Next step**: only the actions allowed right now.",
  ]),
  note("**Mark as adopted** only appears after both the home visit and the adoption fee have been recorded.", "Important"),
  H2("4.3 Signing up for shifts"),
  ...steps([
    "Find a shift under **Upcoming shifts** on your dashboard.",
    "Click **Sign up**. You get a confirmation by SMS (if you have a mobile number on your account) or email.",
    "To drop out, click **Cancel** on the shift. Please do this early so someone else can take the place.",
  ]),
  P("PawConnect stops double bookings automatically. You can't sign up for a full shift, sign up twice, or sign up for two shifts that overlap in time, even if two people click at the same moment."),
  H2("4.4 Logging your hours"),
  ...steps([
    "Under **Log hours**, choose the **Date**, enter the number of **Hours** and pick the **Activity** (Dog walking, Cattery cleaning, Front desk, Events & fundraising, Transport or General).",
    "Optionally add **Notes** for the administrator.",
    "Click **Submit for approval**. The hours show as **Pending** under **My logged hours** until an administrator approves them. Only approved hours count toward your total.",
  ]),
  H2("4.5 Logging a medical record"),
  P("Volunteers record every vet visit, vaccination and treatment, so the animal's full health history is in one place and the visiting vet can see it."),
  ...steps([
    "Open the animal's profile (from **Browse animals**).",
    "Scroll to **Medical history**. Earlier records are shown on the left.",
    "In **Log a medical record**, choose the **Type** (Vaccination, Treatment, Checkup, Sterilisation or Other).",
    "Fill in **What was done?**, the **Date**, the **Next due** date if there is one, the **Vet** and any **Details**.",
    "Click **Save record**. It appears in the history immediately. If it is a vaccination, the animal's public profile shows it as vaccinated.",
  ]),
  figure("medical-record.png", "Logging a medical record", [
    "The animal's medical history, newest first.",
    "Record **Type**.",
    "**What was done?** (required).",
    "**Date** (required) and **Next due**.",
    "**Save record**.",
  ]),
  note("Medical history is only visible to volunteers and administrators. The public only sees whether the animal is vaccinated."),
);

// 5 Administrators
add(H1("5. For shelter administrators"),
  P("Administrators can do everything volunteers can, for every branch, and also manage animals, shifts, users, donations and reports. All admin pages are reached through the tabs under the **Shelter dashboard** heading."),
  note("On the live site you must turn on two-step verification (section 2.5) before you can open the Shelter dashboard. PawConnect takes you to the set-up page the first time.", "Important"),
  gap(),
  H2("5.1 The shelter dashboard"),
  figure("admin-dashboard.png", "The shelter dashboard", [
    "Admin tabs: Overview, Applications & shifts, Animals, Shifts, Volunteers, Volunteer hours, Donations, Reports, Users.",
    "Key numbers: animals in care, adoptions and volunteer hours this month, donations received.",
    "Work queues. Click a card such as **Pledges awaiting payment →** to go straight to that list.",
    "**Recent applications**. Click a reference to review it.",
    "**Reports →** opens the funder report.",
  ]),
  H2("5.2 Managing animals"),
  H3("Recording a new arrival"),
  ...steps([
    "Open the **Animals** tab.",
    "Fill in **Record a new arrival**: name, species, breed, age, size, kennel number, whether it is good with kids and other pets, and a short bio.",
    "Click **Add animal**. It appears on the public **Browse animals** page straight away with the status **Available**.",
  ]),
  H3("Changing status, details and photos"),
  bullets([
    "To change status quickly, pick the new status in the animal's row and click **Save**. Statuses are Available, Pending, Adopted and Fostered.",
    "Click **Edit** to change any detail, then **Save details**.",
    "Under **Photos**, choose a JPEG, PNG or WebP image (**2 MB or smaller**, up to **6 per animal**) and click **Upload**. Click **Remove** to delete a photo.",
  ]),
  figure("admin-animals.png", "Managing animals", ["Status **Save** for one animal.", "**Edit** an animal's details and photos.", "**Record a new arrival**.", "**Add animal**."]),
  figure("admin-edit-animal.png", "Editing an animal", ["**Save details**.", "Change **Status**.", "Current **Photos**.", "**Upload** a new photo."]),
  note("Two animals can't share a kennel number. PawConnect tells you if the number is already in use."),
  H2("5.3 Publishing volunteer shifts"),
  ...steps([
    "Open the **Shifts** tab.",
    "Under **Create a shift**, enter the **Date**, **Start** and **End** times (South African time), the **Role** and how many **Places** are available. Add **Notes** if needed.",
    "Click **Create shift**. Volunteers see it on their dashboards immediately.",
  ]),
  figure("admin-shifts.png", "Publishing shifts", ["Upcoming shifts with who has signed up.", "**Create a shift**.", "**Create shift**."]),
  H2("5.4 Volunteers and their hours"),
  P("The **Volunteers** tab lists every volunteer with their activity, contact details, join date, approved hours and pending logs. Sort it by name, hours or join date."),
  figure("admin-volunteers.png", "The volunteer directory", ["Volunteer list.", "**Add volunteer** (opens Users, section 5.5)."]),
  P("The **Volunteer hours** tab is where logged hours are approved:"),
  ...steps([
    "Optionally filter by volunteer or status and click **Filter**.",
    "Check each pending entry and click **Approve** or **Reject**.",
    "The chart **Approved hours by volunteer** updates. Only approved hours count in reports.",
  ]),
  figure("admin-hours.png", "Approving volunteer hours", ["**Filter** the list.", "**Approve** (or **Reject**) an entry.", "Approved hours per volunteer."]),
  H2("5.5 Users and access"),
  H3("Adding a staff member or volunteer"),
  ...steps([
    "Open the **Users** tab.",
    "Under **Add a staff member or volunteer**, fill in their name, email, phone, **Role** and (for volunteers) their usual **Volunteer activity**.",
    "Choose a **Temporary password** and give it to them in person or by phone, not by email.",
    "Click **Create account**. Ask them to log in and change their details.",
  ]),
  H3("Changing roles and deactivating accounts"),
  bullets([
    "Click **Make volunteer** / **Make administrator** or **Remove…** in a user's row to change their roles.",
    "Click **Deactivate** when someone leaves. They are signed out within 5 minutes and can't log in again. Their history (applications, hours, records) is kept. Click **Reactivate** to undo.",
  ]),
  figure("admin-users.png", "Users and access", ["Grant or remove a role.", "**Deactivate** an account.", "**Add a staff member or volunteer**.", "**Create account**."]),
  H2("5.6 Confirming donations"),
  ...steps([
    "Open the **Donations** tab. **Pledges awaiting payment** lists donations people have pledged but not yet paid.",
    "Check the bank statement for an EFT with the matching **Receipt / EFT ref**.",
    "Click **Mark received**. The donor is emailed their receipt automatically and the amount counts in reports.",
    "If a pledge will never be paid, click **Cancel**.",
  ]),
  figure("admin-donations.png", "Confirming donations", ["**Mark received** once the EFT clears.", "Donations received in the last 30 days.", "**Full report & export →**."]),
  H2("5.7 Funder reports and CSV export"),
  ...steps([
    "Open the **Reports** tab.",
    "Choose the **From** and **To** dates and click **Run report**. The report takes well under 10 seconds for any range.",
    "Read the totals: donations received, adoptions completed, applications submitted and approved volunteer hours, plus donations by type and the average time to a decision.",
    "Click **Export donations (CSV)** to download the donations in that range. The file opens in Excel or Google Sheets, ready for funding applications.",
  ]),
  figure("funder-report.png", "The funder report", [
    "**From** and **To** dates.",
    "**Run report**.",
    "**Export donations (CSV)**.",
    "Headline totals for the range.",
    "Donations split by type: general, sponsorship and adoption fees, plus outstanding pledges.",
    "Service levels: average time from application to decision, against the 2-day target.",
  ]),
  H2("5.8 Reviewing applications as an administrator"),
  P("Administrators review applications exactly as volunteers do (section 4.2), but can see and decide applications for **every branch**. Open them from **Recent applications** on the dashboard or the **Applications & shifts** tab."),
);

// 6 Setup
add(H1("6. Setup and technical requirements"),
  P("This section is for the technical volunteer who looks after PawConnect. Day-to-day users can skip it. The full technical documentation is in the project's GitHub repository (`README.md` and the `docs` folder)."),
  H2("6.1 How PawConnect is hosted"),
  table(["Part", "Service", "Notes"], [
    ["Web application", "Azure App Service (Linux, .NET 8), South Africa North", "HTTPS only, TLS 1.2+, health check on `/health`, running version on `/version`."],
    ["Database", "Azure Database for PostgreSQL Flexible Server", "Daily backups kept for 7 days, with point-in-time restore."],
    ["Secrets", "Azure Key Vault", "Connection strings and passwords. Read by the app's managed identity, never stored in code."],
    ["Monitoring", "Application Insights", "Alerts if errors pass 2% or responses slow beyond 1 second."],
    ["Email", "Any SMTP service (e.g. Azure Communication Services)", "Confirmations, approvals and receipts."],
    ["SMS", "Twilio", "Optional. Urgent shift messages, with email as the fallback."],
    ["Code and releases", "GitHub and GitHub Actions", "Tests, security scans and deployments run automatically."],
  ], [1900, 3300, 4212]),
  gap(),
  H2("6.2 Running PawConnect on your own computer"),
  P("**You need:** the .NET 8 SDK, Docker Desktop (for PostgreSQL) and Git."),
  ...steps([
    "Clone the repository: `git clone https://github.com/ST10285120/st10285120.INSY7315.Part2.PAWCONNECT`",
    "Start the database: `docker compose up -d db`",
    "Start the app: `dotnet run --project src/PawConnect.Web`. Database tables are created and demo data is loaded automatically.",
    "Open `http://localhost:5231` and log in with a demo account (listed in `README.md`).",
    "To run the automated tests: `dotnet test`",
  ]),
  note("No Docker? Install PostgreSQL 16, create a `pawconnect` database and user, and update `ConnectionStrings:PawConnect` in `appsettings.Development.json`."),
  gap(),
  H2("6.3 Configuration settings"),
  P("Settings are set in Azure under **App Service → Configuration** (use a double underscore `__` in place of `:`). Secrets should point at Key Vault."),
  table(["Setting", "Purpose"], [
    ["`ConnectionStrings:PawConnect`", "PostgreSQL connection string (Key Vault)."],
    ["`Seed:AdminEmail` / `Seed:AdminPassword`", "Creates the first administrator on an empty database (Key Vault for the password)."],
    ["`Seed:DemoData` / `Seed:DemoPassword`", "Loads demo animals and accounts. **Staging only, never production.**"],
    ["`Notifications:Email:SmtpHost`, `UserName`, `Password`, `FromAddress`", "Email sending. Without a host, emails are only written to the log."],
    ["`Notifications:Sms:Enabled`, `Provider`, `AccountSid`, `AuthToken`, `FromNumber`", "SMS through Twilio (`Provider = Twilio`). Keep staging on `Log`."],
    ["`Security:RequireAdminTwoFactor`", "Forces administrators to use two-step verification. On by default in production."],
  ], [4200, 5212]),
  gap(),
  H2("6.4 The first administrator on the live site"),
  ...steps([
    "Put the manager's email in `Seed:AdminEmail` and a strong temporary password in Key Vault for `Seed:AdminPassword`.",
    "Restart the app. The administrator account is created on start-up.",
    "The manager logs in, sets up two-step verification (section 2.5), and then creates everyone else's accounts from **Users**.",
  ]),
  H2("6.5 Releasing a new version"),
  bullets([
    "Changes are made on a `feature/…` branch and merged into `develop` through a pull request. GitHub Actions builds the app, runs every test, checks code coverage (at least 70%), scans for vulnerable packages and runs the accessibility audit.",
    "Merging into `develop` deploys automatically to the **staging** site and runs a smoke test.",
    "Merging `develop` into `main` waits for **manual approval**, then deploys to the **live** site and checks it is healthy. If the check fails, the deploy log prints what the site is answering, to help find the cause.",
  ]),
  H2("6.6 Backups and recovery"),
  P("Azure backs up the database every day and keeps backups for 7 days. To restore, open the PostgreSQL server in the Azure portal, choose **Restore**, pick a point in time and restore to a new server, then point `ConnectionStrings:PawConnect` at it. Practise this once a quarter on staging."),
);

// 7 Troubleshooting
add(H1("7. Troubleshooting and FAQ"),
  table(["Problem or question", "What to do"], [
    ["I forgot my password.", "PawConnect does not have a self-service reset yet (it is planned). Contact the shelter manager, who will ask the technical volunteer to reset it for you."],
    ["It says my account is locked.", "After 5 wrong passwords the account locks for 15 minutes. Wait, then try again carefully."],
    ["It says my account has been deactivated.", "An administrator switched your account off. Contact the shelter if this is a mistake."],
    ["My authenticator code is not accepted.", "Codes change every 30 seconds, so use the current one. Check that your phone's clock is set automatically. If it still fails, use a recovery code."],
    ["I lost my phone and my recovery codes.", "Contact the technical volunteer. After checking who you are, they can switch two-step verification off for your account so you can set it up again on a new phone."],
    ["The **Apply to adopt** button is missing.", "The animal is Pending, Adopted or Fostered, so it can't take new applications right now. Try another animal, or check back later."],
    ["I closed the application form by accident.", "Open it again from the same animal. Your answers were saved as you typed."],
    ["I didn't get a confirmation email.", "Check your spam folder and that your email address under **My account** is correct. Your application or receipt is still saved and visible in PawConnect."],
    ["I can't sign up for a shift.", "The shift is full, you are already on it, or it overlaps with another shift you signed up for."],
    ["My logged hours don't show in my total.", "Hours count only once an administrator approves them. Pending hours are shown under **My logged hours**."],
    ["My donation still shows as pledged.", "It changes to received once the shelter sees your EFT. Use your receipt number as the payment reference so it can be matched."],
    ["A photo won't upload.", "Use JPEG, PNG or WebP, 2 MB or smaller. Each animal can have up to 6 photos."],
    ["As an admin, I'm sent to the two-step verification page.", "Administrators must use two-step verification on the live site. Set it up (section 2.5) and you'll go straight in."],
    ["The site is slow to load the first time.", "If the site is on Azure's free plan it sleeps when idle. The first visit wakes it, which takes about 20 seconds."],
    ["The page says “Something went wrong”.", "Try again in a minute. If it keeps happening, note the time and what you clicked, and tell the technical volunteer. Every error is logged."],
    ["Does PawConnect work on my phone?", "Yes. Every page works on phones and tablets, in the browser. No app is needed."],
    ["Who can see my personal information?", "Only shelter staff who need it for your application or donation. It is never sold or shared. Download it any time from **My account**."],
  ], [3500, 5912]),
  gap(),
  H2("Getting help"),
  bullets([
    "**Day-to-day questions:** ask the shelter's volunteer super user, who has been trained on every part of PawConnect.",
    "**Technical problems:** contact the technical volunteer. Include the time, the page address and what you clicked.",
    "**Account problems:** contact the shelter manager, who can unlock, reactivate or re-create accounts.",
  ]),
);

// Appendix
add(H1("Appendix A: Quick reference"),
  table(["I want to…", "Go to", "Click"], [
    ["Find an animal", "Browse animals", "Filters, then **View profile**"],
    ["Adopt", "Animal profile", "**Apply to adopt**"],
    ["Check my application", "My applications", "–"],
    ["Donate or sponsor", "Donate", "**Pledge**"],
    ["Download my data", "My account", "**Download my data (JSON)**"],
    ["Review an application", "Volunteer dashboard", "**Review** → **Start review** → **Approve** / **Reject**"],
    ["Sign up for a shift", "Volunteer dashboard", "**Sign up**"],
    ["Log hours", "Volunteer dashboard", "**Submit for approval**"],
    ["Log a vet visit", "Animal profile", "**Save record**"],
    ["Add a new animal", "Shelter dashboard → Animals", "**Add animal**"],
    ["Publish a shift", "Shelter dashboard → Shifts", "**Create shift**"],
    ["Approve hours", "Shelter dashboard → Volunteer hours", "**Approve**"],
    ["Add a volunteer", "Shelter dashboard → Users", "**Create account**"],
    ["Confirm an EFT", "Shelter dashboard → Donations", "**Mark received**"],
    ["Report to funders", "Shelter dashboard → Reports", "**Run report**, **Export donations (CSV)**"],
  ], [3100, 3100, 3212]),
);

// ---------- document ----------
const today = "2 November 2026";
const cover = [
  new Paragraph({ spacing: { before: 2200 }, children: [new TextRun({ text: "HOPE & PAWS ANIMAL SHELTER", color: MARIGOLD, bold: true, size: 22, characterSpacing: 60 })] }),
  new Paragraph({ spacing: { before: 200 }, children: [new TextRun({ text: "PawConnect", font: "Georgia", bold: true, size: 84, color: FOREST })] }),
  new Paragraph({ border: { bottom: { style: BorderStyle.SINGLE, size: 18, color: MARIGOLD, space: 8 } }, spacing: { after: 300 },
    children: [new TextRun({ text: "User Guide", font: "Georgia", size: 52, color: INK })] }),
  new Paragraph({ children: [new TextRun({ text: "How to browse, adopt, donate, volunteer and run the shelter with PawConnect", size: 26, color: SOFT })] }),
  new Paragraph({ spacing: { before: 2600 }, children: [new TextRun({ text: "INSY7315 Work Integrated Learning · Task 3", size: 22 })] }),
  new Paragraph({ children: [new TextRun({ text: "Carson Norman · ST10285120", size: 22 })] }),
  new Paragraph({ children: [new TextRun({ text: `Version 1.0 · ${today}`, size: 22, color: SOFT })] }),
  new Paragraph({ children: [new PageBreak()] }),
  new Paragraph({ children: [new TextRun({ text: "Contents", font: "Georgia", bold: true, size: 36, color: FOREST })], spacing: { after: 200 } }),
  new TableOfContents("Contents", { hyperlink: true, headingStyleRange: "1-2" }),
];

const doc = new Document({
  features: { updateFields: true },
  creator: "Carson Norman", title: "PawConnect User Guide", description: "User Guide for PawConnect, Hope & Paws Animal Shelter",
  styles: {
    default: { document: { run: { font: "Arial", size: 21, color: INK }, paragraph: { spacing: { line: 276, lineRule: LineRuleType.AUTO } } } },
    paragraphStyles: [
      { id: "Heading1", name: "Heading 1", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { font: "Georgia", size: 36, bold: true, color: FOREST }, paragraph: { spacing: { before: 0, after: 200 }, outlineLevel: 0 } },
      { id: "Heading2", name: "Heading 2", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { font: "Georgia", size: 28, bold: true, color: FOREST }, paragraph: { spacing: { before: 320, after: 120 }, outlineLevel: 1, keepNext: true } },
      { id: "Heading3", name: "Heading 3", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { font: "Arial", size: 23, bold: true, color: CLAY }, paragraph: { spacing: { before: 200, after: 80 }, outlineLevel: 2, keepNext: true } },
      { id: "Caption", name: "Caption", basedOn: "Normal", run: { size: 18, italics: true, color: SOFT }, paragraph: { spacing: { after: 100 } } },
    ],
  },
  numbering: { config: [
    { reference: "bullets", levels: [{ level: 0, format: LevelFormat.BULLET, text: "•", alignment: AlignmentType.LEFT,
      style: { paragraph: { indent: { left: 500, hanging: 300 } }, run: { color: MARIGOLD } } }] },
    ...numberingConfigs,
  ] },
  sections: [
    { properties: { page: { size: { width: PAGE_W, height: 16838 }, margin: { top: MARGIN, bottom: MARGIN, left: MARGIN, right: MARGIN } } },
      children: cover },
    { properties: { page: { size: { width: PAGE_W, height: 16838 }, margin: { top: MARGIN, bottom: MARGIN, left: MARGIN, right: MARGIN }, pageNumbers: { start: 1 } } },
      headers: { default: new Header({ children: [new Paragraph({ border: { bottom: { style: BorderStyle.SINGLE, size: 4, color: "D9D2BE", space: 4 } },
        children: [new TextRun({ text: "PawConnect User Guide", size: 17, color: SOFT }),
          new TextRun({ children: [new PositionalTab({ alignment: PositionalTabAlignment.RIGHT, relativeTo: PositionalTabRelativeTo.MARGIN, leader: PositionalTabLeader.NONE })] }),
          new TextRun({ text: "Hope & Paws Animal Shelter", size: 17, color: SOFT })] })] }) },
      footers: { default: new Footer({ children: [new Paragraph({ alignment: AlignmentType.CENTER,
        children: [new TextRun({ children: ["Page ", PageNumber.CURRENT], size: 17, color: SOFT })] })] }) },
      children: body },
  ],
});

Packer.toBuffer(doc).then((buf) => {
  fs.writeFileSync(OUT, buf);
  console.log(`Wrote ${OUT}: ${figNo} figures, ${figNo - missing.length} with screenshots, placeholders: ${missing.join(", ") || "none"}`);
});
