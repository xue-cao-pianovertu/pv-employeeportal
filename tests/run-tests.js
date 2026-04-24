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
let testEmail, testPassword, testRegId, testRefId, adminToken, staffToken;

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
    steps_outside: 2, steps_inside: 1, stair_turns: 0,
    collect_piano: false, recycle_piano: false, crane_required: false,
    delivery_asap: true,
    surcharge_flag: false,
    piano_category_id: 1,
    piano_make:  'Yamaha',
    piano_model: 'U1',
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
    assert(data.client_password && data.client_password.endsWith('_pianovertu'), 'client_password invalid');
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
  });

  await test('POST /api/Login — staff credentials return token', async () => {
    const { res, data } = await post('Login', { username: 'staff@pianovertu.com', password: 'changeme' });
    assert(res.ok, `HTTP ${res.status}: ${JSON.stringify(data)}`);
    assert(data.token,     'token missing');
    assert(data.role,      'role missing');
    assert(data.full_name, 'full_name missing');
    assert(data.role === 'staff', `role should be staff, got ${data.role}`);
    staffToken = data.token;
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
    assert('payment_status'  in row, 'payment_status field missing');
    assert('delivery_status' in row, 'delivery_status field missing');
    assert('price'           in row, 'price field missing');
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
      payment_status:          'fully_paid',
      delivery_status:         'delivered',
      staff_notes:             'Test automatisé',
      tuning_sessions_agreed:  2,
      bench_model_id:          null,
    }, adminToken);
    assert(res.ok, `HTTP ${res.status}: ${JSON.stringify(data)}`);
    assert(data.success, 'success not true');
  });

  await test('PATCH /api/UpdateRegistration — unknown id returns 404', async () => {
    const { res } = await patch('UpdateRegistration', {
      id: 999999, surcharge_amount: 0, cheque_to_collect: false,
      google_review: false, fully_paid: false,
      payment_status: 'not_paid', delivery_status: 'to_plan',
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

  // 10. GetMyRegistrations
  console.log(`\n${BOLD}GetMyRegistrations${RESET}`);

  let customerToken;

  await test('POST /api/Login — re-login as customer to get token', async () => {
    const { res, data } = await post('Login', { username: testEmail, password: testPassword });
    assert(res.ok, `HTTP ${res.status}: ${JSON.stringify(data)}`);
    assert(data.token, 'token missing');
    assert(data.role === 'customer', `role should be customer, got ${data.role}`);
    customerToken = data.token;
  });

  await test('GET /api/GetMyRegistrations returns registrations for customer', async () => {
    assert(customerToken, 'no customerToken');
    const res  = await fetch(`${BASE}/api/GetMyRegistrations`, {
      headers: { 'Authorization': `Bearer ${customerToken}` },
    });
    const data = await res.json();
    assert(res.ok, `HTTP ${res.status}: ${JSON.stringify(data)}`);
    assert(Array.isArray(data), 'response is not an array');
    assert(data.length > 0, 'no registrations found for customer');
    const row = data[0];
    assert(row.ref_id,        'ref_id missing');
    assert(row.created_at,    'created_at missing');
    assert(!('price'        in row), 'staff field price should not be present');
    assert(!('invoice_number' in row), 'staff field invoice_number should not be present');
    assert(!('staff_notes'  in row), 'staff field staff_notes should not be present');
  });

  await test('GET /api/GetMyRegistrations — no token returns 401', async () => {
    const res = await fetch(`${BASE}/api/GetMyRegistrations`);
    assert(res.status === 401, `expected 401, got ${res.status}`);
  });

  await test('GET /api/GetMyRegistrations — invalid token returns 401', async () => {
    const res = await fetch(`${BASE}/api/GetMyRegistrations`, {
      headers: { 'Authorization': 'Bearer invalid.token.here' },
    });
    assert(res.status === 401, `expected 401, got ${res.status}`);
  });

  await test('GET /api/GetMyRegistrations — admin token returns 403 (customer only)', async () => {
    assert(adminToken, 'no adminToken');
    const res = await fetch(`${BASE}/api/GetMyRegistrations`, {
      headers: { 'Authorization': `Bearer ${adminToken}` },
    });
    assert(res.status === 403, `expected 403, got ${res.status}`);
  });

  await test('GET /api/GetMyRegistrations — staff token returns 403 (customer only)', async () => {
    assert(staffToken, 'no staffToken');
    const res = await fetch(`${BASE}/api/GetMyRegistrations`, {
      headers: { 'Authorization': `Bearer ${staffToken}` },
    });
    assert(res.status === 403, `expected 403, got ${res.status}`);
  });

  // 11. Staff role access
  console.log(`\n${BOLD}Staff role access${RESET}`);

  await test('staff token can call UpdateRegistration', async () => {
    assert(testRegId && staffToken, 'missing testRegId or staffToken');
    const { res, data } = await patchAuth('UpdateRegistration', {
      id: testRegId, surcharge_amount: 0, cheque_to_collect: false,
      google_review: false, fully_paid: false,
      payment_status: 'not_paid', delivery_status: 'to_plan',
      staff_notes: 'Test staff role', tuning_sessions_agreed: 0, bench_model_id: null,
    }, staffToken);
    assert(res.ok, `HTTP ${res.status}: ${JSON.stringify(data)}`);
    assert(data.success, 'success not true');
  });

  await test('staff token can call ResetClientPassword', async () => {
    assert(staffToken && testEmail, 'missing staffToken or testEmail');
    const { res, data } = await patchAuth('ResetClientPassword', { customer_email: testEmail }, staffToken);
    assert(res.ok, `HTTP ${res.status}: ${JSON.stringify(data)}`);
    assert(data.success === true, 'success not true');
    assert(typeof data.new_password === 'string' && data.new_password.length === 8, 'new_password invalid');
    // Reset back so suite stays idempotent
    const { data: ld } = await post('Login', { username: testEmail, password: data.new_password });
    await patchXToken('ChangePassword', { current_password: data.new_password, new_password: testPassword }, ld.token);
  });

  // 12. GetFormData — has_tradeup (requires DB migration to be run first)
  console.log(`\n${BOLD}GetFormData — has_tradeup${RESET}`);

  await test('GET /api/GetFormData categories include has_tradeup field', async () => {
    const { res, data } = await get('GetFormData?lang=fr');
    assert(res.ok, `HTTP ${res.status}`);
    assert(data.categories.every(c => 'has_tradeup' in c), 'has_tradeup field missing from categories');
    const cat1 = data.categories.find(c => c.id === 1);
    const cat3 = data.categories.find(c => c.id === 3);
    assert(cat1 && cat1.has_tradeup === true, 'category id=1 should have has_tradeup=true');
    assert(cat3 && cat3.has_tradeup === true, 'category id=3 should have has_tradeup=true');
  });

  // 13. ChangePassword
  console.log(`\n${BOLD}ChangePassword${RESET}`);

  const newPassword = testPassword + 'X';

  await test('PATCH /api/ChangePassword — no token returns 401', async () => {
    const { res } = await patch('ChangePassword', { current_password: 'x', new_password: 'y' });
    assert(res.status === 401, `expected 401, got ${res.status}`);
  });

  await test('PATCH /api/ChangePassword — missing fields returns 400', async () => {
    assert(customerToken, 'no customerToken');
    const { res } = await patchXToken('ChangePassword', {}, customerToken);
    assert(res.status === 400, `expected 400, got ${res.status}`);
  });

  await test('PATCH /api/ChangePassword — wrong current password returns 403', async () => {
    assert(customerToken, 'no customerToken');
    const { res } = await patchXToken('ChangePassword', { current_password: 'wrongpwd', new_password: 'newpwd123' }, customerToken);
    assert(res.status === 403, `expected 403, got ${res.status}`);
  });

  await test('PATCH /api/ChangePassword — correct change returns success', async () => {
    assert(customerToken, 'no customerToken');
    const { res, data } = await patchXToken('ChangePassword', { current_password: testPassword, new_password: newPassword }, customerToken);
    assert(res.ok, `HTTP ${res.status}: ${JSON.stringify(data)}`);
    assert(data.success === true, 'success not true');
  });

  await test('Login with new password works after ChangePassword', async () => {
    const { res, data } = await post('Login', { username: testEmail, password: newPassword });
    assert(res.ok, `HTTP ${res.status}: ${JSON.stringify(data)}`);
    assert(data.token, 'token missing');
    customerToken = data.token;
    // Reset back to original so suite is idempotent
    await patchXToken('ChangePassword', { current_password: newPassword, new_password: testPassword }, customerToken);
  });

  // 14. ResetClientPassword
  console.log(`\n${BOLD}ResetClientPassword${RESET}`);

  await test('PATCH /api/ResetClientPassword — no token returns 401', async () => {
    const { res } = await patch('ResetClientPassword', { customer_email: testEmail });
    assert(res.status === 401, `expected 401, got ${res.status}`);
  });

  await test('PATCH /api/ResetClientPassword — customer token returns 403', async () => {
    assert(customerToken, 'no customerToken');
    const { res } = await patchAuth('ResetClientPassword', { customer_email: testEmail }, customerToken);
    assert(res.status === 403, `expected 403, got ${res.status}`);
  });

  await test('PATCH /api/ResetClientPassword — unknown email returns 404', async () => {
    const { res } = await patchAuth('ResetClientPassword', { customer_email: 'nobody@nobody.invalid' }, adminToken);
    assert(res.status === 404, `expected 404, got ${res.status}`);
  });

  await test('PATCH /api/ResetClientPassword — admin generates new 8-char password', async () => {
    const { res, data } = await patchAuth('ResetClientPassword', { customer_email: testEmail }, adminToken);
    assert(res.ok, `HTTP ${res.status}: ${JSON.stringify(data)}`);
    assert(data.success === true, 'success not true');
    assert(typeof data.new_password === 'string' && data.new_password.length === 8,
      `new_password should be 8 chars, got: ${JSON.stringify(data.new_password)}`);
    // Verify new password works
    const { res: lr, data: ld } = await post('Login', { username: testEmail, password: data.new_password });
    assert(lr.ok, `Login with reset password failed: HTTP ${lr.status}`);
    // Reset back to original so suite is idempotent
    await patchXToken('ChangePassword', { current_password: data.new_password, new_password: testPassword }, ld.token);
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
