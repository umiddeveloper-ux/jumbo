// Generates the ORIGINAL avatar frames of the books (design/frames): one ornate frame and one simple ring per book 1..14, the legend frame,
// the award tiles and a preview sheet. Pure vector art made by code (no raster, no randomness): run  node tools/gen-frames.mjs
//
// Geometry (viewBox 0 0 200 200): the avatar circle is r = 62 around (100,100) and stays free; the ring lies at r = 62..72; ornaments grow
// outward and stay inside r = 98. Shapes are drawn pointing UP from the origin (base at 0,0) and placed by a polar angle (0 = top, clockwise).
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const OUT = join(dirname(fileURLToPath(import.meta.url)), '..', 'design', 'frames');
mkdirSync(OUT, { recursive: true });

const C = 100;
const f = n => String(+n.toFixed(1));
const polar = (a, r) => { const t = a * Math.PI / 180; return [C + r * Math.sin(t), C - r * Math.cos(t)]; };

// ---------- materials: 4 stops, light -> dark ----------
const M = {
  bronze:   ['#ffd9a8', '#d99a5b', '#a8672e', '#6e3f19'],
  silver:   ['#ffffff', '#d6dbe6', '#9aa3b5', '#626b7e'],
  gold:     ['#fff3b0', '#ffd23f', '#e0a100', '#9a6a00'],
  emerald:  ['#c4ffe0', '#3fe08a', '#0f9d58', '#06623a'],
  sapphire: ['#d6e8ff', '#68a4ff', '#2a62d9', '#142f7a'],
  ruby:     ['#ffd6db', '#ff6478', '#d1203b', '#7a0f22'],
  platinum: ['#f9fcff', '#dce7fb', '#a9b9e0', '#6d7fb5'],
  amethyst: ['#f3dcff', '#bc6ef5', '#7a2fc4', '#42176f'],
  flame:    ['#fff3ae', '#ffbb12', '#ff6a00', '#c1200a'],
  diamond:  ['#ffffff', '#c6f3ff', '#6cc7f2', '#2f86c9'],
  crystal:  ['#f5eeff', '#cdb8ff', '#8f6ee8', '#5436a8'],
  phoenix:  ['#fff3b8', '#ffc83a', '#ff8a1f', '#d6420f'],
  legend:   ['#ecd2ff', '#aa63f2', '#6a2bb8', '#341066'],
  cream:    ['#ffffff', '#fff6e0', '#f0dfb8', '#cdb27a'],
};
const GEM = { ruby: M.ruby, sapphire: M.sapphire, emerald: M.emerald, diamond: M.diamond, gold: M.gold, amethyst: M.amethyst, pearl: ['#ffffff', '#f4f1ee', '#d9d2cc', '#a89f98'] };

// ---------- small building blocks (all return strings) ----------
const grad = (id, st, d = 'd') => {
  const [x2, y2] = d === 'v' ? [0, 1] : d === 'h' ? [1, 0] : [1, 1];
  return `<linearGradient id="${id}" x1="0" y1="0" x2="${x2}" y2="${y2}">${st.map((c, i) => `<stop offset="${[0, .38, .7, 1][i]}" stop-color="${c}"/>`).join('')}</linearGradient>`;
};
const leaf = (l, w) => `M0 0C${f(w)} ${f(-l * .25)} ${f(w * .8)} ${f(-l * .8)} 0 ${f(-l)}C${f(-w * .8)} ${f(-l * .8)} ${f(-w)} ${f(-l * .25)} 0 0Z`;
const shard = (l, w) => `M${f(-w)} 0L${f(-w * .72)} ${f(-l * .6)}L0 ${f(-l)}L${f(w * .72)} ${f(-l * .6)}L${f(w)} 0Z`;
const claw = (l, w) => `M${f(-w)} 0C${f(-w)} ${f(-l * .35)} ${f(w * .8)} ${f(-l * .6)} ${f(w * 2.2)} ${f(-l)}C${f(w * 1.2)} ${f(-l * .5)} ${f(w)} ${f(-l * .3)} ${f(w)} 0Z`;
const feather = (l, w) => `M${f(-w * .4)} 0C${f(w * 1.3)} ${f(-l * .15)} ${f(w * 1.1)} ${f(-l * .75)} ${f(w * .2)} ${f(-l)}C${f(-w * .9)} ${f(-l * .7)} ${f(-w * 1.1)} ${f(-l * .2)} ${f(-w * .4)} 0Z`;
const flame = (l, w) => `M${f(-w)} 0C${f(-w * 1.1)} ${f(-l * .4)} ${f(-w * .1)} ${f(-l * .55)} ${f(w * .15)} ${f(-l)}C${f(w * .3)} ${f(-l * .6)} ${f(w * 1.2)} ${f(-l * .45)} ${f(w)} 0Z`;
const gemPath = r => `M0 ${f(-r)}L${f(r * .85)} ${f(-r * .15)}L0 ${f(r)}L${f(-r * .85)} ${f(-r * .15)}Z`;
const star = (r, k = .45) => { let d = ''; for (let i = 0; i < 10; i++) { const rr = i % 2 ? r * k : r, t = i * Math.PI / 5; d += (i ? 'L' : 'M') + f(rr * Math.sin(t)) + ' ' + f(-rr * Math.cos(t)); } return d + 'Z'; };

// place a <use> of a def at polar (a, r); rot = extra rotation; flip mirrors the shape; s scales it
const put = (id, a, r, { rot = 0, flip = false, s = 1, o } = {}) => {
  const [x, y] = polar(a, r);
  return `<use href="#${id}" transform="translate(${f(x)} ${f(y)}) rotate(${f(a + rot)})${flip ? ' scale(-1 1)' : ''}${s !== 1 ? ` scale(${s})` : ''}"${o ? ` opacity="${o}"` : ''}/>`;
};
const at = (id, x, y, { rot = 0, s = 1, flip = false } = {}) => `<use href="#${id}" transform="translate(${f(x)} ${f(y)})${rot ? ` rotate(${f(rot)})` : ''}${flip ? ' scale(-1 1)' : ''}${s !== 1 ? ` scale(${s})` : ''}"/>`;
const mirror = (a) => 360 - a;                 // the angle on the other side
// the length a shape may have so that its tip stays inside r = maxR (base at polar (a, r), pointing in direction d degrees clockwise from up)
const fit = (a, r, d, len, maxR = 96) => {
  const [bx, by] = polar(a, r), x = bx - C, y = by - C, t = d * Math.PI / 180, ux = Math.sin(t), uy = -Math.cos(t);
  const dot = x * ux + y * uy, tmax = -dot + Math.sqrt(Math.max(0, dot * dot - (x * x + y * y) + maxR * maxR));
  return Math.min(1, tmax / len);
};
// a wing: feathers [base angle, direction, length factor] fanning along the side, the same on the left (mirrored); each one is cut to stay inside r = 96
const wings = (id, nominal, list, extra = {}) => list.map(([a, d, lf]) => {
  const k = Math.min(lf, fit(a, 68, d, nominal));
  return put(id, a, 68, { rot: d - a, s: k, ...extra }) + put(id, mirror(a), 68, { rot: -(d - a), flip: true, s: k, ...extra });
}).join('');
const range = (a, b, step) => { const o = []; for (let v = a; step > 0 ? v <= b + 1e-6 : v >= b - 1e-6; v += step) o.push(v); return o; };

// the ring: a thick gradient circle with an inner shadow, a bright outer edge and a highlight arc
const ring = (g, w = 10, r = 67) => {
  const [hx1, hy1] = polar(-62, r), [hx2, hy2] = polar(-14, r);
  return `<circle cx="100" cy="100" r="${r}" fill="none" stroke="url(#${g})" stroke-width="${w}"/>`
    + `<circle cx="100" cy="100" r="${f(r - w / 2 - 1)}" fill="none" stroke="#000" stroke-opacity=".3" stroke-width="2.6"/>`
    + `<circle cx="100" cy="100" r="${f(r + w / 2 - .6)}" fill="none" stroke="#fff" stroke-opacity=".38" stroke-width="1.1"/>`
    + `<path d="M${f(hx1)} ${f(hy1)}A${r} ${r} 0 0 1 ${f(hx2)} ${f(hy2)}" fill="none" stroke="#fff" stroke-opacity=".55" stroke-width="2.6" stroke-linecap="round"/>`;
};

// a gem in a small setting
const gemDefs = (key, gid, setId = 'gold') => `<g id="${key}"><path d="${gemPath(7)}" fill="url(#${gid})" stroke="url(#${setId})" stroke-width="1.6" stroke-linejoin="round"/><path d="M0-7L0 7M-6-1H6" stroke="#fff" stroke-opacity=".45" stroke-width=".7"/><path d="M-3.5-3.2L0-5.5" stroke="#fff" stroke-opacity=".8" stroke-width="1.1" stroke-linecap="round"/></g>`;
const crownPath = (w, h) => `M${f(-w / 2)} 0L${f(-w / 2)} ${f(-h * .55)}L${f(-w * .28)} ${f(-h * .3)}L${f(-w * .14)} ${f(-h * .85)}L0 ${f(-h * .4)}L${f(w * .14)} ${f(-h * .85)}L${f(w * .28)} ${f(-h * .3)}L${f(w / 2)} ${f(-h * .55)}L${f(w / 2)} 0Z`;
const bigCrown = (w, h) => `M${f(-w / 2)} 0L${f(-w / 2)} ${f(-h * .5)}L${f(-w * .3)} ${f(-h * .25)}L${f(-w * .2)} ${f(-h * .9)}L${f(-w * .1)} ${f(-h * .35)}L0 ${f(-h)}L${f(w * .1)} ${f(-h * .35)}L${f(w * .2)} ${f(-h * .9)}L${f(w * .3)} ${f(-h * .25)}L${f(w / 2)} ${f(-h * .5)}L${f(w / 2)} 0Z`;
const sparkle = r => `M0 ${f(-r)}Q${f(r * .12)} ${f(-r * .12)} ${f(r)} 0Q${f(r * .12)} ${f(r * .12)} 0 ${f(r)}Q${f(-r * .12)} ${f(r * .12)} ${f(-r)} 0Q${f(-r * .12)} ${f(-r * .12)} 0 ${f(-r)}Z`;

// the wrapper: ids get the file prefix so that inline copies never collide
const svg = (defs, body) => `<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 200 200"><defs>${defs}</defs>${body}</svg>`;
const prefixIds = (s, p) => s.replace(/id="([^"]+)"/g, `id="${p}$1"`).replace(/url\(#([^)]+)\)/g, `url(#${p}$1)`).replace(/href="#([^"]+)"/g, `href="#${p}$1"`);
const min = s => s.replace(/\s*\n\s*/g, '').replace(/>\s+</g, '><');

// ---------- the 14 frames ----------
const NAMES = ['', 'Bronze', 'Silver', 'Gold', 'Emerald', 'Sapphire', 'Ruby', 'Platinum', 'Amethyst', 'Flame', 'Diamond', 'Crystal', 'Phoenix', 'Royal', 'Jumbo Legend'];
const RARITY = ['', 'Common', 'Common', 'Rare', 'Rare', 'Rare', 'Epic', 'Epic', 'Epic', 'Epic', 'Legendary', 'Legendary', 'Legendary', 'Legendary', 'Mythic'];
const MATERIAL = ['', 'bronze', 'silver', 'gold', 'emerald', 'sapphire', 'ruby', 'platinum', 'amethyst', 'flame', 'diamond', 'crystal', 'phoenix', 'gold', 'legend'];

const commonDefs = m => grad('g', M[m]) + grad('gv', M[m], 'v') + grad('gr', [...M[m]].reverse()) + grad('gold', M.gold) + grad('cream', M.cream, 'v');
const gemGrads = names => names.map(n => grad('gm-' + n, GEM[n])).join('');

const FRAMES = {
  1() { // bronze: two laurel branches meeting under an open book
    const L = 'L', defs = commonDefs('bronze') + `<g id="l"><path d="${leaf(21, 7.5)}" fill="url(#gv)"/><path d="M0-1V-17" stroke="#fff" stroke-opacity=".35" stroke-width=".8"/></g>`
      + `<g id="b"><circle r="2.3" fill="url(#gr)"/></g>`
      + `<g id="book"><path d="M0-3C-7-7-15-7-20-4V10C-15 7-7 7 0 11Z" fill="url(#cream)" stroke="url(#g)" stroke-width="2" stroke-linejoin="round"/><path d="M0-3C7-7 15-7 20-4V10C15 7 7 7 0 11Z" fill="url(#cream)" stroke="url(#g)" stroke-width="2" stroke-linejoin="round"/><path d="M-15 0C-10-2-6-2-3 0M-15 4C-10 2-6 2-3 4M15 0C10-2 6-2 3 0M15 4C10 2 6 2 3 4" fill="none" stroke="#b79a5a" stroke-width=".9"/></g>`;
    let body = '';
    for (const a of range(165, 38, -13)) { body += put('l', a, 70, { rot: -38 }) + put('l', mirror(a), 70, { rot: 38, flip: true }); }
    for (const a of range(158, 45, -13)) { body += put('b', a, 77) + put('b', mirror(a), 77); }
    body += ring('g') + at('book', 100, 168);
    return [defs, body];
  },
  2() { // silver: long slim willow leaves and a star under the ring
    const defs = commonDefs('silver') + `<g id="l"><path d="${leaf(28, 4.6)}" fill="url(#gv)"/><path d="M0-1V-24" stroke="#fff" stroke-opacity=".5" stroke-width=".7"/></g><g id="s"><path d="${star(12)}" fill="url(#g)" stroke="url(#gr)" stroke-width="1.2" stroke-linejoin="round"/><path d="${star(5, .5)}" fill="#fff" fill-opacity=".45"/></g>`;
    let body = '';
    for (const a of range(172, 28, -9)) { body += put('l', a, 70, { rot: -62 }) + put('l', mirror(a), 70, { rot: 62, flip: true }); }
    for (const a of range(165, 45, -15)) { body += put('l', a, 70, { rot: -22, s: .62 }) + put('l', mirror(a), 70, { rot: 22, flip: true, s: .62 }); }
    body += ring('g') + at('s', 100, 176);
    return [defs, body];
  },
  3() { // gold: paired olive leaves, a three-point crown on top, a ribbon bow at the bottom
    const defs = commonDefs('gold') + gemGrads(['ruby', 'sapphire']) + gemDefs('gr1', 'gm-ruby') + gemDefs('gs1', 'gm-sapphire')
      + `<g id="l"><path d="${leaf(20, 6.5)}" fill="url(#gv)"/><path d="M0-1V-16" stroke="#fff" stroke-opacity=".4" stroke-width=".7"/></g>`
      + `<g id="crown"><path d="${crownPath(40, 26)}" fill="url(#gv)" stroke="url(#gr)" stroke-width="1.6" stroke-linejoin="round"/><path d="M-20-4H20" stroke="#fff" stroke-opacity=".5" stroke-width="1.2"/><circle cx="-18.5" cy="-14" r="2.6" fill="url(#g)"/><circle cx="0" cy="-22" r="3" fill="url(#g)"/><circle cx="18.5" cy="-14" r="2.6" fill="url(#g)"/></g>`
      + `<g id="bow"><path d="M0 0C-8-9-20-9-22-2C-20 5-10 5 0 0ZM0 0C8-9 20-9 22-2C20 5 10 5 0 0Z" fill="url(#gv)" stroke="url(#gr)" stroke-width="1.3" stroke-linejoin="round"/><path d="M-3 2L-12 14L-6 12L-3 16ZM3 2L12 14L6 12L3 16Z" fill="url(#gr)"/><circle r="3.4" fill="url(#g)" stroke="url(#gr)" stroke-width="1"/></g>`;
    let body = '';
    for (const a of range(158, 70, -17)) {
      body += put('l', a, 70, { rot: -52 }) + put('l', a, 70, { rot: -8 });
      body += put('l', mirror(a), 70, { rot: 52, flip: true }) + put('l', mirror(a), 70, { rot: 8, flip: true });
    }
    body += ring('g') + at('bow', 100, 171) + at('crown', 100, 38) + at('gr1', 100, 24, { s: .8 }) + at('gs1', 88, 28, { s: .45 }) + at('gs1', 112, 28, { s: .45 });
    return [defs, body];
  },
  4() { // emerald: a bed of green crystals under the ring, tallest at the bottom
    const defs = commonDefs('emerald') + gemGrads(['emerald', 'diamond']) + gemDefs('ge', 'gm-diamond')
      + `<g id="s1"><path d="${shard(22, 7.5)}" fill="url(#gv)"/><path d="M0 0L0-20M-4.5-2L-2-15" stroke="#fff" stroke-opacity=".35" stroke-width=".8"/></g>`
      + `<g id="s2"><path d="${shard(26, 8)}" fill="url(#g)"/><path d="M0 0L0-23M-5-2L-2-17" stroke="#fff" stroke-opacity=".4" stroke-width=".8"/></g>`;
    let body = '';
    for (const a of range(70, 290, 18)) { const big = Math.abs(a - 180) < 28; body += put(big ? 's2' : 's1', a, 68, { s: big ? 1.1 : 1 }); }
    body += ring('g') + at('ge', 100, 26, { s: .9 });
    return [defs, body];
  },
  5() { // sapphire: two clusters of blue crystals fanning out like wings, a diamond on top and a gem below
    const defs = commonDefs('sapphire') + gemGrads(['diamond', 'sapphire']) + gemDefs('gd', 'gm-diamond') + gemDefs('gb', 'gm-sapphire')
      + `<g id="s1"><path d="${shard(20, 6)}" fill="url(#gv)"/></g><g id="s2"><path d="${shard(27, 7.5)}" fill="url(#g)"/><path d="M0 0L0-23M-3.6-1L-1.6-16" stroke="#fff" stroke-opacity=".45" stroke-width=".8"/></g>`;
    let body = '';
    const fan = [[28, 's1', 0], [44, 's2', 0], [60, 's1', 0], [76, 's2', 0], [92, 's2', 0], [108, 's2', 0], [124, 's1', 0], [140, 's2', 0], [156, 's1', 0]];
    for (const [a, id] of fan) { body += put(id, a, 68) + put(id, mirror(a), 68); }
    body += ring('g') + at('gd', 100, 23, { s: 1.15 }) + at('gb', 100, 172, { s: 1.1 });
    return [defs, body];
  },
  6() { // ruby: curved claws sweeping around, a heart-cut ruby at the bottom
    const defs = commonDefs('ruby') + gemGrads(['ruby']) + gemDefs('gr1', 'gm-ruby')
      + `<g id="c1"><path d="${claw(20, 3.8)}" fill="url(#gv)"/></g><g id="c2"><path d="${claw(28, 4.6)}" fill="url(#g)"/></g>`;
    let body = '';
    for (let i = 0; i < 9; i++) { const a = 10 + i * 19; body += put(i % 2 ? 'c1' : 'c2', a, 67) + put(i % 2 ? 'c1' : 'c2', mirror(a), 67, { flip: true }); }
    body += ring('g') + at('gr1', 100, 172, { s: 1.5 }) + at('gr1', 100, 25, { s: .8 });
    return [defs, body];
  },
  7() { // platinum: pale swept wings and a star on top
    const defs = commonDefs('platinum') + `<g id="f1"><path d="${feather(30, 8)}" fill="url(#gv)" stroke="url(#gr)" stroke-width=".8"/><path d="M0-1C1-12 1-22 .5-28" stroke="#fff" stroke-opacity=".6" stroke-width=".8" fill="none"/></g><g id="f2"><path d="${feather(40, 9.5)}" fill="url(#gv)" stroke="url(#gr)" stroke-width=".8"/><path d="M0-1C1-14 1-30 .5-37" stroke="#fff" stroke-opacity=".6" stroke-width=".8" fill="none"/></g>`
      + `<g id="f3"><path d="${feather(50, 11)}" fill="url(#gv)" stroke="url(#gr)" stroke-width=".8"/><path d="M0-2C1.5-18 1.5-36 .6-47" stroke="#fff" stroke-opacity=".65" stroke-width=".9" fill="none"/></g><g id="s"><path d="${star(11)}" fill="url(#g)" stroke="url(#gr)" stroke-width="1.2" stroke-linejoin="round"/><path d="${star(4.5, .5)}" fill="#fff" fill-opacity=".6"/></g>`;
    let body = '';
    body += wings('f3', 50, [[116, 84, .8], [107, 70, .95], [98, 54, 1], [89, 40, 1], [80, 26, 1], [71, 13, .9], [62, 3, .75]]);
    body += ring('g') + at('s', 100, 21, { s: 1.15 });
    return [defs, body];
  },
  8() { // amethyst: sparse purple crystals and an orbit with a pearl
    const defs = commonDefs('amethyst') + gemGrads(['amethyst', 'pearl']) + gemDefs('ga', 'gm-amethyst')
      + `<g id="s1"><path d="${shard(17, 6)}" fill="url(#gv)"/></g><g id="s2"><path d="${shard(27, 8)}" fill="url(#g)"/><path d="M0 0L0-22" stroke="#fff" stroke-opacity=".4" stroke-width=".8"/></g>`
      + `<radialGradient id="pe" cx=".35" cy=".3" r=".8"><stop offset="0" stop-color="#fff"/><stop offset="1" stop-color="#c9a6f0"/></radialGradient>`
      + `<mask id="mk"><rect width="200" height="200" fill="#fff"/><circle cx="100" cy="100" r="69" fill="#000"/></mask>`;
    let body = '';
    for (let i = 0; i < 8; i++) body += put(i % 2 ? 's1' : 's2', 22.5 + i * 45, 69);
    body += `<g mask="url(#mk)" transform="rotate(-24 100 100)"><ellipse cx="100" cy="100" rx="89" ry="38" fill="none" stroke="url(#gv)" stroke-width="3"/><ellipse cx="100" cy="100" rx="89" ry="38" fill="none" stroke="#fff" stroke-opacity=".45" stroke-width=".8"/><circle cx="189" cy="100" r="5.2" fill="url(#pe)"/><circle cx="11" cy="100" r="3.2" fill="url(#pe)"/></g>`;
    body += ring('g') + at('ga', 100, 26, { s: 1.0 });
    return [defs, body];
  },
  9() { // flame: swirling tongues of fire, an ember ring
    const defs = grad('g', ['#ffd58a', '#ff8a1f', '#c1200a', '#5a0d05']) + grad('gv', M.flame, 'v') + grad('gr', [...M.flame].reverse()) + grad('in', ['#ffffff', '#fff3ae', '#ffcf3a', '#ffa000'], 'v') + grad('gold', M.gold)
      + `<g id="t1"><path d="${flame(24, 7)}" fill="url(#gv)"/><path d="${flame(14, 3.6)}" fill="url(#in)"/></g><g id="t2"><path d="${flame(36, 9.5)}" fill="url(#gv)"/><path d="${flame(21, 5)}" fill="url(#in)"/></g>`;
    let body = '';
    for (let i = 0; i < 20; i++) { const a = i * 18, up = Math.max(0, Math.cos(a * Math.PI / 180)), len = 14 + 17 * up, big = i % 2 === 0, lean = a === 0 ? 0 : a < 180 ? -13 : a === 180 ? 0 : 13; body += put(big ? 't2' : 't1', a, 66, { rot: lean, s: len / (big ? 36 : 24) * (big ? 1 : 1.1) }); }
    body += ring('g');
    return [defs, body];
  },
  10() { // diamond: a fine ice crown of thin shards, sparkles, a snowflake on top
    const defs = commonDefs('diamond') + `<g id="s1"><path d="${shard(13, 3.8)}" fill="url(#gv)"/></g><g id="s2"><path d="${shard(22, 4.8)}" fill="url(#g)"/><path d="M0 0L0-18" stroke="#fff" stroke-opacity=".7" stroke-width=".7"/></g><g id="sp"><path d="${sparkle(7)}" fill="#fff"/></g>`
      + `<g id="sn"><circle r="10" fill="url(#g)" stroke="url(#gr)" stroke-width="1.3"/><path d="M0-8V8M-7-4L7 4M-7 4L7-4" stroke="#fff" stroke-width="1.6" stroke-linecap="round"/><circle r="2" fill="#fff"/></g>`;
    let body = '';
    for (let i = 0; i < 28; i++) body += put(i % 2 ? 's1' : 's2', i * (360 / 28), 70.5);
    body += ring('g') + at('sn', 100, 22) + put('sp', 40, 86, { rot: -40 }) + put('sp', 150, 88, { rot: -150, s: .8 }) + put('sp', 250, 85, { rot: -250, s: .7 });
    return [defs, body];
  },
  11() { // crystal: a two-layer cluster of violet shards, a gem at the bottom
    const defs = commonDefs('crystal') + grad('gd2', ['#b9a3f2', '#7a56d6', '#4b2c9d', '#2a1668'], 'v') + gemGrads(['amethyst']) + gemDefs('gg', 'gm-amethyst')
      + `<g id="b"><path d="${shard(27, 8.4)}" fill="url(#gd2)"/></g><g id="fr"><path d="${shard(22, 6.8)}" fill="url(#gv)"/><path d="M0 0L0-18M-3.5-1L-1.6-12" stroke="#fff" stroke-opacity=".45" stroke-width=".7"/></g>`;
    let body = '';
    for (let i = 0; i < 12; i++) body += put('b', i * 30, 69);
    for (let i = 0; i < 12; i++) body += put('fr', 15 + i * 30, 71);
    body += ring('g') + at('gg', 100, 173, { s: 1.4 });
    return [defs, body];
  },
  12() { // phoenix: gold-and-orange wings rising, a crest of flame, small tail flames
    const defs = grad('g', M.phoenix) + grad('gv', M.phoenix, 'v') + grad('gr', [...M.phoenix].reverse()) + grad('fl', ['#fff3b8', '#ffc83a', '#ff6a00', '#c1200a'], 'v') + grad('gold', M.gold)
      + `<g id="f1"><path d="${feather(32, 8.5)}" fill="url(#gv)" stroke="url(#gr)" stroke-width=".8"/></g><g id="f2"><path d="${feather(54, 12)}" fill="url(#gv)" stroke="url(#gr)" stroke-width=".8"/><path d="M0-34C1.5-38 1.5-45 .6-52" stroke="#d6420f" stroke-width="3.4" fill="none" stroke-linecap="round"/></g>`
      + `<g id="fl"><path d="${flame(30, 8)}" fill="url(#fl)"/></g><g id="fs"><path d="${flame(16, 5)}" fill="url(#fl)"/></g>`;
    let body = '';
    body += wings('f2', 54, [[120, 98, .8], [111, 82, .95], [102, 66, 1], [93, 50, 1], [84, 34, 1], [75, 20, .95], [66, 8, .8]]);
    body += put('fs', 150, 68, { rot: 25 }) + put('fs', 210, 68, { rot: -25, flip: true }) + put('fs', 180, 68);
    body += ring('g') + put('fl', 0, 66, { s: .95 }) + put('fl', -24, 66, { s: .7, rot: -10 }) + put('fl', 24, 66, { s: .7, rot: 10 });
    return [defs, body];
  },
  13() { // royal: a jewelled crown over a ring of pearls and a ribbon
    const defs = commonDefs('gold') + gemGrads(['ruby', 'sapphire', 'emerald', 'pearl']) + gemDefs('gr1', 'gm-ruby') + gemDefs('gs1', 'gm-sapphire') + gemDefs('ge1', 'gm-emerald')
      + `<radialGradient id="pe" cx=".35" cy=".3" r=".8"><stop offset="0" stop-color="#fff"/><stop offset="1" stop-color="#d8cfc7"/></radialGradient><g id="p"><circle r="3.6" fill="url(#pe)" stroke="url(#gr)" stroke-width=".8"/></g>`
      + `<g id="crown"><path d="${bigCrown(50, 34)}" fill="url(#gv)" stroke="url(#gr)" stroke-width="1.7" stroke-linejoin="round"/><path d="M-25-5H25" stroke="#fff" stroke-opacity=".5" stroke-width="1.3"/><circle cx="-15" cy="-30" r="2.8" fill="url(#pe)"/><circle cx="0" cy="-35" r="3.3" fill="url(#pe)"/><circle cx="15" cy="-30" r="2.8" fill="url(#pe)"/></g>`
      + `<g id="rib"><path d="M-30-3C-18 6-8 6 0 3C8 6 18 6 30-3L26 10C16 15 8 14 0 11C-8 14-16 15-26 10Z" fill="url(#gv)" stroke="url(#gr)" stroke-width="1.2" stroke-linejoin="round"/></g>`;
    let body = '';
    for (let i = 0; i < 12; i++) { const a = i * 30; if (Math.abs(((a + 180) % 360) - 180) < 40) continue; body += put('p', a, 76); }
    body += ring('g') + at('rib', 100, 174) + at('gr1', 100, 174, { s: .9 }) + at('crown', 100, 39)
      + at('gs1', 90, 25, { s: .5 }) + at('gr1', 100, 18, { s: .65 }) + at('gs1', 110, 25, { s: .5 })
      + put('ge1', 90, 67, { s: .75 }) + put('ge1', 270, 67, { s: .75 });
    return [defs, body];
  },
  14() { // Jumbo Legend: big purple-and-gold wings, a crown, gems on the ring, stars
    const defs = commonDefs('legend') + gemGrads(['amethyst', 'ruby', 'diamond']) + gemDefs('ga', 'gm-amethyst') + gemDefs('gr1', 'gm-ruby') + gemDefs('gd', 'gm-diamond')
      + `<g id="f1"><path d="${feather(34, 9)}" fill="url(#gv)" stroke="url(#gold)" stroke-width="1.1"/></g><g id="f2"><path d="${feather(58, 12.5)}" fill="url(#gv)" stroke="url(#gold)" stroke-width="1.1"/><path d="M0-3C1.5-20 1.5-40 .6-54" stroke="#fff" stroke-opacity=".5" stroke-width="1" fill="none"/></g>`
      + `<g id="crown"><path d="${bigCrown(44, 30)}" fill="url(#gold)" stroke="#8a5a00" stroke-width="1.4" stroke-linejoin="round"/><path d="M-22-4H22" stroke="#fff" stroke-opacity=".55" stroke-width="1.2"/></g><g id="sp"><path d="${sparkle(8)}" fill="#fff"/></g><g id="st"><path d="${star(6)}" fill="#fff" stroke="url(#gold)" stroke-width="1"/></g>`;
    let body = '';
    body += wings('f2', 58, [[122, 100, .8], [113, 86, .95], [104, 70, 1], [95, 55, 1], [86, 40, 1], [77, 26, 1], [68, 13, .9], [59, 3, .75]]);
    body += ring('g') + at('crown', 100, 38) + at('gr1', 100, 22, { s: .7 }) + at('ga', 88, 29, { s: .45 }) + at('ga', 112, 29, { s: .45 })
      + put('gd', 90, 67, { s: .8 }) + put('gd', 270, 67, { s: .8 }) + put('ga', 180, 67, { s: .9 })
      + put('sp', 22, 90, { rot: -22, s: .8 }) + put('sp', 338, 90, { rot: 22, s: .8 });
    return [defs, body];
  },
};

// ---------- the rings (small sizes): the same material, a gradient ring with one gem ----------
const ringSvg = n => {
  const m = MATERIAL[n];
  const gemKey = { 1: 'gold', 2: 'diamond', 3: 'ruby', 4: 'emerald', 5: 'sapphire', 6: 'ruby', 7: 'diamond', 8: 'amethyst', 9: 'gold', 10: 'diamond', 11: 'amethyst', 12: 'ruby', 13: 'ruby', 14: 'amethyst' }[n];
  const defs = grad('g', M[m]) + grad('gr', [...M[m]].reverse()) + grad('gold', M.gold) + grad('gm', GEM[gemKey]);
  const body = ring('g', 12, 67) + `<g transform="translate(100 169)"><path d="${gemPath(10)}" fill="url(#gm)" stroke="url(#gold)" stroke-width="2.2" stroke-linejoin="round"/><path d="M-4-4.5L0-8" stroke="#fff" stroke-opacity=".8" stroke-width="1.6" stroke-linecap="round"/></g>`
    + (n >= 10 ? `<path d="${sparkle(8)}" transform="translate(150 50)" fill="#fff"/>` : '') + (n === 14 ? `<g transform="translate(100 31)"><path d="${bigCrown(30, 22)}" fill="url(#gold)" stroke="#8a5a00" stroke-width="1.6" stroke-linejoin="round"/></g>` : '');
  return min(svg(defs, body));
};

const frameSvg = n => { const [defs, body] = FRAMES[n](); return min(svg(defs, body)); };

// the legend: the book-14 frame inside a halo of rays with extra stars (given for finishing all the books)
const legendSvg = () => {
  const [defs, body] = FRAMES[14]();
  let rays = '';
  for (let i = 0; i < 24; i++) { const a = i * 15, [x1, y1] = polar(a - 3, 73), [x2, y2] = polar(a, i % 2 ? 90 : 98), [x3, y3] = polar(a + 3, 73); rays += `M${f(x1)} ${f(y1)}L${f(x2)} ${f(y2)}L${f(x3)} ${f(y3)}Z`; }
  const halo = `<path d="${rays}" fill="url(#gold)" fill-opacity=".55"/>`;
  const extra = put('st', 130, 90) + put('st', 230, 90);
  return min(svg(defs, halo + body + extra));
};

// ---------- the sample avatar of the preview and the award tiles ----------
const sampleAvatar = min(`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 124 124"><defs><linearGradient id="bg" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#ffe3b3"/><stop offset="1" stop-color="#ffb35c"/></linearGradient></defs><circle cx="62" cy="62" r="62" fill="url(#bg)"/><circle cx="62" cy="52" r="22" fill="#f6c9a0"/><path d="M38 50C38 28 86 28 86 50C78 40 46 40 38 50Z" fill="#5b3a29"/><circle cx="54" cy="55" r="2.6" fill="#3b2a20"/><circle cx="70" cy="55" r="2.6" fill="#3b2a20"/><path d="M55 64Q62 70 69 64" fill="none" stroke="#b5503c" stroke-width="2.4" stroke-linecap="round"/><path d="M20 124C22 92 102 92 104 124Z" fill="#4c8bf5"/></svg>`);

const trophy = `<g id="tr"><path d="M-14-16H14V-6C14 4 7 10 0 10C-7 10-14 4-14-6Z" fill="url(#g)" stroke="url(#gr)" stroke-width="1.5" stroke-linejoin="round"/><path d="M-14-12C-24-12-24 0-13 1M14-12C24-12 24 0 13 1" fill="none" stroke="url(#g)" stroke-width="3" stroke-linecap="round"/><path d="M-3 10H3V18H-3ZM-10 18H10V24H-10Z" fill="url(#gr)"/><path d="M-8-13V-4" stroke="#fff" stroke-opacity=".7" stroke-width="2" stroke-linecap="round"/></g>`;

const awardSvg = n => {
  if (n === 15) { // the Jumbo Legend award: the legend frame with a golden trophy
    const [defs, body] = FRAMES[14](); const leg = legendSvg();
    return min(`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 200"><defs>${defs}<radialGradient id="med" cx=".35" cy=".3" r=".9"><stop offset="0" stop-color="#4a2486"/><stop offset="1" stop-color="#1b0a3d"/></radialGradient>${trophy}</defs>`
      + leg.replace(/^<svg[^>]*><defs>.*?<\/defs>/, '').replace(/<\/svg>$/, '') + `<circle cx="100" cy="100" r="60" fill="url(#med)"/><circle cx="100" cy="100" r="56" fill="none" stroke="url(#gold)" stroke-width="2.2"/>` + `<use href="#tr" transform="translate(100 100) scale(1.9)"/></svg>`);
  }
  const m = MATERIAL[n], [defs, body] = FRAMES[n]();
  const med = `<radialGradient id="med" cx=".35" cy=".3" r=".9"><stop offset="0" stop-color="${M[m][1]}"/><stop offset=".6" stop-color="${M[m][2]}"/><stop offset="1" stop-color="${M[m][3]}"/></radialGradient>`;
  const num = `<text x="100" y="${n > 9 ? 118 : 120}" text-anchor="middle" font-family="Nunito,Arial,Helvetica,sans-serif" font-weight="900" font-size="${n > 9 ? 48 : 58}" fill="#fff" stroke="#000" stroke-opacity=".35" stroke-width="3" paint-order="stroke">${n}</text>`;
  return min(svg(defs + med, body + `<circle cx="100" cy="100" r="60" fill="url(#med)"/><circle cx="100" cy="100" r="56" fill="none" stroke="#fff" stroke-opacity=".5" stroke-width="1.6"/><path d="M52 78A52 52 0 0 1 100 48" fill="none" stroke="#fff" stroke-opacity=".5" stroke-width="3" stroke-linecap="round"/>` + num));
};

// ---------- write everything ----------
const pad = n => String(n).padStart(2, '0');
const files = {};
for (let n = 1; n <= 14; n++) { files[`frame-${pad(n)}.svg`] = frameSvg(n); files[`ring-${pad(n)}.svg`] = ringSvg(n); files[`award-${pad(n)}.svg`] = awardSvg(n); }
files['frame-legend.svg'] = legendSvg();
files['award-legend.svg'] = awardSvg(15);
files['sample-avatar.svg'] = sampleAvatar;
for (const [name, text] of Object.entries(files)) writeFileSync(join(OUT, name), text);

// ---------- the preview sheets ----------
const cell = (n, big, dark) => {
  const id = n === 15 ? 'legend' : pad(n);
  const fr = n === 15 ? 'frame-legend.svg' : `frame-${pad(n)}.svg`, rg = n === 15 ? 'ring-14.svg' : `ring-${pad(n)}.svg`;
  const av = s => `<div class="av" style="width:${s}px;height:${s}px"><img class="a" src="sample-avatar.svg" style="width:${s * .62}px;height:${s * .62}px"><img class="fr" src="${s <= 48 ? rg : fr}" style="width:${s}px;height:${s}px"></div>`;
  return `<div class="bx ${dark ? 'dk' : 'lt'}">${big ? av(160) : ''}${av(64)}${av(36)}</div>`;
};
const label = n => n === 15 ? 'Legend award · Mythic · purple + gold + halo' : `Book ${n} · ${NAMES[n]} · ${RARITY[n]}`;
const css = `body{margin:0;padding:18px;font-family:Nunito,Arial,sans-serif;background:#eceff3;color:#222}h1{font-size:20px;margin:0 0 10px}h2{font-size:16px;margin:22px 0 8px}.grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:10px}.c{background:#fff;border-radius:12px;padding:8px;box-shadow:0 1px 3px #0002}.t{font-size:12px;font-weight:700;margin:4px 2px 6px}.pair{display:flex;flex-direction:column;gap:6px}.bx{flex:1;border-radius:10px;padding:8px;display:flex;align-items:center;justify-content:space-around;gap:6px;min-height:150px}.dk{background:#15171c}.lt{background:#f6f1e7}.av{position:relative;flex:none}.av .a{position:absolute;left:19%;top:19%;border-radius:50%}.av .fr{position:absolute;left:0;top:0}.small .bx{min-height:84px}.small .grid{grid-template-columns:repeat(5,minmax(0,1fr))}.aw{display:grid;grid-template-columns:repeat(5,minmax(0,1fr));gap:8px}.ac{border-radius:10px;padding:8px;text-align:center;font-size:12px;font-weight:700}.ac img{width:112px;height:112px}.ac .n{margin-top:2px}`;
const page = (title, body, small) => `<!doctype html><html lang="en"><head><meta charset="utf-8"><title>${title}</title><style>${css}</style></head><body class="${small ? 'small' : ''}"><h1>${title}</h1>${body}</body></html>`;
const frames = Array.from({ length: 15 }, (_, i) => i + 1);
const awardCell = (n, dark) => `<div class="ac ${dark ? 'dk' : 'lt'}" style="${dark ? 'color:#eee' : ''}"><img src="${n === 15 ? 'award-legend.svg' : `award-${pad(n)}.svg`}"><div class="n">${n === 15 ? 'Jumbo Legend' : `Книга ${n}: Завершена`}</div></div>`;
writeFileSync(join(OUT, 'preview.html'), page('Jumbo avatar frames, preview (not integrated)',
  `<div class="grid">${frames.map(n => `<div class="c"><div class="t">${label(n)}</div><div class="pair">${cell(n, true, true)}${cell(n, true, false)}</div></div>`).join('')}</div>`
  + `<h2>Awards (the same frame, a coloured medallion with the book number; the Legend award has a trophy)</h2><div class="aw">${frames.map(n => awardCell(n, true)).join('')}</div><div class="aw" style="margin-top:8px">${frames.map(n => awardCell(n, false)).join('')}</div>`));
writeFileSync(join(OUT, 'preview-small.html'), page('Small sizes: 64 px (full frame) and 36 px (ring)',
  `<div class="grid">${frames.map(n => `<div class="c"><div class="t">${label(n)}</div><div class="pair">${cell(n, false, true)}${cell(n, false, false)}</div></div>`).join('')}</div>`, true));

const sizes = Object.entries(files).filter(([k]) => /frame|ring|award/.test(k)).map(([k, v]) => `${k} ${v.length}`);
console.log(sizes.join('\n'));
