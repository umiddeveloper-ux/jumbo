// Builds the mobile app's image assets from the website's sources, so both clients share one look:
//   - tiger_<mood>.svg / tiger_head_<mood>.svg   (static poses of Components/Shared/Tiger.razor; animation is done in code)
//   - app icon / splash                          (wwwroot/favicon.svg)
// Run:  node tools/gen-mobile-assets.mjs
import fs from 'node:fs';
import path from 'node:path';

const root = path.resolve(import.meta.dirname, '..');
const web = path.join(root, 'JumboWeb/JumboWeb');
const app = path.join(root, 'Jumbo');
const imgDir = path.join(app, 'Resources/Images');
fs.mkdirSync(imgDir, { recursive: true });
fs.mkdirSync(path.join(app, 'Resources/Raw'), { recursive: true });

// ───────────── tiger ─────────────
const razor = fs.readFileSync(path.join(web, 'Components/Shared/Tiger.razor'), 'utf8');
let body = razor.slice(razor.indexOf('<ellipse class="tg-shadow"'), razor.indexOf('</svg>'));
body = body.replace(/@\*[\s\S]*?\*@/g, '');   // razor comments

const HIDDEN_BY_DEFAULT = ['tg-eyes-happy', 'tg-brows', 'tg-tear', 'tg-mouth-open', 'tg-mouth-sad', 'tg-sparkles'];
const MOODS = {
  idle: { show: [], hide: ['tg-mouth-open', 'tg-mouth-sad'] },
  wave: { show: ['tg-mouth-open'], hide: ['tg-mouth-smile'], transforms: { 'tg-arm-r': 'rotate(-143 132 160)' } },
  cheer: {
    show: ['tg-eyes-happy', 'tg-mouth-open', 'tg-sparkles'], hide: ['tg-eyes', 'tg-mouth-smile'],
    transforms: { 'tg-arm-l': 'rotate(158 68 160)', 'tg-arm-r': 'rotate(-158 132 160)' }
  },
  sad: {
    show: ['tg-brows', 'tg-tear', 'tg-mouth-sad'], hide: ['tg-mouth-smile'],
    transforms: { 'tg-tail': 'rotate(-12 136 192)', 'tg-head': 'translate(0 4) rotate(-5 64 112)' }
  }
};

function tigerSvg(mood, headOnly) {
  const m = MOODS[mood];
  const hiddenSet = new Set(HIDDEN_BY_DEFAULT);
  m.show.forEach((c) => hiddenSet.delete(c));
  m.hide.forEach((c) => hiddenSet.add(c));
  let b = body.replace(/<(g|path|circle|ellipse)\b([^>]*?)class="([^"]*)"([^>]*?)(\/?)>/g, (all, tag, pre, cls, post, selfClose) => {
    const classes = cls.split(/\s+/);
    let extra = classes.some((c) => hiddenSet.has(c)) ? ' display="none"' : '';
    for (const [c, t] of Object.entries(m.transforms ?? {})) if (classes.includes(c)) extra += ` transform="${t}"`;
    return `<${tag}${pre}${post}${extra}${selfClose}>`;
  });
  if (headOnly) b = b.replace(/<ellipse[^>]*opacity="0.12"[^>]*\/>/, '');
  const vb = headOnly ? '34 12 132 124' : '0 0 200 226';
  const size = headOnly ? '132 124' : '200 226';
  return `<?xml version="1.0" encoding="UTF-8"?>\n<svg xmlns="http://www.w3.org/2000/svg" viewBox="${vb}" width="${size.split(' ')[0]}" height="${size.split(' ')[1]}">\n${b.trim()}\n</svg>\n`;
}

for (const mood of Object.keys(MOODS)) {
  fs.writeFileSync(path.join(imgDir, `tiger_${mood}.svg`), tigerSvg(mood, false));
  fs.writeFileSync(path.join(imgDir, `tiger_head_${mood}.svg`), tigerSvg(mood, true));
}

// ───────────── icon + splash ─────────────
const fav = fs.readFileSync(path.join(web, 'wwwroot/favicon.svg'), 'utf8');
// background = the blue rounded square only, foreground = the tiger without it (adaptive icons)
const bgRect = /<rect width="128" height="128" rx="28" fill="url\(#jbg\)"\/>/;
if (!bgRect.test(fav)) throw new Error('favicon background rect not found');
const defs = fav.match(/<defs>[\s\S]*?<\/defs>/)[0];
const fg = fav.replace(bgRect, '').replace(/<title>[\s\S]*?<\/title>\s*/, '');
fs.writeFileSync(path.join(app, 'Resources/AppIcon/appiconfg.svg'), fg.replace('width="128" height="128" viewBox="0 0 128 128"', 'width="128" height="128" viewBox="-14 -14 156 156"'));
fs.writeFileSync(path.join(app, 'Resources/AppIcon/appicon.svg'),
  `<?xml version="1.0" encoding="UTF-8"?>\n<svg width="128" height="128" viewBox="0 0 128 128" xmlns="http://www.w3.org/2000/svg">\n${defs}\n  <rect width="128" height="128" fill="url(#jbg)"/>\n</svg>\n`);
fs.writeFileSync(path.join(app, 'Resources/Splash/splash.svg'), fg.replace('width="128" height="128" viewBox="0 0 128 128"', 'width="128" height="128" viewBox="0 0 128 128"'));
console.log('done:', Object.keys(MOODS).length * 2, 'tiger images');
