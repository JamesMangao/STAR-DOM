# STAR:DOM — Presentation Cheat Sheet

> Read this two or three times before you present. The **scripts** (the words in
> `>` blocks) are in English — say them as written. The **notes** are reminders
> for you, not for the panel.
>
> Sections 1, 5 and 9 are the ones that matter most. Good luck!

---

## 1. Opening Pitch (30 seconds)

Say this while the landing page is still loading.

> "STAR:DOM is an online marketplace and pop-up hub for a Filipino solo artisan
> brand. Customers order ready-made products online — shipped nationwide via
> J&T Express — and can request custom commissions through a five-step wizard.
>
> The store owner runs everything from one console: products, events, order
> fulfilment, and the online payment channels themselves — how many there are
> is a setting, not a code change.
>
> The important part is that the system enforces the real workflow. An order
> starts unpaid, because J&T charges by weight and the studio has to quote the
> shipping fee before the total is final. The customer then pays by QR against
> whichever channel the owner enabled, quotes a reference number, and the studio verifies that reference behind a
> password before anything ships or starts processing. One side alone can never
> complete a payment."

**Note:** as you say this, the `http://localhost:8095` tab should already be
open. When you finish, scroll straight to the featured products.

---

## 2. Tech Stack

| Layer | Technology | Five-second explanation |
|---|---|---|
| Language | VB.NET (.NET Framework 4.8) | Server-rendered; matches the capstone spec |
| UI | ASP.NET Web Forms | No separate frontend build — one deploy |
| Data | PostgreSQL (Supabase) via Npgsql | Parameterised queries only, so injection-safe |
| Dev server | IIS Express, port 8095 | `run-website.bat` does everything for you |
| Production | Docker (Mono/XSP4) on Render | Free tier, containerised |
| Sharing | Cloudflare quick tunnel | Gives you a public URL for the demo |

**Architecture — draw this on the whiteboard if they ask:**

```
Browser → Page → Service → Repository → PostgreSQL
```

Each layer only talks to the next one. Pages render, Services hold the rules,
Repositories hold all the SQL.

---

## 3. Roles — Exactly Two

| Role | Who | Can do |
|---|---|---|
| **CUSTOMER** | Anyone who registers — registration always creates a customer | Browse, order (delivery only), pay by QR against any enabled channel, review, request commissions |
| **ADMIN** | The single store owner — the admin *is* the merchant | Everything else: products and stock, events, order confirmation and J&T quote, payment verification, commission pipeline, reports, user management, Payment Settings |

**One-liner:** "It's a single-owner brand, so the admin is also the merchant.
There's no separate seller registration — every new account is a customer."

---

## 4. Numbers to Say Confidently

> Every number below was produced by `tools\doc-facts.bat`. Run it before a
> presentation — these drifted once because they were typed by hand.

- **30** database tables · **100** seeded products (91 active) · **3** active bundles · **7** pop-up events
- **34** pages · **14** repositories · **9** services · **80** VB.NET files, about **15,600** lines
- **93** tests (91 pass; 2 are date-dependent and unrelated to a demo)
- **2** roles · **1** admin account · **9** customers · **5** commission steps
- **3** payment channels live — GCash, GOtyme, BPI — and **unlimited**, because the owner
  can add one from the admin page without touching the code. No card, no COD,
  deliberately
- **1** courier: J&T Express
- Stickers **₱30** each, bundle **4 for ₱100** · Button pins **₱35** each, bundle **3 for ₱100**
- Shipping: **quoted by J&T**, never a flat fee. Indicative zones:
  Luzon ₱0–100 · Visayas ₱101–200 · Mindanao ₱201–300 — the real figure follows
  the parcel's weight and is confirmed by the studio before the total is final
- Order numbers look like `SD-yyyyMMdd-nnnn`
- Passwords: **PBKDF2-HMAC-SHA256**, 10,000 iterations, per-user salt

---

## 5. Core Demo Workflows

### A. Placing an order (customer side)

1. Add to cart — there's a toast confirmation.
2. Checkout asks for the **delivery address and phone only**. No payment
   method here, no COD, no pick-up. Shipping is "quoted after confirmation"
   because the J&T fee cannot be known while the parcel is unweighed.
3. **Place Order** writes the order in **one database transaction**, with a
   conditional stock decrement — so the last unit can never be oversold.
   Status is `PENDING`, total is goods minus bundle savings.
4. Flash message: "The studio will return it with the final price and shipping fee."

### B. Confirm and quote (owner side)

1. Go to **Orders & Payments**, open the `PENDING` order.
2. **Confirm & quote**: enter the J&T shipping fee and your password. That
   entry *is* the confirmation — status becomes `CONFIRMED` and the total is
   final, in the same statement.
3. The customer is notified: "Shipping is ₱X via J&T Express — final total ₱Y."
4. Until this runs, payment submission refuses: there is no final figure to pay.

### C. Paying by QR ⭐ (the feature to show proudly)

1. On **Order Detail**, the customer picks a channel. The list is whatever the
   owner has enabled — **GCash, GOtyme, BPI** — and it only appears once the
   fee is quoted.
2. The **Scan to Pay popup** opens. Everything in it is **admin-managed** —
   nothing is hardcoded:
   - the **QR image** (uploaded by the admin, or a styled placeholder)
   - the **account number** for that specific channel
   - the **account name**
   - the **display mode** — QR only, number + name only, name only, or everything

   **Pick BPI here.** It is the one no developer wrote code for; it exists
   because the owner added it. Then point out the footer says *"GCash · GOtyme ·
   Online banks · J&T Express"* — the banks collapsed into one chip, so it does
   not churn every time the owner changes a bank.
3. The customer pays in their wallet app, saves the **reference number**, and
   enters it **plus their password**.
4. This only parks the payment as **SUBMITTED** — it is not paid yet. A yellow
   "Payment submitted — waiting for verification" card appears. No receipt, no
   processing, no J&T booking.

### D. Verifying the payment (owner side) — this is where the money actually lands

- The merchant sees the submitted reference *and* a password box beside it:
  one form, two buttons — **Confirm** or **Decline**.
- **Password re-entry every time**, verified against a fresh database read, not
  against the session.
- **Confirm** → payment `PAID` → receipt issued. This is the only path that
  reaches `PAID`; a declined reference never does.
- **Decline** → payment `FAILED`; the customer is handed the support line
  ("message @star.d0mm on Instagram or contact 09701375033") and may resubmit.

### E. Fulfilment (J&T)

1. `PROCESSING` — refused until the payment is `PAID`.
2. **Book J&T** — the waybill number is required to reach `SHIPPED`.
3. `DELIVERED` — checks the *stored* waybill, not the form field.
4. The customer taps **"Confirm order received"** → `RECEIVED` → reviews unlock.

### F. Bundles (automatic pricing)

Five stickers = one bundle (₱100) plus one extra (₱30) = **₱130**, shown as a
separate "Bundle savings" line. Line prices stay truthful, so the receipt
adds up honestly.

### G. Commissions

Five-step wizard → the merchant **accepts and quotes an offer** (no deposit, no
clarify round-trip) → the customer confirms → pays by QR (reference
only) → the merchant **verifies behind a password** → production (in
production / revision / finalized) → **delivery only** → received → completed.

Worth mentioning: each artist has a **capacity** the system actually enforces —
you cannot flood an artist's queue with requests.

### H. Payment Settings (admin) ⭐ (demo this right after C or D)

1. Log out, log in as `admin / admin123`.
2. Sidebar → **SYSTEM → Payment Settings**.
3. One card per channel — **GCash (blue)**, **GOtyme (green)** and **BPI** — each with:
   - Account name and account number fields
   - QR image upload (PNG/JPG/WebP, max 5 MB) and a remove option
   - **"What customers see"** dropdown — *Everything / QR only / Number + name
     only / Name only*
   - **"Show at checkout"** toggle — off means it disappears from the customer's
     payment options
   - An optional caption under the QR
4. A **live preview** at the bottom of the page — that is exactly what the
   customer will see.
5. Save → "payment settings saved" flash → go straight back to the order popup
   and the change is already live.
6. **Then the best beat in the whole demo:** the **Add New Channel** card. Type
   a bank that does not exist yet, press the button, and it is immediately a
   real payment option for every customer — its own card, its own QR slot, its
   own account number.

> **Script:** "So if the artisan changes her GCash number, swaps to a new QR
> code, or decides to start accepting a bank I have never heard of, she does
> it here — no developer, no redeploy, no database migration. It takes effect
> immediately on every customer's payment screen. And I did not write code for
> BPI."

### I. Storefront spotlight (admin) ⭐ (a quick one)

1. Sidebar → **ADMIN → Storefront**.
2. Pick the product the landing hero should show. Save.
3. Go back to `/` — the hero is that product.
4. Unpin it and the hero falls back to the store’s **best-seller by units sold**,
   so the front page always leads with something that actually sells instead of
   whatever someone remembered to flag "featured".

> **Script:** "The front page picks its own hero. I can pin one, but if I
> delete it or hide it, it falls back to whatever is genuinely selling — the
> page is never empty and never stale."

---

## 6. Likely Questions While Presenting

**"Where does the QR code come from?"**
> "From the database — the admin uploads the real QR image in Payment Settings.
> If nothing has been uploaded yet we show a styled placeholder so it is never
> blank. The display mode controls whether customers also see the number and
> the account name."

**"Why is there no payment method at checkout?"**
> "Because the customer only pays the *final* total. J&T quotes the shipping
> fee from the parcel's weight, so the studio confirms the order with that fee
> first — only then do the enabled channels appear on the order page."

**"What if the store owner changes her number — or wants a bank you have never "
**"heard of?"**
> "She edits it in Payment Settings, and there is an Add New Channel button for
> a bank the code has never seen. No hardcoded number and no hardcoded channel
> list anywhere — the popup reads the Paymentsettings table, and the payment
> picker is built from it."

**"What happens if the reference number doesn't match?"**
> "The studio declines it behind a password. The payment goes `FAILED` — never
> `PAID` — and the customer gets the support line on screen and in a
> notification, then resubmits a corrected reference. Neither side can complete
> a payment alone: whoever says 'I paid it' doesn't get to decide."

**"Why no real payment gateway?"**
> "Deliberate scope. We record the method, the reference number, and the
> studio's verification instead of wiring in an API. The architecture leaves
> room for one — the order service isolates the confirmation step and already
> records a gateway response field."

**"How do you prevent overselling?"**
> "One transaction per checkout, and the stock UPDATE is conditional
> (`WHERE StockQuantity >= qty`), so the database itself arbitrates the last
> unit."

**"How are passwords stored?"**
> "PBKDF2-HMAC-SHA256, 10,000 iterations, per-user salt, constant-time
> comparison. Payment verification re-checks the password against a fresh
> database read — a wrong password will not go through."

**"SQL injection?"**
> "Impossible by construction — every query goes through one parameterised
> helper, and all SQL lives in the repository layer."

**"CSRF?"**
> "A per-session token verified in exactly one place in the request pipeline,
> so a new page cannot forget the check. A mismatch returns 403 and is logged."

**"What if a user is suspended — are they locked out?"**
> "Immediately. The shell re-checks the account on every page load, so a
> suspension or a role change takes effect on the very next request rather than
> whenever the person decides to log out."

**"Can a customer cancel after paying?"**
> "No. A customer can only cancel while the order is still `PENDING` and unpaid.
> Once it's processing, paid or shipped, cancelling is a conversation with the
> studio — and either way, a cancellation always requires a written reason."

---

## 7. Demo Flow (5–7 minutes, in order)

1. **Landing (signed out)** — storefront, featured pieces. Click **Add to cart**
   → the sign-in gate modal appears → sign in as `bella / customer123` → the
   item lands in the cart.
2. **Cart** — add 4 stickers: subtotal ₱120, bundle savings −₱20, total ₱100.
3. **Checkout** — address and phone only, "Shipping quoted after confirmation",
   no payment method → **Place Order** → flash, then Order Detail.
4. **Merchant confirms** — log out, log in as `admin / admin123` → Orders &
   Payments → **Confirm & Quote**. Mistype the password once to show the guard
   working → then confirm. Now `CONFIRMED` and the total is final.
5. ⭐ **Pay (bella)** — Order Detail: pick **BPI** (the channel I did not code)
   → the Scan-to-Pay popup with *its* QR, number and name. Point out: "all of
   this comes from admin settings."
   Enter the reference and her password → the yellow "Payment submitted —
   waiting for verification" card.
6. ⭐ **Verify (admin)** — the stored reference shows next to a password box →
   **Confirm** → `PAID` and a receipt. (Optional: decline once first to show
   `FAILED` plus the support line, then resubmit and confirm.)
7. **Fulfil** — `PROCESSING` (point out that it refused before the payment was
   confirmed) → book J&T with a tracking number → `SHIPPED` → `DELIVERED`.
8. **Receive** — bella taps "Confirm order received" → `RECEIVED` → review.
9. ⭐ **Payment Settings** — the three channel cards and the display-mode
   dropdown. Switch BPI to "Number + name only", save, then reopen the order
   popup: that QR image is gone and only the number and name show. Switch back.
10. ⭐ **Add New Channel** — type a bank that does not exist, press the button,
    save. Reopen the order payment picker: the new bank is already an option.
    That is the strongest 20 seconds in the demo.
11. **Storefront spotlight** — pin a hero product on `/`, then unpin it and show
    it fall back to the best-seller.
12. **Commission Hub** — the five-step wizard → the merchant quotes an offer →
    confirm → pay → password-gated verify → production. End on the delivery-only
    note and the enforced capacity.

**If something goes wrong:**

- Site won't start → run `doctor.bat`. It checks the machine and tells you
  exactly what is wrong without changing anything. Then `run-website.bat`.
- Page error → "our error policy is a friendly message on screen with the full
  detail logged to the AppErrors table."
- No internet for the tunnel → localhost still works, everything works.
- Data looks wrong → `tools\reseed-supabase.bat` rebuilds schema and seed.
  **Warning:** it wipes orders, payments and users, and asks you to type
  `RESET` to confirm.
- Worst case → screenshots, and walk through Section 5 out loud.

---

## 8. Limitations (offer these — it reads honest)

- No live payment gateway — reference numbers plus studio verification, not
  API-verified.
- Tracking links to J&T's own site; no webhook sync.
- Pop-up booths take no money. STAR:DOM is online-only, so there is no in-store
  sales entry at all and booth takings are absent from reports and dashboard.
  A pop-up is a marketing presence, not a till.
- Single-owner model; a multi-seller marketplace would need role expansion.
- The database password is typed once per machine and stored in a git-ignored
  file. There is no local fallback database, deliberately — one shared database
  means everyone sees the same data.
- Future: real e-wallet API, courier webhook, email notifications.

---

## 9. One-liners (memorise these for transitions)

- "All SQL lives in the repositories, all the rules live in the services, and
  the pages only render."
- "One transaction per checkout — the database arbitrates the last unit."
- "The studio quotes the J&T fee first; the customer only ever pays a final
  total."
- "Password re-entry gates every step that touches money: quote, submit,
  confirm, decline."
- "Nothing ships, and nothing starts processing, until the payment is verified."
- "Two roles: the owner is the admin, everyone else is a customer."
- "A customer submission only parks the payment as submitted — the studio's
  verification is what makes it paid."
- **"The QR details are data, not code — the owner edits them from the admin
  panel and every customer screen updates instantly."** ⭐
- "The website states the courier status; it doesn't pretend to be the courier."
- "There's one database, so a demo looks the same on every machine."

---

## 10. Pre-Presentation Checklist

- [ ] Run `setup.bat` once, then `doctor.bat`, and confirm it says ready.
- [ ] Run `tools\doc-facts.bat` and check every number in Section 4 still holds.
- [ ] **Upload real QR screenshots to Payment Settings** for all three channels
      (PNG/JPG, square, max 5 MB) so the popup looks good on screen.
- [ ] Demo logins ready: `admin / admin123` (the only admin) ·
      `bella / customer123` (customer)
- [ ] ⚠️ **`mika`, `renzo` and `puffu` are CUSTOMERS, not owners.** If your demo
      script logs in as one of them expecting the Merchant Dashboard, it will
      bounce straight back out. Use `admin`.
- [ ] Have one `PENDING` order ready for the owner to quote, and one `SUBMITTED`
      payment ready to verify.
- [ ] Tunnel URL or localhost — both work; the tunnel needs internet.
- [ ] Read Sections 1, 5 and 9 before you go in.
- [ ] Dashboard and reports screenshots open in tabs, as a backup.