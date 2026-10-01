# STAR:DOM — Presentation Cheat Sheet

> Pang-**presentation** ito, hindi defense Q&A. Ito ang dala mo habang nagde-demo:
> ang pitch, ang demo flow step by step, ang numbers na sasabihin mo, at ang mga
> one-liner na pwedeng isigaw sa transition. English ang mga script para madiretso
> mong sabihin, Taglish ang mga palatandaan.
> Basahin nang 2–3 beses bago mag-present. Good luck! 🍀

---

## 1. Opening Pitch (30 seconds — say this while the landing page loads)

> "STAR:DOM is an online marketplace and pop-up hub for a Filipino solo artisan brand.
> Customers order ready-made products online — shipped nationwide via J&T Express or
> claimed in person at an active pop-up stall — and can request custom commissions
> through a 5-step wizard. The store owner runs everything from one console:
> products, events, order fulfillment, payment confirmation, and the GCash and Maya
> QR payment details themselves, which are editable from an admin Payment Settings
> page. The system enforces the real-world workflow: online stock is separate from
> booth stock, pick-up orders close only when both the customer and the stall team
> confirm, and every payment confirmation is protected by password re-entry."

**Transition tip:** habang nagsasalita ka nito, naka-bukas na yung
`http://localhost:8095` sa tab. Pag tapos ka, scroll agad sa featured products.

---

## 2. Tech Stack (one-liner per layer, kung magtanong habang nag-demo)

| Layer | Technology | 5-second explanation |
|---|---|---|
| Language | VB.NET (.NET Framework 4.8) | Capstone spec; server-rendered |
| UI | ASP.NET Web Forms | No separate frontend build, one deploy |
| Data | PostgreSQL (Supabase) via Npgsql | Parameterized queries lang, injection-safe |
| Dev server | IIS Express, port 8095 | `run-website.bat` does everything |
| Production | Docker (Mono/XSP4) on Render | Free tier, containerized |
| Sharing | Cloudflare quick tunnel | Public URL for the demo |

**Architecture (i-draw mo 'to sa whiteboard kung hahanapin):**

```
Browser → Pages (App\) → Services (rules) → Repositories (all SQL) → PostgreSQL
```

---

## 3. Roles — Exactly Two

| Role | Who | Can do |
|---|---|---|
| **CUSTOMER** | Any registered user (registration always creates CUSTOMER) | Browse, order (delivery or pick-up), pay, review, request commissions |
| **ADMIN** | The single store owner — admin IS the merchant | Everything else: products & stock, events, fulfillment, payment confirmation, commission pipeline, reports, user management, **Payment Settings** |

> One-liner: "Single-owner brand, kaya ang admin ay siyang merchant. Walang
> separate seller registration — lahat ng bagong account ay customer."

---

## 4. Numbers to Say Confidently

- **30** database tables · **100** seeded products · **3** active bundles
- **30** web pages (29 `.aspx` + master) · ~**11,900** lines of VB.NET
- Shipping fee: **₱80**, free at **₱1,500+**; pick-up always **₱0**
- Stickers: **₱30** each; bundle **4 for ₱100** · Button pins: **₱35** each; bundle **3 for ₱100**
- **3** payment methods: GCash, Maya, COD/Claim (no card — deliberately)
- Courier: **J&T Express only** · Commission wizard: **5 steps**
- Order numbers: `SD-yyyyMMdd-nnnn` · Passwords: **PBKDF2**, 10,000 iterations
- **2** e-wallet channels (GCash, Maya) — both admin-configurable

---

## 5. Core Demo Workflows

### A. Order placement (customer side)
1. Add to cart (may toast confirmation).
2. Checkout → **Delivery** (₱80, free ≥ ₱1,500) o **Pick-up** (choose stall, ₱0).
3. Payment: **GCash / Maya / COD**.
4. Order written in **one DB transaction** — conditional stock decrement, so
   hindi posibleng ma-oversell ang last unit.

### B. Paying with GCash / Maya (⭐ the new feature — show this proudly)
1. After checkout, the **Scan to Pay popup** opens on the order page.
2. Everything in that popup is **admin-managed** — walang hardcoded:
   - The **QR image** (uploaded sa admin, o stylized placeholder kung wala pa)
   - The **GCash/Maya number**
   - The **account name**
   - The **display mode** — QR only, number + name only, name only, or everything
3. Customer pays, saves the **reference number**, enters it to verify.

### C. Payment Settings admin page (⭐ demo this right after B)
1. Logout → login `admin / admin123`.
2. Sidebar → **SYSTEM → Payment Settings**.
3. Two cards: **GCash (blue)** at **Maya (green)** — each with:
   - Account name, account number fields
   - QR image upload (PNG/JPG/WebP, max 5 MB) + remove option
   - **"What customers see"** dropdown — THE feature: *Everything / QR only /
     Number + name only / Name only*
   - **"Show at checkout"** toggle — off = mawawala sa checkout options
   - Optional caption under the QR
4. **Live preview** sa ibaba ng page — kung ano makikita ng customer, yun din.
5. Save → "payment settings saved" flash → balik ka agad sa OrderDetail popup:
   updated na.

> Script: "So if the artisan changes her GCash number or swaps to a new QR code,
> she does it here — no developer, no redeploy. Changes take effect immediately
> on every customer's payment screen."

### D. Payment confirmation (owner side)
- **Password re-entry** always (verified against fresh DB read, not session).
- GCash/Maya: e-wallet **reference number** required.
- On confirm: PAID → order CONFIRMED → official receipt issued.

### E. Fulfillment (J&T)
1. "Scheduled for booking" → 2. "Booked with J&T — tracking number + link" →
3. "Delivered". The site states status; it doesn't pretend to be the courier.

### F. Pick-up claim (two-sided)
Stall taps **"Confirm hand-over"** + customer taps **"Confirm order received"**
→ only then DELIVERED + receipt. Isa lang, hindi pa-close.

### G. Bundles (automatic pricing)
5 stickers = 1 bundle (₱100) + 1 extra (₱30) = **₱130**, shown as a separate
"Bundle savings" line. Receipt integrity — line prices stay truthful.

### H. Commissions
5-step wizard → owner pipeline (accept/decline/clarify/offer/produce) →
**delivery-only claim**.

---

## 6. Likely Questions While Presenting (short answers)

**"Saan galing yung QR code na pinapakita niyo?"**
> "From the database — the admin uploads the real QR image in Payment Settings.
> Kung walang upload pa, may stylized placeholder kami para hindi blank. The
> display mode controls whether customers also see the number and account name."

**"What if nagpalit ng number yung store owner?"**
> "She edits it in Payment Settings. There's no hardcoded number anywhere in
> the code — the popup and checkout read from the PaymentSettings table. Defaults
> lang ang nasa seed, at self-healing ang table creation kaya walang migration."

**"Can you turn off one payment method?"**
> "Yes — the per-channel toggle. Pag in-off ko ang Maya, mawawala siya sa
> checkout options immediately; COD stays because it's not an e-wallet."

**"Bakit walang real payment gateway?"**
> "Deliberate scope: we record the method, reference number, and admin
> confirmation instead of wiring an API. The architecture leaves room for one —
> OrderService isolates the confirmation step and records a GatewayResponse."

**"How do you prevent overselling?"**
> "One transaction per checkout; the stock UPDATE is conditional
> (`WHERE StockQuantity >= qty`), so the database itself arbitrates the last unit."

**"How are passwords stored?"**
> "PBKDF2-HMAC-SHA256, 10,000 iterations, per-user salt, constant-time compare.
> Payment confirmation re-verifies the password against a fresh DB read."

**"SQL injection?"**
> "Impossible by construction — every query goes through one parameterized
> helper (`Db.P`); all SQL lives in the repository layer."

**"CSRF?"**
> "Per-session token, verified in exactly one place in the request pipeline —
> a new page cannot forget the check. Mismatch = 403 + logged."

---

## 7. Demo Flow (5–7 minutes, sunod-sunod)

1. **Landing (anon)** — storefront, featured pieces. Click **Add to cart** →
   sign-in gate modal → sign in `bella / customer123` → item lands in cart.
2. **Cart** — add 4 stickers: Subtotal ₱120, Bundle savings −₱20, Total ₱100.
3. **Checkout** — Pick-up + choose stall + **GCash** → place order.
4. **⭐ Scan-to-Pay popup** — QR placeholder/number/name. Ipoint out: "all of
   this comes from admin settings."
5. **⭐ Payment Settings** — logout, login `admin/admin123`, SYSTEM → Payment
   Settings. Show the two channel cards, the **display mode dropdown** (switch
   it to "Number + name only" and save), then open the order popup again —
   the QR image is gone, only number + name show. Switch it back to "Everything".
6. **Confirm payment** — Merchant → Orders & Payments → record payment
   (reference number + password; mistype the password once to show the guard).
7. **Pick-up close-out** — admin "Confirm hand-over" → logout → bella →
   "Confirm order received" → DELIVERED + receipt.
8. **J&T line** — show the tracker link on a delivery order.
9. **Commission Hub** — 5-step wizard, delivery-only note.
10. **(Optional)** Admin Console — two roles only; role dropdown = CUSTOMER/ADMIN.

**Panic fallbacks:**
- DB down → run `start-db.bat`, refresh.
- Page error → "our error policy is friendly message + full detail logged to AppErrors."
- Walang internet para sa tunnel → localhost works offline, lahat gumagana.
- Worst case → screenshots + Section 5 narrative.

---

## 8. Limitations (volunteer these — sounds honest, avoids gotchas)

- No live payment gateway — reference numbers + admin confirmation, not API-verified.
- Tracking links to J&T's own site; no webhook sync.
- Booth stock adjusted manually (deliberate scope cut).
- Single-owner model; multi-tenant sellers would need role expansion.
- Future: real e-wallet API, courier webhook, email notifications, PWA offline view.

---

## 9. One-liners (memorize, isigaw sa transitions)

- "All SQL lives in repositories; all rules live in services; pages only render."
- "One transaction per checkout — the database arbitrates the last unit."
- "Password re-entry gates every payment confirmation."
- "Pick-up closes only when both sides confirm."
- "Two roles: the owner is the admin; everyone else is a customer."
- **"The QR details are data, not code — the owner edits them from the admin
  panel, and every customer screen updates instantly."** ⭐
- "The website states the courier status; it doesn't pretend to be the courier."

---

## 10. Pre-Presentation Checklist

- [ ] Practice the demo flow twice on THIS machine (DB up: `start-db.bat`).
- [ ] **Upload real GCash/Maya QR screenshots sa Payment Settings** para maganda
      ang popup sa demo (PNG/JPG, square, max 5 MB).
- [ ] Demo logins ready: `bella/customer123` · `admin/admin123` · `mika/admin123`.
- [ ] May pending PENDING order na pwedeng i-confirm ng owner.
- [ ] Tunnel URL OR localhost (pareho gumagana; tunnel needs internet).
- [ ] Basahin ang Sections 1, 5, and 9 bago pumasok.
- [ ] Screenshots ng dashboard/reports naka-open sa tabs (Plan B).
