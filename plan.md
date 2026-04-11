# Piano Vertu — Dev Plan

---

## 2026-03-28 — 18:45

### Planned: 6 changes to form, client portal, staff dashboard

---

#### 1. Change password (client) + Reset password (admin)

**Client — change own password:**
- New API: `PATCH /api/ChangePassword` — reads `X-Token` header to identify user, body: `{ current_password, new_password }`
- New file: `api/ChangePassword.cs`
- UI: Add "Changer le mot de passe" collapsible section in `src/dashboard-client.html`

**Admin — reset client password:**
- New API: `PATCH /api/ResetClientPassword` — admin-only, body: `{ user_id }`, auto-generates 8-char password and returns it
- New file: `api/ResetClientPassword.cs`
- UI: Add "Réinitialiser mot de passe" button in staff detail panel (`src/dashboard-staff.html`) — shows generated password in a box

---

#### 2. Remove typed signature + fix signed_at timestamp

**Remove typed option:**
- `src/index.html`: Remove `#typeToggle` button and `#typedNameRow` div
- `src/js/signature.js`: Remove `toggleType()`, `typeMode` export
- `src/js/main.js`: Remove typed signature path in `doSubmit()` — always use canvas data URL

**Signed_at full timestamp:**
- `src/dashboard-client.html` + `src/dashboard-staff.html`: Display `signed_at` with time (HH:MM:SS), not just date
- `api/SubmitRegistration.cs`: Verify `signed_at` stored as full datetime (GETUTCDATE())

---

#### 3. Hide tradeup PDF for digital/consignment categories

**Root cause:** `form.js` `onCatChange()` always shows `tradeupGate` regardless of category.

**Fix:**
- Add `has_tradeup BIT NOT NULL DEFAULT 0` column to `dbo.PianoCategory`
- `UPDATE dbo.PianoCategory SET has_tradeup = 1 WHERE id IN (1, 2)` — neuf + preloved only
- New file: `sql/add_has_tradeup_column.sql`
- `api/GetFormData.cs`: Add `has_tradeup` to categories SELECT + returned object
- `src/js/form.js` `onCatChange()`: Show tradeup gate only when `category.has_tradeup`

---

#### 4. Mobile PDF gate: require click to unlock, not timer

**Root cause:** On mobile, PDFs open in a new tab. Timer runs in background regardless of whether user actually viewed PDF.

**Fix in `src/js/pdf.js`:**
- On mobile (detected via touch), `markRead()` fires immediately when user clicks "open" button (no 20s wait)
- Same for tradeup: `markTradeupRead()` immediately on click on mobile
- `sigLock` only opens after BOTH warranty AND tradeup are read (when tradeupGate is visible)

---

#### 5. Fix intermittent PDF width on desktop

**Root cause:** Duplicate `frame.src = pdfUrl` assignment in `pdf.js` lines 92+95 — possible race/resize issue.

**Fix:**
- Remove duplicate `frame.src` line in `src/js/pdf.js`
- Add `width: 100%; min-width: 0` to `.pdf-gate iframe` CSS in `src/index.html`

---

#### 6. Client portal PDF links — on-click URL generation (no expiry)

**Problem:** SAS URLs pre-fetched on panel open expire after 30 min.

**Fix in `src/dashboard-client.html`:**
- Replace `loadPdfLink()` pre-fetch with an `openClientPdf(blobName)` function that generates a fresh SAS URL on each click and immediately opens it
- Render PDF fields as buttons, not pre-loaded links

---

### DB changes required (run manually before deploying)
```sql
-- Item 3: add has_tradeup flag to PianoCategory
ALTER TABLE dbo.PianoCategory ADD has_tradeup BIT NOT NULL DEFAULT 0;
UPDATE dbo.PianoCategory SET has_tradeup = 1 WHERE id IN (1, 2); -- Acoustique neuf + Occasion certifié
```

---

### Files to touch
| File | Change |
|---|---|
| `api/ChangePassword.cs` | New |
| `api/ResetClientPassword.cs` | New |
| `sql/add_has_tradeup_column.sql` | New |
| `api/GetFormData.cs` | Add `has_tradeup` |
| `src/js/form.js` | Conditional tradeup gate |
| `src/js/pdf.js` | Mobile unlock + duplicate src fix + sigLock logic |
| `src/js/signature.js` | Remove typed mode |
| `src/index.html` | Remove typed sig UI + iframe CSS |
| `src/js/main.js` | Remove typed sig path |
| `src/dashboard-client.html` | Password change section + on-click PDF links + signed_at time |
| `src/dashboard-staff.html` | Admin password reset button + signed_at time |

---

### Test plan (to be written after implementation)

#### Item 1 — Change / reset password
- [ ] Client: login → Changer mot de passe → enter wrong current password → error shown
- [ ] Client: login → enter correct current + new password → success → logout → login with new password works
- [ ] Admin: open client detail panel → click Réinitialiser → new password displayed → login as client with new password works
- [ ] API: `PATCH /api/ChangePassword` with no token → 401
- [ ] API: `PATCH /api/ResetClientPassword` with customer token → 403

#### Item 2 — Signature + timestamp
- [ ] Form: typed signature option no longer visible
- [ ] Form: canvas draw → submit → signed_at stored with full datetime
- [ ] Dashboard-client: signed_at shows date AND time (HH:MM:SS)
- [ ] Dashboard-staff: signed_at shows date AND time

#### Item 3 — Tradeup PDF visibility
- [ ] Select "Acoustique neuf" → tradeup gate appears
- [ ] Select "Occasion certifié" → tradeup gate appears
- [ ] Select "Numérique/Hybride" → tradeup gate hidden
- [ ] Select "Consignation" → tradeup gate hidden

#### Item 4 — Mobile PDF gate
- [ ] On mobile: click "Ouvrir" on warranty PDF → sig section unlocks immediately (no 20s wait)
- [ ] On mobile: if tradeupGate visible, must click tradeup "Ouvrir" too before sig section unlocks
- [ ] On desktop: timer still applies (20s after opening)

#### Item 5 — PDF width
- [ ] Desktop: open warranty PDF gate → PDF renders at full width consistently
- [ ] Refresh page multiple times to confirm no intermittent narrow rendering

#### Item 6 — Client portal PDF on-click
- [x] Open registration panel → click PDF button → new tab opens with PDF (URL is fresh)
- [x] Wait 35 min with panel open → click PDF button again → still works (fresh URL generated)

---

## 2026-04-10 — 12:00

### Completed: all 6 items from 2026-03-28 ✅

All items implemented and tested (40/40 automated tests passing).

**Notes / deviations from original plan:**
- Item 3: `has_tradeup = 1` set for `id IN (1, 3)` (Acoustique neuf + Occasion certifié), not `(1, 2)` as originally planned
- `ResetClientPassword`: accepts `customer_email` instead of `user_id` (GetRegistrations doesn't return user_id — simpler to look up by email)
- JWT `ClaimTypes.Role` stored as full URI `http://schemas.microsoft.com/ws/2008/06/identity/claims/role` in token payload — must use `jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)` when reading role from JWT

**DB migration run:**
```sql
ALTER TABLE dbo.PianoCategory ADD has_tradeup BIT NOT NULL DEFAULT 0;
UPDATE dbo.PianoCategory SET has_tradeup = 1 WHERE id IN (1, 3);
```

---

### Bug fix: PDF gate not resetting on category switch

**Root cause:** `onCatChange()` showed pdfGate immediately for all warranty categories, including type-based ones (Acoustique neuf, Numérique) where no PDF is available yet. Switching categories left the previous type's PDF in the iframe.

**Fix:**
- Added `resetPdfGate()` to `src/js/pdf.js` — clears iframe src, resets state/DOM
- `onCatChange()`: for type-based categories, hide gate + call `resetPdfGate()` instead of showing gate
- `onTypeChange()`: show pdfGate when URL is found (previously relied on onCatChange to show it)

**Files changed:** `src/js/pdf.js`, `src/js/form.js`

---

### Bug fix: Auth role guards

**Root cause:** Any role could access `dashboard-staff.html` — login pages redirected to dashboards without checking role.

**Fix:**
- `src/login-staff.html`: after login, redirect customers → `/dashboard-client.html`, staff roles → `/dashboard-staff.html`
- `src/dashboard-staff.html`: auth guard now rejects `role === 'customer'` → redirects to login-staff
- `src/login-client.html`: after login, redirect staff roles → `/dashboard-staff.html`, customers → `/dashboard-client.html`

**Files changed:** `src/login-staff.html`, `src/login-client.html`, `src/dashboard-staff.html`

---

## 2026-04-11

### Feature: Remove status badge from client portal

Status is an internal staff field — customers should not see "En attente / Complété / Payé".

**Fix:**
- Removed `statusBadge()` function and `statusMap` object from `src/dashboard-client.html`
- Removed status badge from card footer
- Removed "Statut" field from detail panel; renamed section title "Statut" → "Enregistrement"
- Removed `.badge` / `.badge-*` CSS rules

**Files changed:** `src/dashboard-client.html`

---

### Feature: Auto-language client portal (FR/EN/ZH)

Client portal now displays in the language the customer used during registration (`r.language` field). No manual switcher — language is detected automatically after data loads and persisted to `localStorage` (`pv_lang`).

**Fix:**
- Added self-contained `DC` translations object (FR/EN/ZH) to `src/dashboard-client.html`
- After `GetMyRegistrations` loads, sets `lang = allRows[0]?.language || 'fr'`
- `applyDashboardLang()` updates all static DOM (header, page title, pwd drawer labels)
- `fv()` uses translated Yes/No and locale-aware date formatting
- `renderCards()` and `buildPanelHTML()` use `t()` for all labels/section titles; card piano/category names use language-specific fields (`type_name_en`, `category_name_zh`, etc.)
- `submitPwdChange()` all messages translated
- `src/login-client.html` reads `pv_lang` from localStorage and applies matching language on load
- `api/GetMyRegistrations.cs`: added `category_name_en`, `category_name_zh`, `type_name_en`, `type_name_zh` to SELECT

**Files changed:** `src/dashboard-client.html`, `src/login-client.html`, `api/GetMyRegistrations.cs`

---

### Cleanup: Remove EmailJS

EmailJS was sending emails client-side after registration, exposing service ID and public key in frontend code.

**Fix:**
- Removed EmailJS SDK `<script>` tags from `src/index.html`
- Removed `EMAILJS_SERVICE`, `EMAILJS_TEMPLATE`, `EMAILJS_TEMPLATE_CUSTOMER` constants from `src/js/main.js`
- Removed staff notification email block and customer welcome email block from `doSubmit()`
- `src/emailjs-template.html` kept as content reference for future ACS implementation

**Files changed:** `src/index.html`, `src/js/main.js`

**⏳ TODO — Azure ACS email (future):**
- Move email sending server-side to `api/SubmitRegistration.cs`
- Use Azure Communication Services SDK for .NET
- Staff notification email (new registration) + customer welcome email (new account + credentials)
- ACS connection string as `AcsConnectionString` app setting

---

### Feature: Consignment-specific signature instruction

When consignment category is selected, the signature instruction now shows a no-warranty acknowledgment instead of the standard warranty agreement text, in all 3 languages.

**Fix:**
- Added `sigInstrConsign` key (FR/EN/ZH) to `src/js/lang.js`
- `applyLang()` checks `deps.isConsignment` to choose which text to render on `#sigInstr`
- Added `_isConsignment` state + `getIsConsignment()` export to `src/js/form.js`
- `onCatChange()` sets `_isConsignment = !category.has_warranty` and updates `#sigInstr` directly
- `src/js/main.js`: imported `getIsConsignment`, passed `isConsignment: getIsConsignment()` to both `applyLang()` calls

**Files changed:** `src/js/lang.js`, `src/js/form.js`, `src/js/main.js`

---

### Feature: Split status into payment_status + delivery_status

Replaced the single `status` field with two independent status fields, each backed by a DB lookup table.

**DB migration (run manually before deploying):** `sql/add_payment_delivery_status.sql`
- Creates `dbo.PaymentStatus` (not_paid / partially_paid / fully_paid / store_financing)
- Creates `dbo.DeliveryStatus` (to_plan / sent_to_mover / delivered)
- `ALTER TABLE dbo.Registrations ADD payment_status ... delivery_status ...` (both NOT NULL with defaults)
- Old `status` column left in DB untouched

**Files changed:** `api/GetRegistrations.cs`, `api/UpdateRegistration.cs`, `src/dashboard-staff.html`, `tests/run-tests.js`, `sql/add_payment_delivery_status.sql`

---

### Feature: Consignment notice + signature unlock in Garantie & Consentement

**Root cause:** For consignment (`has_warranty = false`), `pdfGate` is hidden so `_pdfRead` is never set — `tryUnlockSig()` requires `_pdfRead === true`, meaning `sigLock` never opened and the signature canvas was permanently blocked.

**Fix:**
- Added `#consignSigNote` div inside c5 (between tradeupGate and sig-lock wrapper) — always visible above the overlay, shows the no-warranty acknowledgment text in the selected language
- `applyLang()` sets `#l-consign-sig-note` text using existing `l_consign_note` key (FR/EN/ZH)
- Added `unlockForConsignment()` export to `src/js/pdf.js` — sets `_pdfRead = true` and calls `tryUnlockSig()` immediately
- `onCatChange()` shows `#consignSigNote` and calls `unlockForConsignment()` when `!category.has_warranty`; hides it and calls `resetPdfGate()` when deselecting category

**Files changed:** `src/index.html`, `src/js/lang.js`, `src/js/pdf.js`, `src/js/form.js`


