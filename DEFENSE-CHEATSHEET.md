# STAR:DOM — Capstone Defense Q&A Cheat Sheet

> Panimula: English ang mga nakasulat na sagot dito (yan ang karaniwang ginagamit sa panel).
> Basahin nang malakas nang 2–3 beses bago ang defense. May "one-liners" sa dulo na
> pwedeng i-memorize. Good luck! 🍀

---

## 1. 30-Second Elevator Pitch

> "STAR:DOM is an online marketplace and pop-up hub for a Filipino solo artisan brand.
> Customers order existing products online — shipped nationwide via J&T Express or
> claimed in person at an active pop-up stall — and can request custom commissions
> through a 5-step wizard. The store owner manages products, event schedules, order
> fulfillment, and payment confirmation from one admin console. The system enforces
> the real-world workflow: online stock is separate from physical booth stock,
> pick-up orders are confirmed by both the customer and the stall team, and every
> payment confirmation is protected by password re-entry."

---

## 2. Tech Stack (memorize this table)

| Layer | Technology | Why |
|---|---|---|
| Language | VB.NET (.NET Framework 4.8) | Capstone spec; team's strongest language |
| UI | ASP.NET Web Forms (server-rendered HTML from VB code-behind) | Simple single-deploy model, no separate frontend build |
| Data access | ADO.NET + Npgsql 4.1.10, parameterized queries | Direct control over SQL, injection-safe |
| Database | PostgreSQL 16 (Supabase in production; portable local instance in dev) | Free, robust, matches Supabase hosting |
| Dev server | IIS Express (localhost:8095) | Built into Windows, no admin rights needed |
| Production | Docker (Ubuntu + Mono/XSP4) deployed on Render | Free tier, containerized, reproducible |
| Sharing for demo | Cloudflare quick tunnel (cloudflared is bundled) | Public URL without deploying |

**Architecture (3-layer):**
```
Browser  →  Web Forms pages (App\, Site.master)
             →  Services layer (Shared\Services)   ← business rules live here
                 →  Repositories (Shared\Repositories)  ← all SQL lives here
                     →  Db module (Code\Db.vb)  ← the ONLY file that knows Npgsql
                         →  PostgreSQL
Models (Shared\Models) are shared DTOs used by all layers.
```

---

## 3. Roles — Exactly Two

| Role | Who | Can do |
|---|---|---|
| **CUSTOMER** | Any registered user (registration always creates CUSTOMER) | Browse, order (delivery or pick-up), pay, review, request commissions |
| **ADMIN** | The single store owner — admin IS the merchant | Everything customers can't: products & stock, events, order fulfillment, payment confirmation, commission pipeline, reports, user management |

> One-liner: "STAR:DOM is a single-owner brand, so the admin account is the
> merchant. There is no separate seller registration — new accounts are always
> customers."

---

## 4. Numbers You Should Know Cold

- **28** database tables · **100** seeded products · **3** active bundles
- Shipping fee: **₱80**, free at **₱1,500+**; pick-up always **₱0**
- Stickers: **₱30** each; bundle **4 for ₱100** (saves ₱20 per complete group)
- Button pins: **₱35** each; bundle **3 for ₱100** (saves ₱5 per complete group)
- **3** payment methods: GCash, Maya, Cash on Delivery/Claim (no card)
- Courier: **J&T Express only**
- Commission wizard: **5 steps**; commissioned products are **delivery-only**
- Order numbers: `SD-yyyyMMdd-nnnn` (derived from the DB identity — race-safe)
- Password hashing: **PBKDF2**, 10,000 iterations, per-user salt

---

## 5. Core Workflows (be ready to narrate these)

### A. Order placement
1. Customer adds to cart (a toast confirms the add).
2. Checkout: choose **Delivery** or **Pick-up**.
   - Delivery → shipping address required, ₱80 fee (free ≥ ₱1,500).
   - Pick-up → choose an **active or upcoming stall** from a dropdown (hours shown), no address, no fee. The server re-validates that the stall is really open/upcoming.
3. Choose payment: GCash / Maya / COD.
4. Order is written in **one database transaction** (order + items + stock decrement + payment row). Stock checks are conditional (`WHERE StockQuantity >= quantity`) so two buyers can never oversell the last unit.

### B. Payment confirmation (owner or customer)
- **Always requires password re-entry** — verified against a fresh database read, not the session.
- GCash/Maya additionally require the **e-wallet reference number**.
- COD requires only the password (the reference is auto-generated).
- On confirmation: payment becomes PAID, order PENDING → CONFIRMED, official receipt issued.

### C. Fulfillment status (J&T only — the website states, never pretends to be the courier)
1. "Order is currently scheduled for booking."
2. "Order booked with J&T Express — tracking number X. Track it on the J&T website." (link to J&T's own tracker)
3. "Order delivered successfully."

### D. Pick-up claim (two-sided confirmation)
1. Customer picks the stall at checkout; order stays PENDING/CONFIRMED.
2. Stall team taps **"Confirm hand-over"**; customer taps **"Confirm order received"**.
3. When BOTH are confirmed → order becomes DELIVERED + PAID + receipt. One side alone can never close the order.

### E. Bundles (automatic pricing)
- Rule comes from the Bundles + BundleItems tables; the deal size/price is parsed from the bundle name ("(4 for 100)").
- In cart/checkout, every **complete** group is repriced; leftovers stay regular.
- Example: 5 stickers = 1 bundle (₱100) + 1 extra (₱30) = ₱130.
- The discount shows as a "Bundle savings" line so official receipt line prices stay truthful.

### F. Commissions
- 5-step wizard (category → what to create → references → specs → submit).
- Owner works it in the Commission Pipeline (accept/decline/clarify/offer/produce).
- **Commissioned products can only be claimed via delivery.**

---

## 6. Anticipated Panel Questions & Suggested Answers

### General

**Q: Why PostgreSQL and not MySQL?**
> A: We initially built on MySQL and migrated fully to PostgreSQL because our
> production target (Supabase) is PostgreSQL, it offers stronger data types
> (true booleans, timestamptz), and its transactional guarantees are what our
> checkout flow depends on. The migration also taught us dialect discipline —
> all SQL now runs through one repository layer.

**Q: What design pattern does the system use?**
> A: A layered / Repository pattern: pages never write SQL; they call Services,
> which enforce business rules and call Repositories, which own all SQL.
> Cross-cutting concerns (security, errors) are centralized — e.g., CSRF is
> checked in exactly one place in the application pipeline.

### Technical

**Q: How do you prevent two customers from buying the last item at the same time?**
> A: The whole checkout is a single database transaction, and the stock
> decrement is conditional — `UPDATE ... WHERE StockQuantity >= quantity`.
> If a concurrent order already took the last unit, the UPDATE matches zero
> rows, the code throws, and the transaction rolls back completely. No
> overselling is possible.

**Q: How do you generate unique order numbers under concurrency?**
> A: We never count rows to build the number. The order row is inserted first
> with a temporary number, and the public number SD-yyyyMMdd-nnnn is derived
> from the database's auto-increment identity — which can never collide.

**Q: How is SQL injection prevented?**
> A: Every query is parameterized through one helper (`Db.P`) — no string
> concatenation of user input reaches the database. All SQL lives in the
> repository layer, which made it auditable in one pass.

**Q: How are passwords stored?**
> A: PBKDF2 with 10,000 iterations and a per-user salt. We never store or log
> plaintext passwords, and payment confirmation re-verifies the password
> against a fresh database read.

**Q: What about CSRF?**
> A: Every POST form carries a per-session token that is verified in exactly
> one place — the application's request-state event — so no page can forget
> the check. A mismatch returns 403 and is logged.

**Q: Where do you store database credentials?**
> A: Not in the repo. The app prefers environment variables
> (SUPABASE_DB_URL or individual DB_* variables) and only falls back to a
> local placeholder connection string for development.

**Q: What happens if the database is down?**
> A: Page-level error handling shows a friendly message instead of a stack
> trace, and every exception is logged to an AppErrors table in the database
> when it is reachable — so failures are diagnosable without exposing details
> to users.

**Q: How do notifications work?**
> A: In-app notifications (no SMS — that was removed deliberately). They are
> role-targeted: customers get order/pickup/commission updates; the owner gets
> new orders and claim confirmations. This is also how buyers learn a new
> pop-up stall opened in their area.

**Q: Why is the compiled DLL committed to the repository?**
> A: The production deploy runs on Mono/XSP4 inside Docker; committing the
> build output guarantees the deployed image boots even though the container
> compiles from source as a gate. It also lets any machine run the site
> without installing a compiler.

### Workflow / Product-decision questions

**Q: Why is payment confirmation password-gated?**
> A: Because confirming a payment is a money-critical action. Requiring the
> operator's own password means a stolen unlocked session can't mark payments
> as paid, and requiring the e-wallet reference number ties the confirmation
> to a real GCash/Maya transaction.

**Q: Why J&T Express only?**
> A: The real business ships exclusively with J&T. The site therefore presents
> one courier truthfully: it records the tracking number, links to J&T's own
> tracker, and shows three status states. We deliberately do not fake a
> full courier dashboard inside the site.

**Q: Why does a pick-up order need two confirmations?**
> A: Mirroring reality: hand-over is a two-party event. The stall confirms it
> released the goods; the customer confirms receipt. Only when both agree does
> the order close — this prevents disputes where one side claims the other
> never confirmed.

**Q: Why are commissions delivery-only?**
> A: A commission takes days of production; requiring the buyer to meet a
> booth window is unrealistic. Delivery-only keeps fulfillment uniform and
> matches how the real business operates made-to-order work.

**Q: Why show "Bundle savings" as a separate line instead of cheaper unit prices?**
> A: Receipt integrity. Line prices on the official receipt must match what
> the buyer actually picked; the bundle is a discount applied to the order,
> which is how bazaars actually ring it up.

### Hardest-problem stories (pick ONE to tell well)

**A. The MySQL → PostgreSQL migration.**
> "Our SQL was dialect-coupled. PostgreSQL refused MySQL-only syntax:
> multi-table DELETE ... JOIN, the FIELD() ordering function, integer flags in
> boolean columns. We centralized all SQL in repositories and rewrote each
> construct — for example, FIELD() became a CASE WHEN ordering, and every
> boolean literal became TRUE/FALSE. The bugs it surfaced were real: one
> integer-vs-boolean mismatch silently killed every notification insert."

**B. The overselling guard.**
> "The naive checkout (check stock, then decrement) loses to concurrency.
> We moved the whole checkout into one transaction and made the decrement
> conditional, so the database itself arbitrates who gets the last unit. The
> loser gets a clean 'insufficient stock' message and a full rollback."

---

## 7. Demo Flow (5–7 minutes)

1. **Landing page (anonymous)** — open `http://localhost:8095`. Show the public storefront: hero, live stats, featured pieces — then browse the catalog and the pop-up tour schedule with no account. Click **Add to cart** to show the premium sign-in gate ("Sign in required", the product, and the CTAs) — then **Sign In** as `bella / customer123` and point out it returns and completes the add. Say the gate is only a courtesy layer: every account page still redirects server-side without it.
2. **Catalog** — point at the bundle banner; open a sticker product ("any 4 for ₱100").
3. **Add 4 stickers** → cart shows Subtotal ₱120, Bundle savings −₱20, Total ₱100.
4. **Checkout** → choose **Pick-up**, select an upcoming stall (hours visible), GCash → place order.
5. **Logout → login `admin / admin123`** (owner view: no marketplace nav, no cart — show this!).
6. **Merchant → Orders & Payments** — find the order: record payment (ref + password — mistype once to show the guard, then correct).
7. **Confirm hand-over** → logout → back as bella → **"Confirm order received"** → order closes DELIVERED, receipt appears.
8. Show the **J&T tracking line** on the earlier delivery order + "Track on J&T Express" link.
9. **Commission Hub** — 5-step wizard, delivery-only note.
10. (Optional) **Admin Console** — two roles, role dropdown has only CUSTOMER/ADMIN.

**Panic fallbacks:**
- If the DB is down: run `start-db.bat`, refresh.
- If a page errors: the error page is friendly by design; say "our error policy is user-friendly message + full detail logged to AppErrors."
- If the demo machine has no internet for the tunnel: demo on localhost — everything works offline.
- Worst case: screenshots + this sheet's workflow section narrate the flow.

---

## 8. Limitations & Future Work (volunteer these before they ask)

- No real payment gateway — GCash/Maya are recorded with reference numbers, not API-verified.
- Tracking is a link to J&T's tracker; no server-side webhook sync.
- Physical booth stock is deducted manually by the owner (deliberate scope cut).
- Single-owner model: one admin account; multi-tenant sellers would need role/ownership expansion.
- Future: real e-wallet API integration, courier webhook sync, email notifications, PWA offline view.

---

## 9. One-liners to Memorize

- "All SQL lives in repositories; all rules live in services; pages only render."
- "One transaction per checkout — the database arbitrates the last unit."
- "Password re-entry gates every payment confirmation."
- "Pick-up closes only when both sides confirm."
- "Two roles: the owner is the admin; everyone else is a customer."
- "The website states the courier status; it doesn't pretend to be the courier."
- "Online inventory is the website's truth; booth stock is managed by the owner."
- "Security is centralized so no page can forget it."

---

## 10. Pre-Defense Checklist

- [ ] Practice the demo flow twice on THIS machine (DB up: `start-db.bat`).
- [ ] Order data state is demo-friendly (orders exist for the owner to confirm).
- [ ] Know your demo logins: bella/customer123 · admin/admin123 · mika/admin123 (legacy owner).
- [ ] Backup plan: tunnel URL OR localhost (both work; tunnel needs internet).
- [ ] Print/export this sheet; skim sections 4, 5, and 9 right before the panel.
- [ ] Open the dashboard/report screenshots in tabs as plan B.
