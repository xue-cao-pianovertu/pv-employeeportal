// Piano Vertu API — Automated test runner
// Usage: node tests/run-tests.js
// Requires Node 18+ (built-in fetch)

const BASE = process.env.API_BASE || 'http://localhost:7072';

const GREEN  = '\x1b[32m';
const RED    = '\x1b[31m';
const YELLOW = '\x1b[33m';
const RESET  = '\x1b[0m';
const BOLD   = '\x1b[1m';

let passed = 0, failed = 0;
let testEmail, testPassword, testRegId, testRefId, adminToken, staffToken, testSalesStaffId;

async function test(name, fn) {
  try {
    await fn();
    console.log(`  ${GREEN}✓${RESET} ${name}`);
    passed++;
  } catch (err) {
    console.log(`  ${RED}✗${RESET} ${name}`);
    console.log(`    ${RED}${err.message}${RESET}`);
    failed++;
  }
}

function assert(cond, msg) {
  if (!cond) throw new Error(msg || 'Assertion failed');
}

async function post(path, body) {
  const res = await fetch(`${BASE}/api/${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  return { res, data: await res.json() };
}

async function get(path) {
  const res = await fetch(`${BASE}/api/${path}`);
  return { res, data: await res.json() };
}

async function patch(path, body) {
  const res = await fetch(`${BASE}/api/${path}`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  return { res, data: await res.json() };
}

async function patchAuth(path, body, tok) {
  const res = await fetch(`${BASE}/api/${path}`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${tok}` },
    body: JSON.stringify(body),
  });
  return { res, data: await res.json() };
}

async function patchXToken(path, body, tok) {
  const res = await fetch(`${BASE}/api/${path}`, {
    method: 'PATCH',
    headers: {
      'Content-Type': 'application/json',
      'X-Token': `Bearer ${tok}`,
      'Authorization': `Bearer ${tok}`,
    },
    body: JSON.stringify(body),
  });
  return { res, data: await res.json() };
}

async function postXToken(path, body, tok) {
  const res = await fetch(`${BASE}/api/${path}`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Token': tok ? `Bearer ${tok}` : '',
      'Authorization': tok ? `Bearer ${tok}` : '',
    },
    body: JSON.stringify(body),
  });
  return { res, data: await res.json() };
}

// ── Test suite ────────────────────────────────────────────────

console.log(`\n${BOLD}Piano Vertu — API Tests${RESET}`);
console.log(`Target: ${YELLOW}${BASE}${RESET}\n`);

(async () => {

 // 1. GetFormData
  console.log(`${BOLD}GetFormData${RESET}`);
  await test('GET /api/GetFormData?lang=fr returns categories', async () => {
    const { res, data } = await get('GetFormData?lang=fr');
    assert(res.ok, `HTTP ${res.status}`);
    assert(Array.isArray(data.categories), 'categories is not an array');
    assert(data.categories.length > 0, 'categories is empty');
    assert(Array.isArray(data.pianoTypes), 'pianoTypes missing');
    assert(Array.isArray(data.benches), 'benches missing');
    assert(data.pdfs, 'pdfs missing');
  });

  await test('GET /api/GetFormData?lang=en returns english names', async () => {
    const { res, data } = await get('GetFormData?lang=en');
    assert(res.ok, `HTTP ${res.status}`);
    assert(data.categories[0].name_en, 'name_en missing');
  });

  // 2. SubmitRegistration
  console.log(`\n${BOLD}SubmitRegistration${RESET}`);

  const ts  = Date.now();
  testEmail = `test.auto.${ts}@test.pianovertu.ca`;
  const payload = {
    language: 'FR',
    customer_first_name: 'Test',
    customer_last_name:  'Automatique',
    customer_email:       testEmail,
    customer_phone1:     '514-555-0100',
    delivery_street:     '1234 Rue Sherbrooke O',
    delivery_city:       'Montréal',
    delivery_province:   'QC',
    delivery_postal:     'H3A 1B5',
    within_40km:         true,
    delivery_elevator:   false,
    steps_outside: 2, steps_inside: 1, stair_turns: 3,
    collect_piano: false, recycle_piano: false, crane_required: false,
    delivery_asap: true,
    surcharge_flag: false,
    piano_category_id: 1,
    piano_make:  'Kawai',
    piano_model: 'K200',
    piano_color: 'Noir laqué',
    purchase_date: new Date().toISOString().split('T')[0],
    humidity_confirmed: true,
    signature_type: 'typed',
    signature_data: 'Test Automatique',
  };

  await test('POST /api/SubmitRegistration returns ref_id', async () => {
    const { res, data } = await post('SubmitRegistration', payload);
    assert(res.ok, `HTTP ${res.status}: ${JSON.stringify(data)}`);
    assert(data.ref_id,  'ref_id missing');
    assert(data.success, 'success not true');
    assert(data.ref_id.startsWith('PV-'), `ref_id format wrong: ${data.ref_id}`);
    testRefId = data.ref_id;
  });

  await test('POST /api/SubmitRegistration creates customer account', async () => {
    const { res, data } = await post('SubmitRegistration', { ...payload, customer_email: `new.${ts}@test.pianovertu.ca` });
    assert(res.ok, `HTTP ${res.status}`);
    assert(data.new_account === true, 'new_account not true');
    assert(data.client_username, 'client_username missing');
    assert(data.client_password === 'pianolover', `client_password invalid: ${data.client_password}`);
    testPassword = data.client_password;
    testEmail    = data.client_username;
  });

  await test('POST /api/SubmitRegistration — duplicate email skips account creation', async () => {
    const { res, data } = await post('SubmitRegistration', { ...payload, customer_email: testEmail });
    assert(res.ok, `HTTP ${res.status}`);
    assert(data.new_account === false, 'should not create duplicate account');
  });

  // 3. Login
  console.log(`\n${BOLD}Login${RESET}`);

  await test('POST /api/Login — admin credentials return token', async () => {
    const { res, data } = await post('Login', { username: 'admin', password: 'plschangeme' });
    assert(res.ok, `HTTP ${res.status}: ${JSON.stringify(data)}`);
    assert(data.token,     'token missing');
    assert(data.role,      'role missing');
    assert(data.full_name, 'full_name missing');
    assert(data.role === 'admin', `role should be admin, got ${data.role}`);
    adminToken = data.token;
  });

  await test('POST /api/Login — customer credentials return token', async () => {
    const { res, data } = await post('Login', { username: testEmail, password: testPassword });
    assert(res.ok, `HTTP ${res.status}: ${JSON.stringify(data)}`);
    assert(data.token, 'token missing');
    assert(data.role === 'customer', `role should be customer, got ${data.role}`);
    staffToken = data.token; // non-admin token reused to verify admin-only endpoints reject it
  });

  await test('POST /api/Login — wrong password returns 401', async () => {
    const { res } = await post('Login', { username: 'admin', password: 'wrongpassword' });
    assert(res.status === 401, `expected 401, got ${res.status}`);
  });

  await test('POST /api/Login — missing fields returns 400', async () => {
    const { res } = await post('Login', { username: 'admin' });
    assert(res.status === 400, `expected 400, got ${res.status}`);
  });

  // 4. GetRegistrations
  console.log(`\n${BOLD}GetRegistrations${RESET}`);

  await test('GET /api/GetRegistrations returns array with expected fields', async () => {
    const { res, data } = await get('GetRegistrations');
    assert(res.ok, `HTTP ${res.status}`);
    assert(Array.isArray(data), 'response is not an array');
    assert(data.length > 0, 'no registrations found');
    const row = data[0];
    assert(row.ref_id,    'ref_id missing');
    assert(row.created_at,'created_at missing');
    assert('price'  in row, 'price field missing');
  });

  await test('GET /api/GetRegistrations includes the test registration', async () => {
    const { res, data } = await get('GetRegistrations');
    assert(res.ok, `HTTP ${res.status}`);
    const found = data.find(r => r.ref_id === testRefId);
    assert(found, `Could not find ${testRefId} in results`);
    testRegId = found.id;
  });

  // 5. UpdateRegistration
  console.log(`\n${BOLD}UpdateRegistration${RESET}`);

  await test('PATCH /api/UpdateRegistration saves staff fields', async () => {
    assert(testRegId, 'no testRegId from previous test');
    const { res, data } = await patchAuth('UpdateRegistration', {
      id:               testRegId,
      invoice_number:   'INV-TEST-001',
      piano_serial:     'SN-AUTO-TEST',
      from_location:    '5193 – Tradition',
      old_piano_dest:   'Recycle / Éco-Centre',
      surcharge_amount: 75.00,
      price:            8999.99,
      cheque_to_collect: false,
      google_review:    true,
      fully_paid:       false,
      status:           'completed',
      staff_notes:      'Test automatisé',
    }, adminToken);
    assert(res.ok, `HTTP ${res.status}: ${JSON.stringify(data)}`);
    assert(data.success, 'success not true');
  });

  await test('PATCH /api/UpdateRegistration — unknown id returns 404', async () => {
    const { res } = await patch('UpdateRegistration', {
      id: 999999, surcharge_amount: 0, cheque_to_collect: false,
      google_review: false, fully_paid: false, status: 'potential',
    });
    assert(res.status === 404, `expected 404, got ${res.status}`);
  });

  await test('PATCH /api/UpdateRegistration — missing id returns 400', async () => {
    const { res } = await patch('UpdateRegistration', { invoice_number: 'X' });
    assert(res.status === 400, `expected 400, got ${res.status}`);
  });

  // 6. UpdateClientInfo
  console.log(`\n${BOLD}UpdateClientInfo${RESET}`);

  await test('PATCH /api/UpdateClientInfo updates client fields', async () => {
    assert(testRegId, 'no testRegId');
    const { res, data } = await patchAuth('UpdateClientInfo', {
      id:                  testRegId,
      customer_first_name: 'Testé',
      customer_last_name:  'Automatique',
      customer_email:      testEmail,
      customer_phone1:     '514-555-0200',
      heard_from:          'Test automatisé',
    }, adminToken);
    assert(res.ok, `HTTP ${res.status}: ${JSON.stringify(data)}`);
    assert(data.success, 'success not true');
  });

  await test('PATCH /api/UpdateClientInfo — unknown id returns 404', async () => {
    const { res } = await patchAuth('UpdateClientInfo', { id: 999999, customer_first_name: 'X' }, adminToken);
    assert(res.status === 404, `expected 404, got ${res.status}`);
  });

  await test('PATCH /api/UpdateClientInfo — missing id returns 400', async () => {
    const { res } = await patchAuth('UpdateClientInfo', { customer_first_name: 'X' }, adminToken);
    assert(res.status === 400, `expected 400, got ${res.status}`);
  });

  // 7. UpdateLivraison
  console.log(`\n${BOLD}UpdateLivraison${RESET}`);

  await test('PATCH /api/UpdateLivraison saves delivery fields', async () => {
    assert(testRegId, 'no testRegId');
    const { res, data } = await patchAuth('UpdateLivraison', {
      id:                testRegId,
      delivery_street:   '1234 Rue Sherbrooke O',
      delivery_city:     'Montréal',
      delivery_province: 'QC',
      delivery_postal:   'H3A 1B5',
      within_40km:       true,
      delivery_floor:    '1er étage',
      delivery_elevator: false,
      steps_outside:     3,
      steps_inside:      1,
      stair_turns:       0,
      crane_required:    false,
      delivery_asap:     false,
      delivery_date:     '2026-04-15',
      collect_piano:     false,
      recycle_piano:     false,
      surcharge_flag:    false,
    }, adminToken);
    assert(res.ok, `HTTP ${res.status}: ${JSON.stringify(data)}`);
    assert(data.success, 'success not true');
  });

  await test('PATCH /api/UpdateLivraison — unknown id returns 404', async () => {
    const { res } = await patchAuth('UpdateLivraison', {
      id: 999999, within_40km: false, delivery_elevator: false,
      steps_outside: 0, steps_inside: 0, stair_turns: 0,
      crane_required: false, delivery_asap: false,
      collect_piano: false, recycle_piano: false, surcharge_flag: false,
    }, adminToken);
    assert(res.status === 404, `expected 404, got ${res.status}`);
  });

  await test('PATCH /api/UpdateLivraison — missing id returns 400', async () => {
    const { res } = await patchAuth('UpdateLivraison', { delivery_city: 'X' }, adminToken);
    assert(res.status === 400, `expected 400, got ${res.status}`);
  });

  // 8. UpdatePiano
  console.log(`\n${BOLD}UpdatePiano${RESET}`);

  await test('PATCH /api/UpdatePiano saves piano fields', async () => {
    assert(testRegId, 'no testRegId');
    const { res, data } = await patchAuth('UpdatePiano', {
      id:            testRegId,
      piano_make:    'Yamaha',
      piano_model:   'U1H',
      piano_color:   'Noir laqué poli',
      purchase_date: new Date().toISOString().split('T')[0],
    }, adminToken);
    assert(res.ok, `HTTP ${res.status}: ${JSON.stringify(data)}`);
    assert(data.success, 'success not true');
  });

  await test('PATCH /api/UpdatePiano — unknown id returns 404', async () => {
    const { res } = await patchAuth('UpdatePiano', { id: 999999, piano_make: 'X' }, adminToken);
    assert(res.status === 404, `expected 404, got ${res.status}`);
  });

  await test('PATCH /api/UpdatePiano — missing id returns 400', async () => {
    const { res } = await patchAuth('UpdatePiano', { piano_make: 'X' }, adminToken);
    assert(res.status === 400, `expected 400, got ${res.status}`);
  });

  // 9. GetAuditLog
  console.log(`\n${BOLD}GetAuditLog${RESET}`);

  await test('GET /api/GetAuditLog returns entries for test registration', async () => {
    assert(testRegId, 'no testRegId');
    const { res, data } = await get(`GetAuditLog?id=${testRegId}`);
    assert(res.ok, `HTTP ${res.status}`);
    assert(Array.isArray(data), 'response is not an array');
    assert(data.length > 0, 'no audit entries found');
    const entry = data[0];
    assert(entry.changed_by,   'changed_by missing');
    assert(entry.changed_at,   'changed_at missing');
    assert(entry.section,      'section missing');
    assert(entry.changes_json, 'changes_json missing');
    // Verify changes_json is valid JSON with old/new structure
    const changes = JSON.parse(entry.changes_json);
    const firstKey = Object.keys(changes)[0];
    assert('old' in changes[firstKey], 'changes_json missing "old" key');
    assert('new' in changes[firstKey], 'changes_json missing "new" key');
  });

  await test('GET /api/GetAuditLog — missing id returns 400', async () => {
    const { res } = await get('GetAuditLog');
    assert(res.status === 400, `expected 400, got ${res.status}`);
  });

  await test('GET /api/GetAuditLog — unknown id returns empty array', async () => {
    const { res, data } = await get('GetAuditLog?id=999999');
    assert(res.ok, `HTTP ${res.status}`);
    assert(Array.isArray(data) && data.length === 0, 'expected empty array for unknown id');
  });

  // 15. GetSalesStaff
  console.log(`\n${BOLD}GetSalesStaff${RESET}`);

  await test('GET /api/GetSalesStaff returns an array', async () => {
    const { res, data } = await get('GetSalesStaff');
    assert(res.ok, `HTTP ${res.status}`);
    assert(Array.isArray(data), 'response is not an array');
  });

  await test('GET /api/GetFormData includes salesStaff array', async () => {
    const { res, data } = await get('GetFormData?lang=fr');
    assert(res.ok, `HTTP ${res.status}`);
    assert(Array.isArray(data.salesStaff), 'salesStaff missing from GetFormData response');
  });

  // 16. AddSalesStaff
  console.log(`\n${BOLD}AddSalesStaff${RESET}`);

  await test('POST /api/AddSalesStaff — no token returns 401', async () => {
    const { res } = await post('AddSalesStaff', { name: 'Test Conseiller' });
    assert(res.status === 401, `expected 401, got ${res.status}`);
  });

  await test('POST /api/AddSalesStaff — non-admin token returns 401', async () => {
    assert(staffToken, 'no staffToken');
    const { res } = await postXToken('AddSalesStaff', { name: 'Test Conseiller' }, staffToken);
    assert(res.status === 401, `expected 401, got ${res.status}`);
  });

  await test('POST /api/AddSalesStaff — missing name returns 400', async () => {
    assert(adminToken, 'no adminToken');
    const { res } = await postXToken('AddSalesStaff', {}, adminToken);
    assert(res.status === 400, `expected 400, got ${res.status}`);
  });

  await test('POST /api/AddSalesStaff — admin creates staff member', async () => {
    assert(adminToken, 'no adminToken');
    const { res, data } = await postXToken('AddSalesStaff', { name: 'Conseiller Test Auto' }, adminToken);
    assert(res.ok, `HTTP ${res.status}: ${JSON.stringify(data)}`);
    assert(data.success === true, 'success not true');
    assert(Number.isInteger(data.id) && data.id > 0, `id invalid: ${data.id}`);
    testSalesStaffId = data.id;
  });

  await test('GET /api/GetSalesStaff includes newly created staff member', async () => {
    assert(testSalesStaffId, 'no testSalesStaffId');
    const { res, data } = await get('GetSalesStaff');
    assert(res.ok, `HTTP ${res.status}`);
    const found = data.find(ss => ss.id === testSalesStaffId);
    assert(found, `testSalesStaffId ${testSalesStaffId} not found in list`);
    assert(found.name === 'Conseiller Test Auto', `name mismatch: ${found.name}`);
  });

  await test('PATCH /api/AddSalesStaff — admin can toggle is_active', async () => {
    assert(adminToken && testSalesStaffId, 'missing adminToken or testSalesStaffId');
    const { res, data } = await patchXToken('AddSalesStaff', { id: testSalesStaffId, is_active: false }, adminToken);
    assert(res.ok, `HTTP ${res.status}: ${JSON.stringify(data)}`);
    assert(data.success === true, 'success not true');
  });

  await test('GET /api/GetSalesStaff excludes deactivated staff member', async () => {
    assert(testSalesStaffId, 'no testSalesStaffId');
    const { res, data } = await get('GetSalesStaff');
    assert(res.ok, `HTTP ${res.status}`);
    const found = data.find(ss => ss.id === testSalesStaffId);
    assert(!found, `deactivated staff id ${testSalesStaffId} should not appear in active list`);
  });

  await test('PATCH /api/AddSalesStaff — re-activate for remaining tests', async () => {
    assert(adminToken && testSalesStaffId, 'missing adminToken or testSalesStaffId');
    const { res, data } = await patchXToken('AddSalesStaff', { id: testSalesStaffId, is_active: true }, adminToken);
    assert(res.ok, `HTTP ${res.status}: ${JSON.stringify(data)}`);
  });

  // 17. Sales staff on registrations
  console.log(`\n${BOLD}Sales staff on registrations${RESET}`);

  await test('PATCH /api/UpdateClientInfo assigns sales staff to registration', async () => {
    assert(testRegId && testSalesStaffId && adminToken, 'missing testRegId, testSalesStaffId, or adminToken');
    const { res, data } = await patchAuth('UpdateClientInfo', {
      id: testRegId,
      sales_staff_ids:   [testSalesStaffId],
      sales_staff_other: null,
    }, adminToken);
    assert(res.ok, `HTTP ${res.status}: ${JSON.stringify(data)}`);
    assert(data.success === true, 'success not true');
  });

  await test('GET /api/GetRegistrations includes sales_staff_names for assigned registration', async () => {
    assert(testRegId && testSalesStaffId, 'missing testRegId or testSalesStaffId');
    const { res, data } = await get('GetRegistrations');
    assert(res.ok, `HTTP ${res.status}`);
    const row = data.find(r => r.id === testRegId);
    assert(row, `testRegId ${testRegId} not found`);
    assert('sales_staff_names' in row, 'sales_staff_names field missing from row');
    assert('sales_staff_ids' in row,   'sales_staff_ids field missing from row');
    assert(row.sales_staff_names && row.sales_staff_names.includes('Conseiller Test Auto'),
      `expected "Conseiller Test Auto" in sales_staff_names, got: ${row.sales_staff_names}`);
  });

  await test('PATCH /api/UpdateClientInfo can set sales_staff_other text', async () => {
    assert(testRegId && adminToken, 'missing testRegId or adminToken');
    const { res, data } = await patchAuth('UpdateClientInfo', {
      id: testRegId,
      sales_staff_other: 'Marie Dupont',
    }, adminToken);
    assert(res.ok, `HTTP ${res.status}: ${JSON.stringify(data)}`);
    assert(data.success === true, 'success not true');
  });

  await test('GET /api/GetRegistrations includes sales_staff_other for updated registration', async () => {
    assert(testRegId, 'no testRegId');
    const { res, data } = await get('GetRegistrations');
    assert(res.ok, `HTTP ${res.status}`);
    const row = data.find(r => r.id === testRegId);
    assert(row, `testRegId ${testRegId} not found`);
    assert(row.sales_staff_other === 'Marie Dupont',
      `expected sales_staff_other "Marie Dupont", got: ${row.sales_staff_other}`);
  });

  await test('GET /api/GetAuditLog includes sales_staff entry after UpdateClientInfo', async () => {
    assert(testRegId, 'no testRegId');
    const { res, data } = await get(`GetAuditLog?id=${testRegId}`);
    assert(res.ok, `HTTP ${res.status}`);
    const clientEntries = data.filter(e => e.section === 'client');
    assert(clientEntries.length > 0, 'no client audit entries');
    const hasSalesStaff = clientEntries.some(e => {
      try {
        const c = JSON.parse(e.changes_json);
        return 'sales_staff' in c || 'sales_staff_other' in c;
      } catch { return false; }
    });
    assert(hasSalesStaff, 'no sales_staff or sales_staff_other key found in audit log changes_json');
  });

  await test('cleanup — deactivate test sales staff member', async () => {
    assert(adminToken && testSalesStaffId, 'missing adminToken or testSalesStaffId');
    const { res, data } = await patchXToken('AddSalesStaff', { id: testSalesStaffId, is_active: false }, adminToken);
    assert(res.ok, `HTTP ${res.status}: ${JSON.stringify(data)}`);
  });

  // ── Summary ───────────────────────────────────────────────
  const total = passed + failed;
  console.log(`\n${'─'.repeat(40)}`);
  console.log(`${BOLD}Results: ${passed}/${total} passed${RESET}`);
  if (failed > 0) {
    console.log(`${RED}${failed} test${failed > 1 ? 's' : ''} failed${RESET}`);
    process.exit(1);
  } else {
    console.log(`${GREEN}All tests passed ✓${RESET}`);
  }
  console.log();

})();
