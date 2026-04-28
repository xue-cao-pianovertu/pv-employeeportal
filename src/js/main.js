// ─────────────────────────────────────────────
// main.js — init, orchestrates all modules
// ─────────────────────────────────────────────

import { getFormData, submitRegistration } from './api.js';
import { L, applyLang } from './lang.js';
import { initPdf, openPdf, markRead, updatePdfGate, getPdfRead, getTradeupRead, initTradeupPdf, openTradeupPdf  } from './pdf.js';
import { initSignature, clearSig, hasSig, getSignatureDataUrl } from './signature.js';
import {
  initForm, initCheckboxes, initRadios,
  populateCategories,
  onCatChange, onTypeChange,
  step, calcSurcharge, revealEl, cnts, getIsConsignment
} from './form.js';
import { initProgress, updateProgress } from './progress.js';

// ── State ─────────────────────────────────────
let lang = 'fr';
let formData = null;

const getLang = () => lang;
const getL = (l) => L[l] || L.fr;
const getFormDataFn = () => formData;

// ── Today ─────────────────────────────────────
const today = new Date();
const todayISO = today.toISOString().split('T')[0];

// ── Init ──────────────────────────────────────
document.addEventListener('DOMContentLoaded', async () => {

  // Wire modules
  initPdf({ getLang, getL });
  initSignature();
  initForm({ getFormData: getFormDataFn, getLang, getL });
  initProgress({ getLang });

  // Dates
  document.getElementById('purchaseDate').value = todayISO;
  const tmr = new Date(today);
  tmr.setDate(tmr.getDate() + 1);
  document.getElementById('deliveryDate').min = tmr.toISOString().split('T')[0];
  document.getElementById('dateDisp').textContent = today.toLocaleDateString(
    'fr-CA', { year: 'numeric', month: 'long', day: 'numeric' }
  );

  // QR params
  const qp = new URLSearchParams(location.search);
  let qrOn = false;
  [['make', 'make'], ['model', 'model'], ['serial', 'serial'], ['color', 'color']].forEach(([p, id]) => {
    const v = qp.get(p);
    if (v) { const el = document.getElementById(id); if (el) el.value = decodeURIComponent(v); qrOn = true; }
  });
  if (qrOn) document.getElementById('qrBanner').style.display = 'flex';

  // Load form data from API
  await loadFormData();

  // Init interactivity
  initCheckboxes();
  initRadios();

  // Event: category change
  document.getElementById('pianoCategory').addEventListener('change', function () {
    onCatChange(this.value);
  });

  // Event: type change
  document.getElementById('pianoType').addEventListener('change', function () {
    onTypeChange(this.value);
  });

  // Event: surcharge-related radios
  document.querySelectorAll('input[name=elevator], input[name=crane], input[name=collect], input[name=recycle]')
    .forEach(el => el.addEventListener('change', calcSurcharge));

  // Event: collect/recycle reveal
  document.querySelector('input[name=collect][value=yes]')
    ?.addEventListener('change', () => { revealEl('collectDetail', true); calcSurcharge(); });
  document.querySelector('input[name=collect][value=no]')
    ?.addEventListener('change', () => { revealEl('collectDetail', false); calcSurcharge(); });
  document.querySelector('input[name=recycle][value=yes]')
    ?.addEventListener('change', () => { revealEl('recycleDetail', true); calcSurcharge(); });
  document.querySelector('input[name=recycle][value=no]')
    ?.addEventListener('change', () => { revealEl('recycleDetail', false); calcSurcharge(); });

  // Apply initial language
  applyLang(lang, { today, pdfRead: getPdfRead(), onCatChange, isConsignment: getIsConsignment() });
});

// ── Load form data ────────────────────────────
async function loadFormData() {
  const loadingEl = document.getElementById('formLoadingMsg');
  try {
    formData = await getFormData(lang);
    populateCategories();
    if (formData.pdfs.tradeup) initTradeupPdf(formData.pdfs.tradeup);
    if (loadingEl) loadingEl.style.display = 'none';
  } catch (err) {
    console.error('Failed to load form data:', err);
    if (loadingEl) {
      loadingEl.textContent = L[lang].eApiLoad;
      loadingEl.style.color = '#b91c1c';
    }
  }
}

// ── setLang (called by language buttons) ─────
window.setLang = async function (l) {
  lang = l;

  // Re-fetch API data in new language (for correct PDF SAS URLs)
  try {
    formData = await getFormData(lang);
    if (formData.pdfs.tradeup) initTradeupPdf(formData.pdfs.tradeup);
  } catch (err) {
    console.error('Failed to reload form data for lang:', lang, err);
  }

  // Re-populate dropdowns in new language
  const savedCatId = document.getElementById('pianoCategory').value;
  const savedTypeId = document.getElementById('pianoType').value;

  populateCategories();

  // Restore saved selections
  if (savedCatId) {
    document.getElementById('pianoCategory').value = savedCatId;
    onCatChange(savedCatId);
    if (savedTypeId) {
      document.getElementById('pianoType').value = savedTypeId;
      onTypeChange(savedTypeId);
    }
  }

  // If PDF already read, keep it read — don't reset gate
  applyLang(lang, { today, pdfRead: getPdfRead(), onCatChange, isConsignment: getIsConsignment() });
  updateProgress();
};

// ── Expose to HTML onclick ────────────────────
window.openPdf = openPdf;
window.openTradeupPdf = openTradeupPdf;
window.markRead = markRead;
window.clearSig = clearSig;
window.step = step;
window.calcSurcharge = calcSurcharge;
window.revealEl = revealEl;

// ── Helpers ───────────────────────────────────
const g = id => document.getElementById(id)?.value?.trim() || '';
const rb = name => document.querySelector(`input[name=${name}]:checked`)?.value || '';

function accList() {
  const items = [];
  if (document.getElementById('acc-assembly').checked) items.push('Assemblage');
  if (document.getElementById('acc-bench').checked) items.push('Banc');
  if (document.getElementById('acc-dampp').checked) items.push('Dampp-Chaser');
  if (document.getElementById('acc-adapter').checked) items.push('Adaptateur élec.');
  if (document.getElementById('acc-headphones').checked) items.push('Écouteurs');
  if (document.getElementById('acc-casters').checked) items.push('Sous-pattes');
  if (document.getElementById('acc-cover').checked) items.push('Housse');
  return items.join(', ') || 'Aucun';
}

const yn = v => v ? 'Oui' : 'Non';

function showErr(msg) {
  const el = document.getElementById('errMsg');
  el.textContent = '⚠ ' + msg;
  el.style.display = 'block';
  el.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
}

// ── Submit ────────────────────────────────────
window.doSubmit = async function () {
  const t = L[lang];
  document.getElementById('errMsg').style.display = 'none';

  // Validation
  if (!g('lastName') || !g('firstName')) return showErr(t.eName);
  if (!g('email') || !g('email').includes('@')) return showErr(t.eEmail);
  if (!g('street')) return showErr(t.eStreet);
  const manualEntry = document.getElementById('makeEditRow').style.display !== 'none';
  if (manualEntry ? !g('makeEdit') : !g('make')) return showErr(t.eMake);
  if (!document.getElementById('asapCheck').checked && !g('deliveryDate')) return showErr(t.eDelDate);
  const humidityVisible = document.getElementById('c4').style.display !== 'none';
  if (humidityVisible && !document.getElementById('humCheck').checked) return showErr(t.eHum);
  const warrantyVisible = document.getElementById('pdfGate').style.display !== 'none';
  const tradeupVisible  = document.getElementById('tradeupGate').style.display !== 'none';
  if (warrantyVisible && !getPdfRead()) return showErr(t.eNotRead);
  if (tradeupVisible  && !getTradeupRead()) return showErr(t.eNotRead);
  if (!document.getElementById('agreeCheck').checked) return showErr(t.eAgree);
  if (!hasSig) return showErr(t.eDraw);

  const btn = document.getElementById('submitBtn');
  btn.disabled = true;
  btn.innerHTML = `<span class="spinner"></span>${t.submitting}`;

  const surcharge = document.getElementById('surchargeWarn').classList.contains('on');

  const payload = {
    language: lang.toUpperCase(),
    customer_last_name: g('lastName'),
    customer_first_name: g('firstName'),
    customer_email: g('email'),
    customer_phone1: g('phone1') || '—',
    customer_phone2: g('phone2') || '—',
    heard_from:           g('heardFrom') || '—',
    referred_by_teacher:  g('referredByTeacher') || null,
    delivery_street: g('street'),
    delivery_apt: g('apt') || '',
    delivery_city: g('city'),
    delivery_province: g('province'),
    delivery_postal: g('postal'),
    within_40km: document.getElementById('km40Check').checked,
    delivery_floor: g('floorText') || '—',
    delivery_elevator: rb('elevator') === 'yes',
    steps_outside: cnts.sout,
    steps_inside: cnts.sin,
    stair_turns: cnts.turns,
    mover_notes: g('moverNotes') || '—',
    collect_piano: rb('collect') === 'yes',
    collect_desc: g('collectDesc') || '—',
    recycle_piano: rb('recycle') === 'yes',
    recycle_desc: g('recycleDesc') || '—',
    crane_required: rb('crane') === 'yes',
    delivery_asap: document.getElementById('asapCheck').checked,
    delivery_date: g('deliveryDate') || 'ASAP',
    delivery_notes: g('deliveryNotes') || '—',
    surcharge_flag: surcharge,
    piano_category_id: parseInt(document.getElementById('pianoCategory').value) || null,
    piano_type_id: parseInt(document.getElementById('pianoType').value) || null,
    piano_make: g('make') || g('makeEdit') || '—',
    piano_model: g('model') || '—',
    piano_serial: g('serial'),
    piano_color: g('color') || '—',
    purchase_date: todayISO,
    accessories: accList(),
    piano_notes: g('pianoNotes') || '—',
    humidity_confirmed: true,
    signature_type: 'drawn',
    signature_data: getSignatureDataUrl() || '',
  };

  try {
    const result = await submitRegistration(payload);

    // Show success
    document.getElementById('docRef').textContent = `Réf : ${result.ref_id}`;
    if (result.new_account) {
      document.getElementById('newAccUser').textContent = result.client_username;
      document.getElementById('newAccPass').textContent = result.client_password;
      document.getElementById('newAccountBox').style.display = 'block';
    }
    document.getElementById('mainForm').style.display = 'none';
    document.getElementById('successScreen').style.display = 'block';
    window.scrollTo({ top: 0, behavior: 'smooth' });

  } catch (err) {
    console.error('Submit failed:', err);
    btn.disabled = false;
    btn.textContent = t.submitBtn;
    showErr('Erreur lors de la soumission. Veuillez réessayer.');
  }
};
