// ─────────────────────────────────────────────
// signature.js — canvas signature + typed mode
// ─────────────────────────────────────────────

export let hasSig = false;
export let typeMode = false;

let cv, ctx;

export function initSignature() {
  cv = document.getElementById('sigCanvas');
  ctx = cv.getContext('2d');
  resizeCv();

  let resizeTimer;
  window.addEventListener('resize', () => {
    clearTimeout(resizeTimer);
    resizeTimer = setTimeout(resizeCv, 200);
  });

  // Mouse
  cv.addEventListener('mousedown', e => {
    const p = gp(e);
    ctx.beginPath();
    ctx.moveTo(p.x, p.y);
    cv._drawing = true;
  });
  cv.addEventListener('mousemove', e => {
    if (!cv._drawing) return;
    hasSig = true;
    const p = gp(e);
    ctx.lineTo(p.x, p.y);
    ctx.stroke();
  });
  cv.addEventListener('mouseup', () => cv._drawing = false);
  cv.addEventListener('mouseleave', () => cv._drawing = false);

  // Touch
  cv.addEventListener('touchstart', e => {
    e.preventDefault();
    cv._drawing = true;
    const p = gp(e);
    ctx.beginPath();
    ctx.moveTo(p.x, p.y);
  }, { passive: false });
  cv.addEventListener('touchmove', e => {
    e.preventDefault();
    if (!cv._drawing) return;
    hasSig = true;
    const p = gp(e);
    ctx.lineTo(p.x, p.y);
    ctx.stroke();
  }, { passive: false });
  cv.addEventListener('touchend', () => cv._drawing = false);
}

function resizeCv() {
  const w = cv.offsetWidth, h = 140;
  const img = hasSig ? cv.toDataURL() : null;
  cv.width = w * devicePixelRatio;
  cv.height = h * devicePixelRatio;
  ctx.scale(devicePixelRatio, devicePixelRatio);
  cv.style.width = w + 'px';
  cv.style.height = h + 'px';
  ctx.strokeStyle = '#0d1f3c';
  ctx.lineWidth = 2.5;
  ctx.lineCap = 'round';
  ctx.lineJoin = 'round';
  if (img) {
    const i = new Image();
    i.onload = () => ctx.drawImage(i, 0, 0, w, 140);
    i.src = img;
  }
}

function gp(e) {
  const r = cv.getBoundingClientRect();
  const s = e.touches ? e.touches[0] : e;
  return { x: s.clientX - r.left, y: s.clientY - r.top };
}

export function clearSig() {
  ctx.clearRect(0, 0, cv.width, cv.height);
  hasSig = false;
}

export function getSignatureDataUrl() {
  return hasSig ? cv.toDataURL() : null;
}

export function toggleType(getLang, getL) {
  typeMode = !typeMode;
  document.getElementById('typedNameRow').style.display = typeMode ? 'block' : 'none';
  document.querySelector('.sig-wrap').style.display = typeMode ? 'none' : 'block';
  const t = getL(getLang());
  document.getElementById('typeToggle').textContent = typeMode ? t.drawMode : t.typeMode;
}
