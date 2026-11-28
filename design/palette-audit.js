// Contrast + overflow audit of the page that is open (paste into the browser console or run through the browser tool).
// Returns { overflow: [...], fails: [...], checked } : every visible text element whose colour against the first opaque background behind it is
// below WCAG AA (4.5, or 3 for large text: >= 24px, or >= 18.66px and bold). A gradient or an image behind the text counts as unknown ("?" in bg).
(function () {
    const parse = c => { const m = c.match(/rgba?\(([^)]+)\)/); if (!m) return null; const p = m[1].split(/[ ,\/]+/).filter(Boolean).map(Number); return { r: p[0], g: p[1], b: p[2], a: p.length > 3 ? p[3] : 1 }; };
    const lin = v => { v /= 255; return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); };
    const lum = c => 0.2126 * lin(c.r) + 0.7152 * lin(c.g) + 0.0722 * lin(c.b);
    const ratio = (a, b) => { const x = lum(a), y = lum(b); return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05); };
    const blend = (top, bottom) => ({ r: top.r * top.a + bottom.r * (1 - top.a), g: top.g * top.a + bottom.g * (1 - top.a), b: top.b * top.a + bottom.b * (1 - top.a), a: 1 });
    const hex = c => '#' + [c.r, c.g, c.b].map(v => Math.round(v).toString(16).padStart(2, '0')).join('');
    function bgOf(el) {
        const layers = [];
        for (let e = el; e && e.nodeType === 1; e = e.parentElement) {
            const cs = getComputedStyle(e);
            if (cs.backgroundImage && cs.backgroundImage !== 'none') return { unknown: true };
            const c = parse(cs.backgroundColor);
            if (c && c.a > 0) { layers.push(c); if (c.a >= 0.99) break; }
        }
        let base = { r: 255, g: 255, b: 255, a: 1 };
        for (let i = layers.length - 1; i >= 0; i--) base = blend(layers[i], base);
        return base;
    }
    const out = { overflow: [], fails: [], checked: 0, width: innerWidth };
    const scroller = document.scrollingElement;
    document.querySelectorAll('body *').forEach(el => {
        const r = el.getBoundingClientRect();
        if (r.width && r.right > innerWidth + 1 && !el.closest('.nh-shots, .skin-chip')) { if (out.overflow.length < 8) out.overflow.push(el.tagName + '.' + String(el.className && el.className.baseVal !== undefined ? el.className.baseVal : el.className).slice(0, 40) + ':' + Math.round(r.right)); }
        const own = [...el.childNodes].filter(n => n.nodeType === 3 && n.textContent.trim().length > 1);
        if (!own.length) return;
        const cs = getComputedStyle(el);
        if (cs.visibility === 'hidden' || cs.display === 'none' || +cs.opacity === 0 || r.width === 0 || r.height === 0) return;
        const fg0 = parse(cs.color); if (!fg0) return;
        const bg = bgOf(el);
        out.checked++;
        if (bg.unknown) return;
        const fg = fg0.a < 1 ? blend(fg0, bg) : fg0;
        const size = parseFloat(cs.fontSize), bold = +cs.fontWeight >= 700;
        const need = size >= 24 || (size >= 18.66 && bold) ? 3 : 4.5;
        const cr = ratio(fg, bg);
        if (cr < need) out.fails.push({ text: own[0].textContent.trim().slice(0, 36), ratio: +cr.toFixed(2), need, fg: hex(fg), bg: hex(bg), sel: el.tagName.toLowerCase() + '.' + String(el.className || '').split(' ').slice(0, 2).join('.') });
    });
    // the same text with the same colours is reported once
    const seen = new Set(); out.fails = out.fails.filter(f => { const k = f.fg + f.bg + f.sel; if (seen.has(k)) return false; seen.add(k); return true; });
    out.fails.sort((a, b) => a.ratio - b.ratio); out.fails = out.fails.slice(0, 25);
    return out;
})();
