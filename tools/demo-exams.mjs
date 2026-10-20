// Gives the 30 demo pupils (demo_a2018_01..30) a few exam results through the public API.
// The pupils' passwords were random and are gone, so they are set again here to a fresh random one that lives only in this
// process (sent to the server over stdin, never printed or written to a file).
import crypto from 'node:crypto';
import { spawnSync } from 'node:child_process';
import os from 'node:os';
import path from 'node:path';

const BASE = 'https://jumbo.uz/api';
const PREFIX = 'demo_a2018_';
const COUNT = 30;
const password = crypto.randomBytes(12).toString('base64url');

const reset = spawnSync('ssh', ['-i', path.join(os.homedir(), '.ssh', 'jumbo_deploy'), 'ubuntu@46.8.195.183',
  `read -r PW; for i in $(seq -w 1 ${COUNT}); do docker exec -e NEW_PASSWORD="$PW" jumbo-api dotnet Jumbo.Api.dll --reset-password ${PREFIX}$i > /dev/null 2>&1 || echo "reset failed: $i"; done; echo reset-done`],
  { input: password + '\n', encoding: 'utf8' });
console.log((reset.stdout || '').trim(), (reset.stderr || '').trim().split('\n').filter(l => !/libgssapi/.test(l)).join('\n'));
if (!/reset-done/.test(reset.stdout || '')) process.exit(1);

async function api(p, { method = 'GET', body, token } = {}) {
  const res = await fetch(BASE + p, {
    method,
    headers: { 'Content-Type': 'application/json', ...(token ? { 'X-Access-Token': token } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  });
  const text = await res.text();
  if (!res.ok) throw new Error(`${method} ${p} -> ${res.status} ${text.slice(0, 160)}`);
  return text ? JSON.parse(text) : null;
}

const pick = (arr, n) => [...arr].sort(() => Math.random() - 0.5).slice(0, n);
const rnd = (a, b) => a + Math.floor(Math.random() * (b - a + 1));

// words of book 1 (English + Russian) so that the wrong answers show up in the team report as "most often missed"
const langs = await api('/languages');
const lid = code => langs.find(l => (l.shorName ?? '').toLowerCase() === code)?.languageId;
const en = lid('en'), ru = lid('ru');
const subjects = await api('/subjects/by-book/1');
const pool = [];
for (const s of subjects.slice(0, 12)) {
  const e = await api(`/words/by-subject/all?subjectId=${s.subjectId}&languageId=${en}`);
  const r = await api(`/words/by-subject/all?subjectId=${s.subjectId}&languageId=${ru}`);
  const byNumber = new Map(r.map(w => [w.wordNumber, w.wordName]));
  for (const w of e) pool.push({ word: w.wordName, translation: byNumber.get(w.wordNumber) ?? null });
}

let results = 0, failed = 0;
for (let i = 1; i <= COUNT; i++) {
  const username = PREFIX + String(i).padStart(2, '0');
  try {
    const login = await api('/users/login', { method: 'POST', body: { username, password } });
    const token = login.accessToken;
    // a stronger or weaker pupil, 1-3 exams: mostly book 1, sometimes book 2
    const level = 45 + Math.random() * 50;
    const exams = rnd(1, 3);
    for (let e = 0; e < exams; e++) {
      const total = [10, 20, 20, 30][rnd(0, 3)];
      const correct = Math.max(0, Math.min(total, Math.round(total * (level + rnd(-12, 12)) / 100)));
      const book = Math.random() < 0.8 ? 1 : 2;
      await api('/exam-results', {
        method: 'POST', token,
        body: { bookNumber: book, totalCount: total, correctCount: correct, mistakes: pick(pool, total - correct) },
      });
      results++;
    }
  } catch (err) {
    failed++;
    console.log(`${username}: FAILED ${err.message}`);
  }
}
console.log(`exam results added: ${results}, pupils failed: ${failed}`);
