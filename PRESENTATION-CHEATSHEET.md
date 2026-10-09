# STAR:DOM — Presentation Cheat Sheet

> Pang-**presentation** ito, hindi defense Q&A. Ito ang dala mo habang nagde-demo:
> ang pitch, ang demo flow step by step, ang numbers na sasabihin mo, at ang mga
> one-liner na pwedeng isigaw sa transition. English ang mga script para madiretso
> mong sabihin, Taglish ang mga palatandaan.
> Basahin nang 2–3 beses bago mag-present. Good luck! 🍀

---

## 1. Opening Pitch (30 seconds — say this while the landing page loads)

> "STAR:DOM is an online marketplace and pop-up hub for a Filipino solo artisan
> brand. Customers order ready-made products online — shipped nationwide via
> J&T Express against a final total the studio quotes first — and can request
> custom commissions through a 5-step wizard. The store owner runs everything
> from one console: products, events, order fulfillment, and the two online
> payment channels themselves, whose QR details are editable from an admin
> Payment Settings page. The system enforces the real-world workflow: an order
> starts unpaid, the studio quotes the J&T fee to make the total final, the
> customer pays by GCash or GOtyme reference number, and the studio verifies
> that reference behind a password re-entry before anything ships or starts
> processing."

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
| **CUSTOMER** | Any registered user (registration always creates CUSTOMER) | Browse, order (delivery only), pay by GCash/GOtyme, review, request commissions |
| **ADMIN** | The single store owner — admin IS the merchant | Everything else: products & stock, events, order confirmation + J&T quote, payment verification, commission pipeline, reports, user management, **Payment Settings** |

> One-liner: "Single-owner brand, kaya ang admin ay siyang merchant. Walang
> separate seller registration — lahat ng bagong account ay customer."

---

## 4. Numbers to Say Confidently

- **30** database tables · **100** seeded products · **3** active bundles
- **33** renderable pages (32 `.aspx` + master) · ~**12,100** lines of VB.NET
- Shipping: **quoted by J&T** — walang flat fee. Indicative zones:
  Luzon ₱0–100 · Visayas ₱101–200 · Mindanao ₱201–300; the exact figure follows
  the parcel's weight and is confirmed by the studio before the total is final.
- Stickers: **₱30** each; bundle **4 for ₱100** · Button pins: **₱35** each; bundle **3 for ₱100**
- **2** payment methods: GCash, GOtyme (no card, no COD — deliberately)
- Courier: **J&T Express only** · Commission wizard: **5 steps**
- Order numbers: `SD-yyyyMMdd-nnnn` · Passwords: **PBKDF2**, 10,000 iterations
- **2** e-wallet channels (GCash, GOtyme) — both admin-configurable

---

## 5. Core Demo Workflows

### A. Order placement (customer side)
1. Add to cart (may toast confirmation).
2. Checkout asks for the **delivery address + phone only** — wala pang payment
   method dito, walang COD, walang pick-up. Shipping is **"quoted after
   confirmation"**; the J&T fee cannot be known while the parcel is unweighed.
3. **Place Order** writes the order in **one DB transaction** — conditional stock
   decrement, so hindi posibleng ma-oversell ang last unit. Status: PENDING,
   total = goods minus bundle savings (shipping not yet included).
4. Flash: "The studio will return it with the final price and shipping fee."

### B. Confirm & Quote (owner side) — step 2 of the flow
1. Merchant → **Orders & Payments** → the PENDING order.
2. **Confirm & quote**: enter the J&T shipping fee + password → status becomes
   **CONFIRMED** and the **total is final** in the same statement.
3. Customer is notified: "Shipping is ₱X via J&T Express — final total ₱Y."
4. Until this runs, `SubmitPayment` refuses: there is no final figure to pay.

### C. Paying with GCash / GOtyme (⭐ the new feature — show this proudly)
1. On **Order Detail**, the customer picks a channel (only after the fee is
   quoted) and the **Scan to Pay popup** opens.
2. Everything in that popup is **admin-managed** — walang hardcoded:
   - The **QR image** (uploaded sa admin, o stylized placeholder kung wala pa)
   - The **GCash/GOtyme number**
   - The **account name**
   - The **display mode** — QR only, number + name only, name only, or everything
3. Customer pays, saves the **reference number**, enters it **plus their
   password**. This only parks the payment as **SUBMITTED** — hindi pa bayad.
4. Order Detail shows a yellow **"Payment submitted — waiting for
   verification"** card. No receipt, no PROCESSING, no J&T booking yet.

### D. Payment verification (owner side) — the money actually lands here
- Merchant sees the **submitted reference** *and* a **password box** beside it:
  one form, two buttons — **Confirm** or **Decline**.
- **Password re-entry** always (verified against a fresh DB read, not session).
- Confirm → payment **PAID** → order stays/becomes **CONFIRMED** → official
  receipt issued. Only this path reaches PAID; a declined reference never does.
- Decline → payment **FAILED**; the customer is handed the support line
  ("message @star.d0mm on instagram or contact 09701375033") and may resubmit.

### E. Fulfillment (J&T)
1. **PROCESSING** — refused until the payment is PAID.
2. **Book J&T** — the waybill number is typed in, required to reach SHIPPED.
3. **DELIVERED** — checks the *stored* waybill, not the form field. Parcel-level
   statuses are all gated on a PAID payment: nothing ships unpaid.
4. Customer **"Confirm order received"** → RECEIVED → reviews unlock.

### F. Bundles (automatic pricing)
5 stickers = 1 bundle (₱100) + 1 extra (₱30) = **₱130**, shown as a separate
"Bundle savings" line. Receipt integrity — line prices stay truthful.

### G. Commissions
5-step wizard → merchant **accepts + quotes an offer** (no deposit, no clarify
round-trip) → customer confirms → pays GCash/GOtyme (ref only) → merchant
**verifies behind a password** → production (in production / revision /
finalized) → **delivery-only** → received → completed.

### H. Payment Settings admin page (⭐ demo right after C or D)
1. Logout → login `admin / admin123`.
2. Sidebar → **SYSTEM → Payment Settings**.
3. Two cards: **GCash (blue)** at **GOtyme (green)** — each with:
   - Account name, account number fields
   - QR image upload (PNG/JPG/WebP, max 5 MB) + remove option
   - **"What customers see"** dropdown — THE feature: *Everything / QR only /
     Number + name only / Name only*
   - **"Show at checkout"** toggle — off = mawawala sa payment options
   - Optional caption under the QR
4. **Live preview** sa ibaba ng page — kung ano makikita ng customer, yun din.
5. Save → "payment settings saved" flash → balik ka agad sa order popup:
   updated na.

> Script: "So if the artisan changes her GCash number or swaps to a new QR code,
> she does it here — no developer, no redeploy. Changes take effect immediately
> on every customer's payment screen."

---

## 6. Likely Questions While Presenting (short answers)

**"Saan galing yung QR code na pinapakita niyo?"**
> "From the database — the admin uploads the real QR image in Payment Settings.
> Kung walang upload pa, may stylized placeholder kami para hindi blank. The
> display mode controls whether customers also see the number and account name."

**"Bakit walang payment method sa checkout?"**
> "Because the customer only pays the *final* total. J&T quotes the shipping fee
> from the parcel's weight, so the studio confirms the order with that fee
> first — only then do GCash and GOtyme open on the order page."

**"What if nagpalit ng number yung store owner?"**
> "She edits it in Payment Settings. There's no hardcoded number anywhere in
> the code — the popup reads from the PaymentSettings table. Defaults lang ang
> nasa seed, at self-healing ang table creation kaya walang migration."

**"Ano ang gagawin kung di tugma ang reference number?"**
> "The studio declines it behind a password. The payment goes FAILED — never
> PAID — and the customer gets the support line on screen and in a
> notification, then resubmits a corrected reference. One side can never
> complete a payment alone: kaninong sinabi natin na bayad, hindi bayad."

**"Bakit walang real payment gateway?"**
> "Deliberate scope: we record the method, reference number, and the studio's
> verification instead of wiring an API. The architecture leaves room for one —
> OrderService isolates the confirmation step and records a GatewayResponse."

**"How do you prevent overselling?"**
> "One transaction per checkout; the stock UPDATE is conditional
> (`WHERE StockQuantity >= qty`), so the database itself arbitrates the last unit."

**"How are passwords stored?"**
> "PBKDF2-HMAC-SHA256, 10,000 iterations, per-user salt, constant-time compare.
> Payment verification re-checks the password against a fresh DB read — mali ang
> password, hindi sasara ang confirmation form."

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
3. **Checkout** — address + phone lang, "Shipping quoted after confirmation",
   no payment method → **Place Order** → flash + redirect to Order Detail.
4. **Merchant confirms** — logout, login `admin/admin123` → Orders & Payments →
   **Confirm & Quote**: fee (mistype the password once to show the guard) →
   CONFIRMED, total is now final.
5. **⭐ Pay (bella)** — Order Detail: pick **GCash** → **Scan-to-Pay popup**
   (QR/number/name). Ipoint out: "all of this comes from admin settings."
   Enter ref + password → yellow **"Payment submitted — waiting for
   verification"** card.
6. **⭐ Verify (admin)** — the stored ref shows next to a password box →
   **Confirm** → PAID + receipt. (Optional: Decline once first to show FAILED +
   the support line, then resubmit and confirm.)
7. **Fulfill** — PROCESSING (note it refused before the payment was confirmed)
   → Book J&T with a tracking number → SHIPPED → DELIVERED.
8. **Receive** — bella taps **"Confirm order received"** → RECEIVED → review.
9. **⭐ Payment Settings** — show the two channel cards, the **display mode
   dropdown** (switch it to "Number + name only" and save), then open the order
   popup again — the QR image is gone, only number + name show. Switch back.
10. **Commission Hub** — 5-step wizard → merchant quotes an offer (accept/
    decline, walang clarify loop) → confirm → pay → password-gated verify →
    production. End with the delivery-only note.

**Panic fallbacks:**
- Site won't start → rerun `run-website.bat`; it prints WHICH database it chose.
  The site runs on **Supabase (cloud)** — walang local database na i-start.
- Page error → "our error policy is friendly message + full detail logged to AppErrors."
- Walang internet para sa tunnel → localhost works offline, lahat gumagana.
- Data looks empty → `tools\reseed-supabase.bat` re-creates schema + seed
  (WARNING: it wipes orders, payments and users — type `RESET` to proceed).
- Worst case → screenshots + Section 5 narrative.

---

## 8. Limitations (volunteer these — sounds honest, avoids gotchas)

- No live payment gateway — reference numbers + studio verification, not API-verified.
- Tracking links to J&T's own site; no webhook sync.
- Booth takings are read-only in the reports (seeded demo rows) — no in-store sales-entry or per-event stock screen (deliberate scope cut).
- Single-owner model; multi-tenant sellers would need role expansion.
- Future: real e-wallet API, courier webhook, email notifications, PWA offline view.

---

## 9. One-liners (memorize, isigaw sa transitions)

- "All SQL lives in repositories; all rules live in services; pages only render."
- "One transaction per checkout — the database arbitrates the last unit."
- "The studio quotes the J&T fee first; the customer pays the final total only."
- "Password re-entry gates every step that touches money: quote, submit, confirm, decline."
- "Nothing ships, and nothing starts processing, until the payment is verified."
- "Two roles: the owner is the admin; everyone else is a customer."
- "A customer submission only parks the payment as SUBMITTED — the studio's
  verification is what makes it PAID."
- **"The QR details are data, not code — the owner edits them from the admin
  panel, and every customer screen updates instantly."** ⭐
- "The website states the courier status; it doesn't pretend to be the courier."

---

## 10. Pre-Presentation Checklist

- [ ] Practice the demo flow twice on THIS machine (db = Supabase; `start-db.bat`
      prints "Using Supabase — no local database to start").
- [ ] **Upload real GCash/GOtyme QR screenshots sa Payment Settings** para maganda
      ang popup sa demo (PNG/JPG, square, max 5 MB).
- [ ] Demo logins ready: `bella/customer123` · `admin/admin123` · `mika/admin123`.
- [ ] May pending PENDING order na pwedeng i-quote/i-confirm ng owner, at may
      SUBMITTED payment na pwedeng i-verify.
- [ ] Tunnel URL OR localhost (pareho gumagana; tunnel needs internet).
- [ ] Basahin ang Sections 1, 5, and 9 bago pumasok.
- [ ] Screenshots ng dashboard/reports naka-open sa tabs (Plan B).