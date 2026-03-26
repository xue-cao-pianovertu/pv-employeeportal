# CLAUDE.md — Piano Vertu Employee Portal

Read this file before touching any code. Ask if anything is unclear before writing.

---

## Project Overview

Full-stack web app for **Piano Vertu**, a Montréal piano store.
Two surfaces:
1. **Customer registration form** — trilingual (FR/EN/ZH), mobile-first, in-store use
2. **Staff/employee portal** — login, view/edit submissions, audit trail

**Repo:** `pv-employeeportal`
**Live URL:** `https://orange-dune-09055d60f.4.azurestaticapps.net`

---

## Stack

| Layer | Technology |
|---|---|
| Frontend | Vanilla JS ES6 modules, HTML/CSS |
| Hosting | Azure Static Web Apps |
| Backend | Azure Functions C# (.NET 8, isolated worker model) |
| Database | Azure SQL |
| Blob Storage | Azure Blob Storage — `pianovertustorageaccount`, containers: `warranty-pdfs`, `signatures` |
| Email | EmailJS — Service: `service_bmg9k6m`, Template: `template_8dmr44f`, Key: `yJlzhbDnT4L72MyLn` |
| Auth | JWT (HmacSha256, 8h expiry) — secret in `JwtSecret` app setting |

---

## Project Structure

```
pv-employeeportal/
├── src/                        ← Static frontend (Azure Static Web Apps)
│   ├── index.html              ← Customer registration form ✅
│   ├── login-staff.html        ← Staff login ✅
│   ├── dashboard-staff.html    ← Staff dashboard ✅ COMPLETE (Phase 5)
│   ├── test-client.html        ← Random data generator (DEV ONLY)
│   ├── js/
│   │   ├── api.js              ← fetch calls, apiUrl() helper
│   │   ├── lang.js             ← translations, applyLang()
│   │   ├── form.js             ← dropdowns, surcharge, steppers, onCatChange
│   │   ├── pdf.js              ← PDF gate logic
│   │   ├── signature.js        ← canvas + typed signature mode
│   │   ├── progress.js         ← scroll progress bar
│   │   └── main.js             ← orchestration, loadFormData(), doSubmit()
│   └── img/
│       └── logo.png            ← Logo path is /img/logo.png everywhere
├── api/                        ← Azure Functions (C# .NET 8 isolated)
│   ├── GetBenches.cs           ← ✅
│   ├── GetCategories.cs        ← ✅
│   ├── GetFormData.cs          ← ✅
│   ├── SubmitRegistration.cs   ← ✅
│   ├── Login.cs                ← ✅
│   ├── GetRegistrations.cs     ← ✅
│   ├── UpdateRegistration.cs   ← ✅ (staff fields + status + price + audit)
│   ├── UpdateClientInfo.cs     ← ✅ (client fields + audit)
│   ├── UpdateLivraison.cs      ← ✅ (delivery fields + audit)
│   ├── UpdatePiano.cs          ← ✅ (piano fields + audit)
│   ├── GetAuditLog.cs          ← ✅ (GET /api/GetAuditLog?id=)
│   ├── GetSignatureUrl.cs      ← ✅ (GET /api/GetSignatureUrl?blob=)
│   ├── Program.cs
│   ├── host.json
│   ├── local.settings.json     ← NOT committed (contains secrets)
│   └── pv-employeeportal.csproj
├── sql/
│   ├── create_registrations_table.sql  ← ✅ run
│   ├── create_users_table.sql          ← ✅ run
│   ├── add_status_column.sql           ← ✅ run (status NVARCHAR(20) DEFAULT 'potential')
│   ├── add_price_column.sql            ← ✅ run (price DECIMAL(10,2) NULL)
│   └── create_audit_log_table.sql      ← ✅ run
├── tests/
│   └── run-tests.js            ← 30 automated tests (node tests/run-tests.js)
├── staticwebapp.config.json
└── .github/workflows/          ← CI/CD (watch for duplicate files — causes double triggers)
```

---

## Namespace & Conventions

- **C# namespace:** `PV.AZFunction`
- **Connection string key:** `SqlConnectionString`
- **Storage connection key:** `AzureStorageConnectionString`
- **JWT secret key:** `JwtSecret`
- **Local dev port:** `7072`
- **Logo path:** `/img/logo.png` (not `/assets/logo.png`)
- **API URL helper (JS):**
  ```js
  const API_BASE = window.location.hostname === 'localhost' ? 'http://localhost:7072' : '';
  const apiUrl = endpoint => `${API_BASE}/api/${endpoint}`;
  ```

---

## Database Tables

| Table | Purpose |
|---|---|
| `PianoCategory` | Categories: Acoustique neuf, Occasion certifié, Numérique/Hybride, Consignation |
| `PianoType` | Piano models with `name_fr`, `name_en`, `name_zh`, `brand_name`, `category_id` |
| `bench` | Bench models (`id`, `name`, `description`) |
| `WarrantyPdf` | Warranty PDFs: `type_id` (new/digital) or `category_id` (used) → blob name |
| `TradeUpPdf` | Trade-up/exchange policy PDFs → blob name |
| `PdfDocuments` | Consolidated PDF reference table |
| `Registrations` | ✅ All form fields + signature/PDF blob names + staff fields + status + price |
| `Users` | ✅ username, password (plain text until Phase 6), role, full_name |
| `AuditLog` | ✅ registration_id, changed_by, changed_at, section, changes_json |

### Key DB rules
- Store only **blob names** in DB, never SAS URLs
- Chinese string literals in SQL require `N` prefix: `N'全新原声钢琴'`
- `PianoCategory` has `allows_manual_entry` and `has_warranty` flags
- Consignation: no warranty PDF, but signature still required
- Used piano (`category_id`) shares one warranty PDF; new/digital use `type_id`
- `piano_serial` is nullable — staff can fill it in later via portal

### Registrations table key columns
- `ref_id` — format `PV-YYYY-MM-DD-XXXX` (server-generated, sequential per day)
- `status` — `NVARCHAR(20) NOT NULL DEFAULT 'potential'` — values: `potential | completed | paid | partially_paid`
- `price` — `DECIMAL(10,2) NULL` — sale price entered by staff
- `signature_blob_name` — blob in `signatures` container
- `warranty_pdf_blob` / `tradeup_pdf_blob` — looked up server-side, never sent from client
- Staff fields null on creation, filled via portal

### AuditLog table
- `section` values: `staff` | `client` | `livraison` | `piano`
- `changes_json` format: `{"field": {"old": "...", "new": "..."}}`
- `changed_by` extracted from JWT `unique_name` claim (gracefully falls back to "inconnu")

### Users table roles
`staff` | `admin` | `tuner` | `mover` | `teacher` | `customer`

---

## Azure Functions

### `GetBenches` ✅
- Returns all rows from `bench` table as JSON

### `GetCategories` ✅
- Returns all piano categories with trilingual names

### `GetFormData` ✅
- `GET /api/GetFormData?lang=fr|en|zh` (defaults to `fr`)
- Returns: categories, piano types, benches, warranty PDF SAS URLs, tradeup PDF SAS URL
- Requires both `SqlConnectionString` AND `AzureStorageConnectionString`

### `SubmitRegistration` ✅
- `POST /api/SubmitRegistration` — accepts full registration JSON payload
- Generates `PV-YYYY-MM-DD-XXXX` ref_id server-side
- Uploads signature to `signatures` blob container
- Auto-creates `customer` user account (if email not registered), returns generated password
- Returns `{ ref_id, success, new_account, client_username, client_password }`
- Uses `JsonNamingPolicy.SnakeCaseLower` for deserialization

### `Login` ✅
- `POST /api/Login` — `{ username, password }`
- Returns JWT `{ token, role, full_name }` — 8h expiry, HmacSha256
- JWT claims: `sub` (user id), `unique_name` (username), `full_name`, `role`

### `GetRegistrations` ✅
- `GET /api/GetRegistrations` — all registrations newest-first
- LEFT JOINs with `PianoCategory`, `PianoType`, `bench`
- Returns all columns including `status`, `price`, `signature_blob_name`

### `UpdateRegistration` ✅
- `PATCH /api/UpdateRegistration` — staff fields
- Fields: `invoice_number`, `from_location`, `old_piano_dest`, `surcharge_amount`, `cheque_to_collect`, `google_review`, `fully_paid`, `staff_notes`, `piano_serial`, `status`, `price`
- `from_location` options: 5193 – Tradition, 5223 – Signature, 3830 – Expérience, 5473 – Warehouse, Client pick up / Collecte par client
- `old_piano_dest` options: above + Recycle / Éco-Centre, Deuxième livraison
- `status` defaults to `"potential"` if not sent (never NULL)
- Reads JWT `Authorization` header to log `changed_by`
- SELECTs old values → diffs → writes to `AuditLog` if changed

### `UpdateClientInfo` ✅
- `PATCH /api/UpdateClientInfo` — client fields
- Fields: `customer_first_name`, `customer_last_name`, `customer_email`, `customer_phone1`, `customer_phone2`, `heard_from`
- Audit logs changes with section `"client"`

### `UpdateLivraison` ✅
- `PATCH /api/UpdateLivraison` — delivery fields
- Fields: address, within_40km, floor, elevator, steps_outside, steps_inside, stair_turns, crane_required, delivery_asap, delivery_date, delivery_notes, mover_notes, collect_piano, collect_desc, recycle_piano, surcharge_flag
- Audit logs changes with section `"livraison"`

### `UpdatePiano` ✅
- `PATCH /api/UpdatePiano` — piano fields
- Fields: `piano_make`, `piano_model`, `piano_color`, `purchase_date`, `accessories`, `piano_notes`, `bench_notes`
- Audit logs changes with section `"piano"`

### `GetAuditLog` ✅
- `GET /api/GetAuditLog?id={registrationId}`
- Returns all audit entries for a registration, newest first
- Entry shape: `{ id, changed_by, changed_at, section, changes_json }`

### `GetPdfUrl` ✅
- `GET /api/GetPdfUrl?blob={blobName}`
- Returns 30-min SAS URL for a blob in the `warranty-pdfs` container
- Returns `{ url }`
- Used by customer dashboard to generate download links for warranty/tradeup PDFs

### `GetSignatureUrl` ✅
- `GET /api/GetSignatureUrl?blob={blobName}`
- Returns 30-min SAS URL for a blob in the `signatures` container
- Returns `{ url }`

### Shared helpers in `UpdateRegistration.cs`
- `UpdateRegistration.GetUsername(req)` — extracts `unique_name` from JWT without full validation (Phase 6 will add full validation)
- `UpdateRegistration.WriteAuditLog(conn, regId, changedBy, section, changes)` — called by all Update* functions

### Azure SWA strips the Authorization header ⚠️
Azure Static Web Apps does **not** forward the `Authorization` header to managed API functions. Any function that reads `req.Headers["Authorization"]` will receive an empty string in production.

**Workaround:** Use a custom header `X-Token` alongside `Authorization`:
- Frontend: send both `Authorization: Bearer <token>` AND `X-Token: Bearer <token>`
- Function: check `X-Token` first, fall back to `Authorization` (for local dev compatibility)

`GetMyRegistrations.cs` implements this. The staff update functions still use `Authorization` only — audit log `changed_by` will show `"inconnu"` in production until fixed (tracked for Phase 6).

### JWT claim mapping
`JwtSecurityTokenHandler.ReadJwtToken()` remaps standard JWT claim names to .NET long-form equivalents. Use `jwt.Payload.Sub` to read the `sub` field directly from the raw JSON payload, bypassing mapping entirely.

---

## Staff Dashboard (`dashboard-staff.html`)

### Table
- Columns: Réf, Date, Client, Téléphone, Piano, Catégorie, Facture, Statut
- Search bar filters all text columns live
- Status filter dropdown: Tous / Potentiel / Complété / Payé / Part. payé

### Status badges
| Value | Badge |
|---|---|
| `potential` | Gray — Potentiel |
| `completed` | Blue — Complété |
| `paid` | Green — Payé |
| `partially_paid` | Amber — Part. payé |

### Detail panel sections
Each customer-entered section has a **Modifier** button that toggles an inline edit form:
1. **Client** → `UpdateClientInfo`
2. **Livraison** → `UpdateLivraison`
3. **Piano** → `UpdatePiano`
4. **Documents & Signature** — read-only; shows signature image (drawn) or typed name
5. **Champs personnel** — always-editable staff form → `UpdateRegistration`
6. **Historique des modifications** — lazy-loaded audit log via `GetAuditLog`

### Saving behavior
- Client / Livraison / Piano saves use toast notification + `openPanel(id)` to refresh
- Staff section save uses inline message (no panel rebuild)
- All save requests send `Authorization: Bearer <token>` header for audit logging

### Key JS patterns
- `fv(val)` — formats values: booleans → Oui/Non, dates → fr-CA locale, null → '—'. **Do NOT use `fv()` for integer step counts** (stair_turns, steps_outside, steps_inside) — display raw numbers directly
- `toggleEdit(formId)` — shows/hides edit form (`display: none` ↔ `flex`)
- `showToast(msg)` — bottom-center toast, auto-hides after 2.2s
- `loadAuditLog(id)` — fetches and renders audit log section
- `loadSignature(id, blobName)` — fetches SAS URL and renders `<img>`

---

## Frontend Key Patterns

### ES6 Modules
`index.html` uses `<script type="module">`. No inline scripts.
Export primitives via getters, not direct exports:
```js
// WRONG
export let pdfRead = false;
// RIGHT
let _pdfRead = false;
export const getPdfRead = () => _pdfRead;
```

### loadFormData() flow (main.js)
Called once on `DOMContentLoaded`. On language switch, re-fetches to get correct SAS URLs.

### PDF gate logic
- `pdfGate` and `tradeupGate` start hidden — shown by `onCatChange`
- Warranty PDF: `new_piano_warranty_{typeId}` for new/digital, `used_piano` for used
- Consignment (`!has_warranty`): hide warranty gate entirely

### Category / type dropdown logic (form.js)
- `allows_manual_entry = true` → hide type dropdown AND readonly make field, show `#makeEditRow`
- `allows_manual_entry = false` + has types → show type dropdown

### Subdomain routing
`index.html` detects `staff.pianovertu.ca` and redirects to `/login-staff.html`.

### Auth flow (frontend)
- JWT stored in `localStorage`: `pv_token`, `pv_role`, `pv_full_name`
- `dashboard-staff.html` redirects to login if no token
- Full backend token validation: Phase 6

### Other gotchas
- Use `textContent` not `innerHTML` — prevents `&amp;` rendering issues
- iOS pull-to-refresh: disabled via `overscroll-behavior-y: none` + JS touch handler
- Mobile PDF: served via Google Docs viewer proxy
- Duplicate `.github/workflows/` file causes double CI triggers — check before deploying

---

## Visual Style

| Token | Value |
|---|---|
| Primary navy | `#0d1f3c` |
| Gold accent | `#c9a84c` |
| Background | `#f0eee9` (cream) |
| Fonts | Playfair Display (headings) + DM Sans (body) |
| Cards | White, rounded, navy header |

---

## Automated Tests

```bash
node tests/run-tests.js
# Requires functions running on port 7072
```

34 tests covering: GetFormData, SubmitRegistration, Login, GetRegistrations, UpdateRegistration, UpdateClientInfo, UpdateLivraison, UpdatePiano, GetAuditLog, GetMyRegistrations.

Tests use `patchAuth()` helper (sends `Authorization` header) for all staff endpoints. `adminToken` is captured during Login test and reused. `customerToken` is captured separately for `GetMyRegistrations` tests.

---

## Phase Roadmap

### Phase 1 — `SubmitRegistration` ✅ COMPLETE

### Phase 2 — Email notifications ⏳ PENDING
- **⚠️ TO EVALUATE:** Move customer welcome email from EmailJS (client-side, credentials exposed) to server-side via **ACS** or **Azure Communication Services (ACS)** before go-live
- Staff notification email on new registration also pending

### Phase 3 — Authentication ✅ COMPLETE

### Phase 4 — Customer portal ✅ COMPLETE + DEPLOYED
- `login-client.html` ✅ working in production
- `dashboard-client.html` ✅ working in production — card layout, read-only detail panel
- `GET /api/GetMyRegistrations` ✅ working — uses `X-Token` header to bypass SWA Authorization stripping

### Phase 5 — Staff portal ✅ COMPLETE
- Full registrations table with search + status filter
- Detail panel with all registration data
- Editable sections: Client, Livraison, Piano, Champs personnel
- Audit trail: who changed what, per field, per section
- Status: potential / completed / paid / partially paid
- Price field tracked by staff

### Phase 6 — Security hardening (before go-live)
- BCrypt password hashing
- HttpOnly cookies (replace localStorage JWT)
- JWT validation middleware on all protected endpoints
- Input sanitization
- SAS-only blob access
- Remove hardcoded `admin/changeme` credentials

---

## Portal Files Status

| File | Status | Notes |
|---|---|---|
| `src/index.html` | ✅ Working | Registration form, subdomain detection |
| `src/login-staff.html` | ✅ Working | JWT stored in localStorage |
| `src/dashboard-staff.html` | ✅ Complete | Full Phase 5 staff portal |
| `src/login-client.html` | ✅ Complete | Customer login |
| `src/dashboard-client.html` | ✅ Complete | Read-only card view + detail panel |
| `src/test-client.html` | ✅ Working | DEV ONLY — random data generator |

---

## Local Dev Setup

```bash
cd api && func start
# Functions at http://localhost:7072/api/
```

`local.settings.json` must contain:
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "SqlConnectionString": "...",
    "AzureStorageConnectionString": "DefaultEndpointsProtocol=https;AccountName=pianovertustorageaccount;AccountKey=...;EndpointSuffix=core.windows.net",
    "JwtSecret": "..."
  },
  "Host": {
    "CORS": "http://localhost:3000"
  }
}
```

---

## Instructions for Claude Code

1. **Read this file first**, then read any file you are about to modify
2. **State your plan** before writing any code
3. **One task at a time** — build and confirm working before moving to next
4. **Never commit** `local.settings.json`
5. **Never store SAS URLs** in the database — blob names only
6. **Match the namespace** `PV.AZFunction` for all new functions
7. **Follow existing function structure** — use `GetBenches.cs` as the pattern for new HTTP trigger functions
8. **JWT secret** — always read from `JwtSecret` env var, never hardcode
9. **Audit logging** — all staff-side UPDATE functions must: SELECT old values → diff → call `UpdateRegistration.WriteAuditLog()` if changed
10. **status field** — always default to `"potential"` if not provided, never pass NULL (NOT NULL column)



current status:
⏺ Phase 4 is complete. Here's what was built:                                                                                                                 
                                                                                          
  src/dashboard-client.html                                                                                                                                   
  - Auth guard: redirects to /login-client.html if no token or role !== customer                                                                              
  - Fetches GET /api/GetMyRegistrations with Authorization: Bearer <token> header                                                                             
  - Displays registrations as clickable cards showing: ref_id, date, piano name, category, city, status badge                                                 
  - Status labels match staff dashboard (Potentiel / Complété / Payé / Part. payé) — "En attente" label used for customers instead of the internal "Potentiel"
  - Detail panel (read-only, no editing): Statut, Client, Livraison, Piano, Documents & Signature sections                                                    
  - Signature image loaded via GetSignatureUrl API — same pattern as staff dashboard                                                                          
  - Staff fields excluded from view (no price, invoice, staff_notes)                                                                                          
  - 401 response auto-logs the customer out                                                                                                                   
                                                                                                                                                              
  tests/run-tests.js — 4 new tests (34 total):                                                                                                                
  - Re-login as customer → capture customerToken                                                                                                              
  - GetMyRegistrations returns array with correct fields, no staff fields                                                                                     
  - No token → 401                                                                                                                                            
  - Invalid token → 401     


  

  currently: both point to index.html
  client.pianovertu.com
  staff.pianovertu.com bo