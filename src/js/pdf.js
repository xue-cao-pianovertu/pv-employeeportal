// ─────────────────────────────────────────────
// pdf.js — PDF gate logic
// ─────────────────────────────────────────────

let _pdfRead = false;
let _pdfOpened = false;

export const getPdfRead = () => _pdfRead;

let _getLang;     // injected from main
let _getL;        // injected from main

export function initPdf({ getLang, getL }) {
  _getLang = getLang;
  _getL = getL;
}

export function resetPdf() {
  _pdfRead = false;
  _pdfOpened = false;
}

/**
 * Called when user clicks "Open document"
 * Starts a 20s progress timer then marks as read
 */
export function openPdf() {
  if (_pdfOpened) return;
  _pdfOpened = true;

  document.getElementById('pdfOverlay').classList.add('gone');

  const fill = document.getElementById('pdfFill');
  let elapsed = 0;
  const REQ = 20000;
  const t = setInterval(() => {
    elapsed += 200;
    fill.style.width = Math.min((elapsed / REQ) * 100, 100) + '%';
    if (elapsed >= REQ) { clearInterval(t); markRead(); }
  }, 200);
}

export function markRead() {
  if (_pdfRead) return;
  _pdfRead = true;

  const lang = _getLang();
  const t = _getL(lang);

  document.getElementById('pdfFill').style.cssText = 'width:100%;background:#4ade80';
  const b = document.getElementById('pdfBadge');
  b.textContent = '✓ ' + t.pdfDone;
  b.classList.add('read');

  const statusEl = document.getElementById('pdfStatus');
  statusEl.classList.add('done');
  statusEl.innerHTML = `<svg width="13" height="13" fill="none" stroke="currentColor" stroke-width="2.5" viewBox="0 0 24 24">
    <polyline points="20 6 9 17 4 12"/></svg> <span>${t.pdfReadDone}</span>`;

  document.getElementById('sigLock').classList.add('open');

  setTimeout(() => {
    document.getElementById('sigLock')
      .closest('.sig-lock')
      ?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }, 400);
}

/**
 * Switch the PDF shown in the gate.
 * pdfUrl — SAS URL from formData.pdfs
 * pdfLabel — filename label to display
 */
export function updatePdfGate(pdfUrl, pdfLabel) {
  if (!pdfUrl) return;

  // Reset read state — customer must re-read new PDF
  _pdfRead = false;
  _pdfOpened = false;

  const lang = _getLang();
  const t = _getL(lang);

  const frame = document.getElementById('pdfFrame');      // ← add this back
  const nameEl = document.getElementById('pdfName');
  const overlay = document.getElementById('pdfOverlay');
  const fill = document.getElementById('pdfFill');
  const badge = document.getElementById('pdfBadge');
  const status = document.getElementById('pdfStatus');
  const lock = document.getElementById('sigLock');

  frame.src = pdfUrl;


  frame.src = pdfUrl;
  if (nameEl && pdfLabel) nameEl.textContent = pdfLabel;
  overlay.classList.remove('gone');
  fill.style.width = '0%';
  fill.style.background = '';
  badge.textContent = t.pdfBadge;
  badge.classList.remove('read');
  status.classList.remove('done');
  status.innerHTML = `<svg width="12" height="12" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
    <circle cx="12" cy="12" r="10"/><path d="M12 8v4M12 16h.01"/></svg>
    <span>${t.pdfPending}</span>`;
  lock.classList.remove('open');
}



// ── TradeUp PDF (separate gate, separate read state) ──
let _tradeupRead = false;
let _tradeupOpened = false;

export const getTradeupRead = () => _tradeupRead;

export function initTradeupPdf(url) {
  if (!url) return;
  document.getElementById('tradeupFrame').src = url;
}

export function openTradeupPdf() {
  if (_tradeupOpened) return;
  _tradeupOpened = true;

  document.getElementById('tradeupOverlay').classList.add('gone');

  const fill = document.getElementById('tradeupFill');
  let elapsed = 0;
  const REQ = 20000;
  const t = setInterval(() => {
    elapsed += 200;
    fill.style.width = Math.min((elapsed / REQ) * 100, 100) + '%';
    if (elapsed >= REQ) { clearInterval(t); markTradeupRead(); }
  }, 200);
}

function markTradeupRead() {
  if (_tradeupRead) return;
  _tradeupRead = true;

  const lang = _getLang();
  const t = _getL(lang);

  document.getElementById('tradeupFill').style.cssText = 'width:100%;background:#4ade80';
  const b = document.getElementById('tradeupBadge');
  b.textContent = '✓ ' + t.pdfDone;
  b.classList.add('read');

  const status = document.getElementById('tradeupStatus');
  status.classList.add('done');
  status.innerHTML = `<svg width="13" height="13" fill="none" stroke="currentColor" stroke-width="2.5" viewBox="0 0 24 24">
    <polyline points="20 6 9 17 4 12"/></svg> <span>${t.pdfReadDone}</span>`;
}