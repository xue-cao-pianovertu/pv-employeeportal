// ─────────────────────────────────────────────
// progress.js — scroll-based progress bar
// ─────────────────────────────────────────────

const SEC_NAMES = {
  fr: ["Renseignements client", "Info livraison", "Détails du piano", "Avis d'humidité", "Garantie & Consentement"],
  en: ["Customer Info", "Delivery Info", "Piano Details", "Humidity Notice", "Warranty & Consent"],
  zh: ["客户信息", "送货信息", "钢琴详情", "湿度须知", "保修与同意"],
};

let _getLang;

export function initProgress({ getLang }) {
  _getLang = getLang;
  window.addEventListener('scroll', updateProgress);
  updateProgress();
}

export function updateProgress() {
  const lang = _getLang();
  let active = 0;

  ['c1', 'c2', 'c3', 'c4', 'c5'].forEach((id, i) => {
    const el = document.getElementById(id);
    if (!el) return;
    const r = el.getBoundingClientRect();
    const seg = document.getElementById('ps' + (i + 1));
    if (!seg) return;
    if (r.bottom < 0) seg.className = 'pseg done';
    else if (r.top < window.innerHeight * 0.5) { seg.className = 'pseg active'; active = i; }
    else seg.className = 'pseg';
  });

  const nm = SEC_NAMES[lang] || SEC_NAMES.fr;
  const stepLabel = lang === 'zh' ? '第' : lang === 'en' ? 'Step' : 'Étape';
  document.getElementById('progStep').textContent = `${stepLabel} ${active + 1} / 5`;
  document.getElementById('progName').textContent = nm[active];
}
