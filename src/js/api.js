// ─────────────────────────────────────────────
// api.js — all Azure Function fetch calls
// ─────────────────────────────────────────────

const API_BASE = window.location.hostname === 'localhost'
  ? 'http://localhost:7072'
  : '';

export function apiUrl(endpoint) {
  return `${API_BASE}/api/${endpoint}`;
}

/**
 * Fetch all form bootstrap data: categories, pianoTypes, benches, PDFs
 * @param {string} lang - 'fr' | 'en' | 'zh'
 */
export async function getFormData(lang = 'fr') {
  const res = await fetch(apiUrl(`GetFormData?lang=${lang}`));
  if (!res.ok) throw new Error(`GetFormData failed: ${res.status}`);
  return res.json();
}

/**
 * Submit customer registration to DB
 * @param {object} payload
 */
export async function submitRegistration(payload) {
  const res = await fetch(apiUrl('SubmitRegistration'), {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  });
  if (!res.ok) throw new Error(`SubmitRegistration failed: ${res.status}`);
  return res.json();
}
