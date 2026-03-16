// ─────────────────────────────────────────────
// lang.js — translations + setLang()
// ─────────────────────────────────────────────

export const L = {
  fr: {
    logoSub: 'Spécialistes du Piano',
    heroEye: "Enregistrement d'achat",
    heroSub: 'Veuillez compléter ce formulaire pour enregistrer votre piano et organiser la livraison.',
    qrLabel: 'Piano scanné', qrDetail: 'Informations pré-remplies. Vous pouvez les modifier si nécessaire.',
    t1: 'Renseignements client', t2: 'Info livraison', t3: 'Détails du piano',
    t4: "Avis d'humidité", t5: 'Garantie & Consentement', tStaff: 'À remplir par le personnel',
    l_ln: 'Nom', l_fn: 'Prénom', l_em: 'Courriel', l_p1: 'Téléphone 1', l_p2: 'Téléphone 2',
    l_addr: 'Adresse', l_apt: 'App. / Unité', l_city: 'Ville', l_prov: 'Province', l_postal: 'Code postal',
    sd1: 'Détails de livraison', l_floor: 'Étage / Niveau', l_elev: 'Ascenseur disponible',
    yn_yes: 'Oui', yn_no: 'Non',
    l_sout: 'Marches extérieures', u_sout: 'marches',
    l_sin: 'Marches intérieures', u_sin: 'marches',
    l_turns: "Virages dans l'escalier", u_turns: 'virages',
    sd2: 'Éléments supplémentaires',
    l_collect: 'Piano à reprendre', yn_cy: 'Oui', yn_cn: 'Non',
    l_recycle: 'Piano à recycler', yn_ry: 'Oui', yn_rn: 'Non',
    l_crane: 'Grue nécessaire', yn_cry: 'Oui', yn_crn: 'Non',
    l_notes: 'Notes pour les déménageurs', l_deldate: 'Date de livraison souhaitée',
    wt: 'Supplément de livraison possible',
    wb: 'Notre équipe vous contactera avant la livraison pour discuter de tout frais supplémentaire.',
    l_make: 'Marque', l_make_edit: 'Marque',
    l_model: 'Modèle', l_serial: 'N° de série', l_color: 'Couleur / Fini',
    l_pdate: "Date d'achat", sd_acc: 'Accessoires',
    a_assembly: 'Assemblage', a_bench: 'Banc', a_dampp: 'Dampp-Chaser',
    a_adapter: 'Adaptateur élec.', a_headphones: 'Écouteurs',
    a_casters: 'Sous-pattes', a_cover: 'Housse',
    l_benchtype: 'Notes / Précisions (banc)', l_benchmodel: 'Modèle de banc',
    l_pianoNotes: 'Notes (piano)',
    sel: 'Sélectionner…',
    l_heard: 'Comment avez-vous entendu parler de nous ?',
    l_km40: 'Dans un rayon de 40 km du magasin',
    l_asap: 'Dès que possible (ASAP)', l_datefrom: 'À partir du',
    l_pcat: 'Catégorie', l_ptype: 'Type',
    l_consign_note: "ℹ️ Les pianos en consignation ne sont pas couverts par une garantie. Le client devra signer pour en prendre acte.",
    humNote: `Le piano doit être gardé en tout temps à un taux d'humidité entre <b style="font-size:15px;color:#92400e">40 % et 55 %</b> et une température entre <b style="font-size:15px;color:#92400e">18 °C et 26 °C</b>. En cas contraire, la garantie pourrait être annulée.`,
    humLabel: "Je comprends et m'engage à maintenir les conditions requises pour préserver la validité de la garantie.",
    pdfBadge: "Lire d'abord", pdfDone: 'Lu',
    pdfOverlay: 'Veuillez ouvrir et lire le document de garantie avant de signer',
    pdfOpenBtn: 'Ouvrir le document',
    pdfPending: 'Faites défiler le document pour déverrouiller la signature',
    pdfReadDone: 'Document lu — vous pouvez signer',
    sigLockMsg: 'Lisez d\'abord le document de garantie',
    sigInstr: "En signant, vous acceptez les conditions de la Garantie Limitée Piano Vertu et confirmez l'exactitude de toutes les informations.",
    sigWm: 'Signez au-dessus de cette ligne',
    typeMode: '⌨ Saisir le nom', drawMode: '✍ Dessiner', l_tname: 'Nom légal complet',
    agreeText: "J'ai lu et j'accepte les <a href=\"#\" onclick=\"return false\">Conditions de Garantie</a>. Je consens à la signature électronique. Je confirme l'exactitude de toutes les informations.",
    submitBtn: "Finaliser l'enregistrement", submitting: 'Envoi en cours…',
    staffSep: '— Section staff —',
    l_invoice: 'Facture #', l_from: 'Provenance', l_oldpiano: 'Destination ancien piano',
    l_surcharge: 'Montant supplément ($)',
    sd_staff2: 'Suivi',
    l_cheque: 'Chèque à récupérer', yn_chq_yes: 'Oui', yn_chq_no: 'Non',
    l_review: 'Google review demandé', yn_rev_yes: 'Oui', yn_rev_no: 'Non',
    l_paid: 'Entièrement payé', yn_paid_yes: 'Oui', yn_paid_no: 'Non',
    l_staffnotes: 'Notes staff',
    opt_dest_recycle: 'Recycle / Éco-Centre', opt_dest_second: 'Deuxième livraison',
    sTitle: 'Enregistrement complété',
    sMsg1: 'Merci ! Votre enregistrement Piano Vertu a été soumis.',
    sMsg2: 'Notre équipe vous contactera sous peu.',
    eName: 'Veuillez entrer votre nom complet.',
    eEmail: 'Veuillez entrer un courriel valide.',
    eStreet: "Veuillez entrer l'adresse de livraison.",
    eMake: 'Veuillez sélectionner la marque du piano.',
    eSerial: 'Veuillez entrer le numéro de série.',
    eDelDate: 'Veuillez sélectionner une date de livraison.',
    eHum: "Veuillez confirmer les conditions d'humidité.",
    eNotRead: 'Veuillez lire le document de garantie avant de signer.',
    eAgree: 'Veuillez accepter les conditions.',
    eTyped: 'Veuillez saisir votre nom légal.',
    eDraw: 'Veuillez dessiner votre signature.',
    eApiLoad: 'Erreur de chargement. Veuillez rafraîchir la page.',
  },
  en: {
    logoSub: 'Grand Piano Specialists',
    heroEye: 'Purchase Registration',
    heroSub: 'Please complete this form to register your piano and arrange delivery.',
    qrLabel: 'Piano scanned', qrDetail: 'Information pre-filled from QR code. You may edit if needed.',
    t1: 'Customer Information', t2: 'Delivery Info', t3: 'Piano Details',
    t4: 'Humidity Notice', t5: 'Warranty & Consent', tStaff: 'Staff Section',
    l_ln: 'Last Name', l_fn: 'First Name', l_em: 'Email', l_p1: 'Phone 1', l_p2: 'Phone 2',
    l_addr: 'Street Address', l_apt: 'Apt / Unit', l_city: 'City', l_prov: 'Province', l_postal: 'Postal Code',
    sd1: 'Delivery Details', l_floor: 'Floor / Level', l_elev: 'Elevator available',
    yn_yes: 'Yes', yn_no: 'No',
    l_sout: 'Steps outside', u_sout: 'steps',
    l_sin: 'Steps inside', u_sin: 'steps',
    l_turns: 'Turns in staircase', u_turns: 'turns',
    sd2: 'Extra charge items',
    l_collect: 'Piano to collect', yn_cy: 'Yes', yn_cn: 'No',
    l_recycle: 'Piano to recycle', yn_ry: 'Yes', yn_rn: 'No',
    l_crane: 'Crane required', yn_cry: 'Yes', yn_crn: 'No',
    l_notes: 'Notes for movers', l_deldate: 'Preferred delivery date',
    wt: 'Delivery surcharge may apply',
    wb: 'Our team will contact you before delivery to discuss any additional costs.',
    l_make: 'Brand', l_make_edit: 'Brand',
    l_model: 'Model', l_serial: 'Serial No.', l_color: 'Colour / Finish',
    l_pdate: 'Purchase date', sd_acc: 'Accessories',
    a_assembly: 'Assembly', a_bench: 'Bench', a_dampp: 'Dampp-Chaser',
    a_adapter: 'Electric adapter', a_headphones: 'Headphones',
    a_casters: 'Caster cups', a_cover: 'Piano cover',
    l_benchtype: 'Notes / Details (bench)', l_benchmodel: 'Bench model',
    l_pianoNotes: 'Notes (piano)',
    sel: 'Select…',
    l_heard: 'How did you hear about us?',
    l_km40: 'Within 40 km of the store',
    l_asap: 'As soon as possible (ASAP)', l_datefrom: 'Starting from',
    l_pcat: 'Category', l_ptype: 'Type',
    l_consign_note: 'ℹ️ Consignment pianos are not covered by a warranty. The customer will need to sign to acknowledge.',
    humNote: `The piano must be kept between <b style="font-size:15px;color:#92400e">40% and 55% humidity</b> and <b style="font-size:15px;color:#92400e">18°C and 26°C</b> at all times. Failure to do so may void the warranty.`,
    humLabel: 'I understand and commit to maintaining the required conditions to preserve the warranty.',
    pdfBadge: 'Read first', pdfDone: 'Read',
    pdfOverlay: 'Please open and read the warranty document before signing',
    pdfOpenBtn: 'Open Document',
    pdfPending: 'Scroll through the document to unlock the signature',
    pdfReadDone: 'Document read — you may now sign',
    sigLockMsg: 'Read the warranty document first',
    sigInstr: 'By signing, you agree to the Piano Vertu Limited Warranty and confirm all information is accurate.',
    sigWm: 'Sign above this line',
    typeMode: '⌨ Type Instead', drawMode: '✍ Draw Instead', l_tname: 'Full legal name',
    agreeText: 'I have read and agree to the <a href="#" onclick="return false">Warranty Terms</a>. I consent to electronic signatures. I confirm all information is accurate.',
    submitBtn: 'Complete Registration', submitting: 'Submitting…',
    staffSep: '— Staff Section —',
    l_invoice: 'Invoice #', l_from: 'From location', l_oldpiano: 'Old piano destination',
    l_surcharge: 'Surcharge amount ($)',
    sd_staff2: 'Follow-up',
    l_cheque: 'Cheque to collect', yn_chq_yes: 'Yes', yn_chq_no: 'No',
    l_review: 'Google review requested', yn_rev_yes: 'Yes', yn_rev_no: 'No',
    l_paid: 'Fully paid', yn_paid_yes: 'Yes', yn_paid_no: 'No',
    l_staffnotes: 'Staff notes',
    opt_dest_recycle: 'Recycle / Eco-Centre', opt_dest_second: 'Second delivery',
    sTitle: 'Registration Complete',
    sMsg1: 'Thank you! Your Piano Vertu registration has been submitted.',
    sMsg2: 'Our team will be in touch shortly.',
    eName: 'Please enter your full name.',
    eEmail: 'Please enter a valid email.',
    eStreet: 'Please enter the delivery address.',
    eMake: 'Please select the piano brand.',
    eSerial: 'Please enter the serial number.',
    eDelDate: 'Please select a delivery date.',
    eHum: 'Please confirm the humidity conditions.',
    eNotRead: 'Please read the warranty document before signing.',
    eAgree: 'Please accept the warranty terms.',
    eTyped: 'Please type your full legal name.',
    eDraw: 'Please draw your signature.',
    eApiLoad: 'Loading error. Please refresh the page.',
  },
  zh: {
    logoSub: '大型三角钢琴专家',
    heroEye: '购买注册',
    heroSub: '请填写此表格以注册您的钢琴并安排送货。',
    qrLabel: '已扫描钢琴', qrDetail: '信息已从 QR 码预填。如需要可以修改。',
    t1: '客户信息', t2: '送货信息', t3: '钢琴详情',
    t4: '湿度须知', t5: '保修与同意', tStaff: '员工填写',
    l_ln: '姓', l_fn: '名', l_em: '电子邮箱', l_p1: '电话 1', l_p2: '电话 2',
    l_addr: '街道地址', l_apt: '公寓/单元', l_city: '城市', l_prov: '省份', l_postal: '邮政编码',
    sd1: '送货详情', l_floor: '楼层', l_elev: '有电梯',
    yn_yes: '是', yn_no: '否',
    l_sout: '室外台阶数', u_sout: '级',
    l_sin: '室内台阶数', u_sin: '级',
    l_turns: '楼梯转弯数', u_turns: '个转弯',
    sd2: '额外收费项目',
    l_collect: '需要回收旧钢琴', yn_cy: '是', yn_cn: '否',
    l_recycle: '需要处理旧钢琴', yn_ry: '是', yn_rn: '否',
    l_crane: '需要起重机', yn_cry: '是', yn_crn: '否',
    l_notes: '搬运工附加说明', l_deldate: '期望送货日期',
    wt: '可能需要额外送货费',
    wb: '我们的团队将在送货前与您联系讨论任何额外费用。',
    l_make: '品牌', l_make_edit: '品牌',
    l_model: '型号', l_serial: '序列号', l_color: '颜色/饰面',
    l_pdate: '购买日期', sd_acc: '配件',
    a_assembly: '组装', a_bench: '琴凳', a_dampp: 'Dampp-Chaser',
    a_adapter: '电源适配器', a_headphones: '耳机',
    a_casters: '底座垫', a_cover: '钢琴罩',
    l_benchtype: '备注/详情（琴凳）', l_benchmodel: '琴凳型号',
    l_pianoNotes: '备注（钢琴）',
    sel: '选择…',
    l_heard: '您是如何了解我们的？',
    l_km40: '距店铺40公里范围内',
    l_asap: '尽快（ASAP）', l_datefrom: '最早日期',
    l_pcat: '类别', l_ptype: '类型',
    l_consign_note: 'ℹ️ 寄售钢琴不在保修范围内。客户需签字确认。',
    humNote: `钢琴必须始终保持在 <b style="font-size:15px;color:#92400e">40% 至 55% 湿度</b> 和 <b style="font-size:15px;color:#92400e">18°C 至 26°C</b> 之间。否则可能导致保修失效。`,
    humLabel: '我理解并承诺保持所需条件以保持保修的有效性。',
    pdfBadge: '请先阅读', pdfDone: '已读',
    pdfOverlay: '请打开并阅读保修文件后再签名',
    pdfOpenBtn: '打开文件',
    pdfPending: '请滚动阅读文件以解锁签名',
    pdfReadDone: '文件已阅读 — 您现在可以签名',
    sigLockMsg: '请先阅读保修文件',
    sigInstr: '签名即表示您同意Piano Vertu有限保修条款并确认所有信息准确。',
    sigWm: '请在此线上方签名',
    typeMode: '⌨ 输入姓名', drawMode: '✍ 手写签名', l_tname: '完整法定姓名',
    agreeText: '我已阅读并同意<a href="#" onclick="return false">保修条款</a>。我同意电子签名具有法律约束力。我确认所有信息准确。',
    submitBtn: '完成注册', submitting: '提交中…',
    staffSep: '— 员工填写 —',
    l_invoice: '发票编号', l_from: '来源地点', l_oldpiano: '旧钢琴去向',
    l_surcharge: '附加费金额 ($)',
    sd_staff2: '跟进',
    l_cheque: '需要收取支票', yn_chq_yes: '是', yn_chq_no: '否',
    l_review: '已请求Google评价', yn_rev_yes: '是', yn_rev_no: '否',
    l_paid: '已全额付款', yn_paid_yes: '是', yn_paid_no: '否',
    l_staffnotes: '员工备注',
    opt_dest_recycle: '回收 / 生态中心', opt_dest_second: '第二次送货',
    sTitle: '注册完成',
    sMsg1: '谢谢！您的Piano Vertu注册已提交。',
    sMsg2: '我们的团队将尽快与您联系。',
    eName: '请输入您的全名。',
    eEmail: '请输入有效电子邮箱。',
    eStreet: '请输入送货地址。',
    eMake: '请选择钢琴品牌。',
    eSerial: '请输入序列号。',
    eDelDate: '请选择送货日期。',
    eHum: '请确认湿度条件。',
    eNotRead: '请在签名前阅读保修文件。',
    eAgree: '请接受保修条款。',
    eTyped: '请输入完整法定姓名。',
    eDraw: '请绘制您的签名。',
    eApiLoad: '加载错误，请刷新页面。',
  },
};

const s = (id, v, html = false) => {
  const el = document.getElementById(id);
  if (el) html ? el.innerHTML = v : el.textContent = v;
};

/**
 * Apply all translations to the DOM for a given language.
 * Call after formData is loaded so dropdowns can also be refreshed.
 * @param {string} lang
 * @param {object} deps - { today, pdfRead, pdfOpened, typeMode, onCatChange, refreshDropdowns }
 */
export function applyLang(lang, deps) {
  const t = L[lang];
  const { today, pdfRead, typeMode, onCatChange } = deps;

  document.documentElement.lang = lang === 'zh' ? 'zh-Hans' : lang;
  document.body.style.fontFamily = lang === 'zh'
    ? "'Noto Serif SC','DM Sans',sans-serif"
    : "'DM Sans',sans-serif";

  ['fr', 'en', 'zh'].forEach(x =>
    document.getElementById('btn-' + x)?.classList.toggle('active', x === lang)
  );

  // Hero / QR
  s('heroEye', t.heroEye); s('heroSub', t.heroSub);
  s('qrLabel', t.qrLabel); s('qrDetail', t.qrDetail);

  // Section titles
  s('t1', t.t1); s('t2', t.t2); s('t3', t.t3);
  s('t4', t.t4); s('t5', t.t5); s('tStaff', t.tStaff);

  // Section 1 — Client
  s('l-ln', t.l_ln); s('l-fn', t.l_fn); s('l-em', t.l_em);
  s('l-p1', t.l_p1); s('l-p2', t.l_p2);
  s('l-heard', t.l_heard);

  // Section 2 — Delivery
  s('l-addr', t.l_addr); s('l-apt', t.l_apt); s('l-city', t.l_city);
  s('l-prov', t.l_prov); s('l-postal', t.l_postal);
  s('sd1', t.sd1); s('l-floor', t.l_floor); s('l-elev', t.l_elev);
  s('yn-yes', t.yn_yes); s('yn-no', t.yn_no);
  s('l-sout', t.l_sout); s('u-sout', t.u_sout);
  s('l-sin', t.l_sin); s('u-sin', t.u_sin);
  s('l-turns', t.l_turns); s('u-turns', t.u_turns);
  s('sd2', t.sd2);
  s('l-collect', t.l_collect); s('yn-cy', t.yn_cy); s('yn-cn', t.yn_cn);
  s('l-recycle', t.l_recycle); s('yn-ry', t.yn_ry); s('yn-rn', t.yn_rn);
  s('l-crane', t.l_crane); s('yn-cry', t.yn_cry); s('yn-crn', t.yn_crn);
  s('l-notes', t.l_notes); s('l-deldate', t.l_deldate);
  s('wt', t.wt); s('wb', t.wb);
  s('l-km40', t.l_km40); s('l-asap', t.l_asap); s('l-datefrom', t.l_datefrom);

  // Section 3 — Piano
  s('l-pcat', t.l_pcat); s('l-ptype', t.l_ptype);
  s('l-make', t.l_make); s('l-make-edit', t.l_make_edit);
  s('l-model', t.l_model); s('l-serial', t.l_serial);
  s('l-color', t.l_color); s('l-pdate', t.l_pdate);
  s('sd-acc', t.sd_acc);
  s('a-assembly', t.a_assembly); s('a-bench', t.a_bench); s('a-dampp', t.a_dampp);
  s('a-adapter', t.a_adapter); s('a-headphones', t.a_headphones);
  s('a-casters', t.a_casters); s('a-cover', t.a_cover);
  s('l-benchtype', t.l_benchtype); s('l-benchmodel', t.l_benchmodel);
  s('l-pianoNotes', t.l_pianoNotes);
  s('l-consign-note', t.l_consign_note);

  // All "Select…" placeholders
  ['opt-cat-ph', 'opt-prov-ph', 'opt-bench-ph', 'opt-from-ph', 'opt-dest-ph', 'opt-heard-ph']
    .forEach(id => { const el = document.getElementById(id); if (el) el.textContent = t.sel; });

  // Section 4 — Humidity
  s('humNote', t.humNote, true); s('humLabel', t.humLabel);

  // Section 5 — PDF / Signature
  s('pdfStatusTxt', t.pdfPending);
  s('sigLockMsg', t.sigLockMsg); s('sigInstr', t.sigInstr); s('sigWm', t.sigWm);
  document.getElementById('typeToggle').textContent = typeMode ? t.drawMode : t.typeMode;
  s('l-tname', t.l_tname);
  s('agreeText', t.agreeText, true);
  s('submitBtn', t.submitBtn);

  // Staff
  s('staffSepLabel', t.staffSep);
  s('l-invoice', t.l_invoice); s('l-from', t.l_from);
  s('l-oldpiano', t.l_oldpiano); s('l-surcharge', t.l_surcharge);
  s('sd-staff2', t.sd_staff2);
  s('l-cheque', t.l_cheque); s('yn-chq-yes', t.yn_chq_yes); s('yn-chq-no', t.yn_chq_no);
  s('l-review', t.l_review); s('yn-rev-yes', t.yn_rev_yes); s('yn-rev-no', t.yn_rev_no);
  s('l-paid', t.l_paid); s('yn-paid-yes', t.yn_paid_yes); s('yn-paid-no', t.yn_paid_no);
  s('l-staffnotes', t.l_staffnotes);
  s('opt-dest-recycle', t.opt_dest_recycle); s('opt-dest-second', t.opt_dest_second);

  // Success screen
  s('sTitle', t.sTitle); s('sMsg1', t.sMsg1); s('sMsg2', t.sMsg2);

  // Date display
  document.getElementById('dateDisp').textContent = today.toLocaleDateString(
    lang === 'fr' ? 'fr-CA' : lang === 'zh' ? 'zh-CN' : 'en-CA',
    { year: 'numeric', month: 'long', day: 'numeric' }
  );

  // Re-render piano dropdowns in new language
  const curCat = document.getElementById('pianoCategory')?.value;
  if (curCat) onCatChange(curCat);
}
