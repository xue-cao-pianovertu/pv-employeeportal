// ─────────────────────────────────────────────
// form.js — dropdowns, category/type, surcharge, steppers, checkboxes
// ─────────────────────────────────────────────

import { updatePdfGate } from './pdf.js';

// ── State ─────────────────────────────────────
export const cnts = { sout: 0, sin: 0, turns: 0 };
const STEP_T = 10;

// Injected from main
let _formData = null;
let _getLang;
let _getL;

export function initForm({ getFormData, getLang, getL }) {
  _formData = getFormData;
  _getLang = getLang;
  _getL = getL;
}

// ── Populate dropdowns from formData ─────────────────────────────

export function populateCategories() {
  const sel = document.getElementById('pianoCategory');
  const lang = _getLang();
  const t = _getL(lang);
  const data = _formData();

  sel.innerHTML = `<option value="" id="opt-cat-ph">${t.sel}</option>`;
  data.categories.forEach(cat => {
    const opt = document.createElement('option');
    opt.value = cat.id;
    opt.dataset.hasWarranty = cat.has_warranty;
    opt.dataset.allowsManualEntry = cat.allows_manual_entry;
    opt.textContent = cat[`name_${lang}`];
    sel.appendChild(opt);
  });
}

export function populatePianoTypes(categoryId) {
  const sel = document.getElementById('pianoType');
  const lang = _getLang();
  const t = _getL(lang);
  const data = _formData();

  sel.innerHTML = `<option value="">${t.sel}</option>`;

  const filtered = data.pianoTypes.filter(pt => pt.category_id === parseInt(categoryId));
  filtered.forEach(pt => {
    const opt = document.createElement('option');
    opt.value = pt.id;
    opt.textContent = pt[`name_${lang}`];
    if (pt.brand_name) opt.dataset.brand = pt.brand_name;
    sel.appendChild(opt);
  });

  // Show/hide type row
  const row = document.getElementById('subtypeRow');
  if (row) row.style.display = filtered.length > 0 ? 'block' : 'none';
}

export function populateBenches() {
  const sel = document.getElementById('benchModel');
  const lang = _getLang();
  const t = _getL(lang);
  const data = _formData();

  sel.innerHTML = `<option value="" id="opt-bench-ph">${t.sel}</option>`;
  data.benches.forEach(b => {
    const opt = document.createElement('option');
    opt.value = b.id;
    opt.textContent = b.name;
    sel.appendChild(opt);
  });
  // Add "Other" at end
  const autre = document.createElement('option');
  autre.textContent = 'Autre / Other';
  sel.appendChild(autre);
}

// ── Category change ───────────────────────────

export function onCatChange(categoryId) {

  const data = _formData();
  const lang = _getLang();
  if (!data) return;
console.log('categoryId:', categoryId);
console.log('warranty keys:', Object.keys(_formData().pdfs.warranty));
  const category = data.categories.find(c => c.id === parseInt(categoryId));
  const makeEditRow = document.getElementById('makeEditRow');
  const consignNote = document.getElementById('consignNote');
  const c4 = document.getElementById('c4');
  const c5 = document.getElementById('c5');

  // Reset type dropdown and make field
  document.getElementById('pianoType').innerHTML = `<option value="">—</option>`;
  document.getElementById('subtypeRow').style.display = 'none';
  setMake('', true);
  makeEditRow.style.display = 'none';

  if (!category) return;

  const hasTypes = data.pianoTypes.some(pt => pt.category_id === parseInt(categoryId));

  if (hasTypes) {
    populatePianoTypes(categoryId);
  } else {
    // Pre-loved / consignment — manual brand entry
    makeEditRow.style.display = 'block';
    setMake('', false);
  }

  // Consignment note + hide warranty/humidity
  consignNote.style.display = !category.has_warranty ? 'block' : 'none';
  c4.style.display = category.has_warranty ? 'block' : 'none';
  c5.style.display = 'block'; // always show signature card
  // Hide just the PDF gate for consignment
  document.getElementById('pdfGate').style.display = category.has_warranty ? 'block' : 'none';

  // For categories without type subtypes but WITH warranty (e.g. preloved)
  // → show category-level PDF immediately
  if (!hasTypes && category.has_warranty) {
    //const url = data.pdfs.warranty[`cat_${categoryId}`];
    //updatePdfGate(url, `warranty-cat-${categoryId}.pdf`);
const url = data.pdfs.warranty['used_piano'];
  updatePdfGate(url, 'warranty-used.pdf');
  }
}

// ── Type change ───────────────────────────────

export function onTypeChange(pianoTypeId) {
  const data = _formData();
  if (!data) return;

  const typeOpt = document.querySelector(`#pianoType option[value="${pianoTypeId}"]`);
  const brand = typeOpt?.dataset.brand || '';
  if (brand) setMake(brand, true);

  // Update warranty PDF
  const url = data.pdfs.warranty[`new_piano_warranty_${pianoTypeId}`];
  if (url) updatePdfGate(url, `warranty-type-${pianoTypeId}.pdf`);
}

// ── Make field helper ─────────────────────────

function setMake(val, readonly) {
  const el = document.getElementById('make');
  if (!el) return;
  el.value = val;
  el.readOnly = readonly;
  el.style.color = readonly ? 'var(--muted)' : 'var(--navy)';
  el.style.cursor = readonly ? 'default' : 'text';
}

// ── Steppers ──────────────────────────────────

export function step(key, delta) {
  cnts[key] = Math.max(0, Math.min(50, cnts[key] + delta));
  document.getElementById('v-' + key).textContent = cnts[key];
  calcSurcharge();
}

// ── Surcharge ─────────────────────────────────

export function calcSurcharge() {
  const elev = document.querySelector('input[name=elevator]:checked')?.value;
  const crane = document.querySelector('input[name=crane]:checked')?.value;
  const coll = document.querySelector('input[name=collect]:checked')?.value;
  const rec = document.querySelector('input[name=recycle]:checked')?.value;
  const show =
    elev === 'no' ||
    cnts.sout > STEP_T ||
    cnts.sin > STEP_T ||
    cnts.turns >= 3 ||
    crane === 'yes' ||
    coll === 'yes' ||
    rec === 'yes';
  document.getElementById('surchargeWarn').classList.toggle('on', show);
}

// ── ASAP toggle ───────────────────────────────

export function toggleAsap(on) {
  document.getElementById('dateFromRow').style.display = on ? 'none' : 'block';
  if (on) document.getElementById('deliveryDate').value = '';
}

// ── Reveal helper ─────────────────────────────

export function revealEl(id, on) {
  document.getElementById(id)?.classList.toggle('on', on);
}

// ── Checkboxes ────────────────────────────────

export function initCheckboxes() {
  document.querySelectorAll('.cb-item, .acc-item, .agree-row').forEach(item => {
    const inp = item.querySelector('input[type=checkbox]');
    const icon = item.querySelector('.cb-icon, .acc-icon, .agree-icon');
    if (!inp || !icon) return;
    const update = () => {
      item.classList.toggle('checked', inp.checked);
      icon.textContent = inp.checked ? '✕' : '';
      if (inp.id === 'acc-bench') revealEl('benchTypeRow', inp.checked);
    };
    inp.addEventListener('change', update);
    update();
  });

  // ASAP checkbox
  const asapInp = document.getElementById('asapCheck');
  if (asapInp) asapInp.addEventListener('change', () => toggleAsap(asapInp.checked));
}

// ── Radio buttons ─────────────────────────────

export function initRadios() {
  document.querySelectorAll('.yn').forEach(group => {
    const labels = group.querySelectorAll('label');
    labels.forEach(lbl => {
      const inp = lbl.querySelector('input[type=radio]');
      if (!inp) return;
      if (!lbl.querySelector('.yn-icon')) {
        const icon = document.createElement('span');
        icon.className = 'yn-icon';
        icon.textContent = '';
        lbl.insertBefore(icon, lbl.firstChild);
      }
      inp.addEventListener('change', () => {
        labels.forEach(l => {
          const li = l.querySelector('.yn-icon');
          const checked = l.querySelector('input').checked;
          l.classList.toggle('selected', checked);
          if (li) li.textContent = checked ? '✕' : '';
        });
      });
    });
    // Set initial state
    const checked = group.querySelector('input:checked');
    if (checked) {
      const lbl = checked.closest('label');
      lbl.classList.add('selected');
      const icon = lbl.querySelector('.yn-icon');
      if (icon) icon.textContent = '✕';
    }
  });
}
