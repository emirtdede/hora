// Generates the embedded translation catalogs from the English source.
// Usage: node scripts/translate-locales.mjs [--force] [--only tr,de]
import fs from "node:fs";
import path from "node:path";

const root = path.resolve(import.meta.dirname, "..");
const source = JSON.parse(fs.readFileSync(path.join(root, "scripts", "locales", "en.json"), "utf8"));
const languages = JSON.parse(fs.readFileSync(path.join(root, "scripts", "languages.json"), "utf8"));
const outDir = path.join(root, "src", "Hora.Core", "Localization", "locales");
fs.mkdirSync(outDir, { recursive: true });

const args = process.argv.slice(2);
const force = args.includes("--force");
const onlyArg = args.find((a) => a.startsWith("--only="));
const only = onlyArg ? new Set(onlyArg.slice(7).split(",")) : null;
const keysArg = args.find((a) => a.startsWith("--keys="));
const selectedKeys = keysArg ? new Set(keysArg.slice(7).split(",").filter(Boolean)) : null;

const googleCode = { "zh-tw": "zh-TW", fil: "tl", no: "no", he: "iw" };
const entries = Object.entries(source).filter(([key]) => !selectedKeys || selectedKeys.has(key));
const BATCH = 8;
const REQUEST_DELAY_MS = Number(process.env.HORA_LOCALE_DELAY_MS ?? 400);
const MAX_ATTEMPTS = Number(process.env.HORA_LOCALE_ATTEMPTS ?? 6);

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

// Scripts that do not separate words with spaces render a much shorter string
// than English, so the length heuristic only applies to spaced scripts.
const letterCount = (text) => (String(text).match(/\p{L}/gu) ?? []).length;

const placeholders = (text) =>
  [...String(text).matchAll(/\{(\d+)\}/g)].map((match) => match[0]).sort().join(",");

// A translation that lost every word (for example ". , , .") is worse than no
// translation at all: it hides the English fallback and ships broken copy.
function isDegenerate(value, original, target) {
  if (typeof value !== "string" || value.trim().length === 0) return true;
  const source = letterCount(original);
  if (source === 0) return false;
  const translated = letterCount(value);
  if (translated === 0) return true;
  // Letter counts are not comparable across scripts: Hindi "Yesterday" is the
  // complete word "कल", two graphemes against nine Latin letters. The length
  // heuristic only applies when the translation itself is Latin script.
  if (!/\p{Script=Latin}/u.test(value)) return false;
  // Short labels can legitimately collapse to one or two letters (Polish
  // "About" is "O"), so the ratio only applies to full sentences.
  if (source < 8) return false;
  return translated < source * 0.25;
}

// The marker splitting can fail when the service alters a marker. A value that
// still carries a marker or a neighbouring entry's placeholder is contaminated.
function isContaminated(value, original) {
  const text = String(value ?? "");
  if (/<<<|>>>/.test(text)) return true;
  const allowed = new Set(placeholders(original).split(",").filter(Boolean));
  return [...text.matchAll(/\{(\d+)\}/g)].some((match) => !allowed.has(match[0]));
}

function needsWork(value, original, target) {
  return isDegenerate(value, original, target)
    || placeholders(value) !== placeholders(original)
    || /<<<|>>>/.test(String(value ?? ""));
}

function fix(text, original) {
  let value = String(text ?? "").trim();
  value = value.replace(/\u2014|\u2013|\u2015/g, "-");
  value = value.replace(/[\u2700-\u27bf\u{1f000}-\u{1faff}\ufe0f]/gu, "");
  // CJK punctuation already carries its trailing space; a space inserted after
  // it reads as a gap in Japanese, Chinese, and Korean sentences.
  value = value.replace(/([。、，：；！？」』】）])\s+/gu, "$1");
  // Drop marker remnants and placeholders the English string does not have;
  // appending missing placeholders keeps the format contract intact.
  value = value.replace(/<{2,}\s*\d+\s*>{2,}/g, " ");
  const tokens = [...original.matchAll(/\{(\d+)\}/g)].map((m) => m[0]);
  const allowed = new Set(tokens);
  value = value.replace(/\{(\d+)\}/g, (match) => (allowed.has(match) ? match : ""));
  for (const token of tokens) if (!value.includes(token)) value += " " + token;
  return value.replace(/\s+/g, " ").trim();
}

async function requestTranslation(target, payload) {
  const url =
    "https://translate.googleapis.com/translate_a/single" +
    `?client=gtx&sl=en&tl=${encodeURIComponent(googleCode[target] ?? target)}&dt=t&q=${encodeURIComponent(payload)}`;
  const res = await fetch(url, { headers: { "user-agent": "Hora-Locale-Builder/1.0" } });
  if (!res.ok) {
    const retryAfter = Number(res.headers.get("retry-after") ?? 0);
    const error = new Error(`HTTP ${res.status}`);
    error.status = res.status;
    error.retryAfterMs = Number.isFinite(retryAfter) && retryAfter > 0 ? retryAfter * 1000 : 0;
    throw error;
  }

  const data = await res.json();
  return (data?.[0] ?? []).map((part) => part?.[0] ?? "").join("");
}

// Translated markers keep batches in order, but the service occasionally drops one.
// A failed entry must not discard the rest of its batch: successful rows are
// returned and the caller retries only the keys that are still unfinished.
async function translateBatch(target, batch) {
  const marker = "<<<";
  const payload = batch.map(([, value], index) => `${marker}${index}>>>${value}`).join("\n");
  const text = await requestTranslation(target, payload);
  const chunks = text.split(/<<<\s*(\d+)\s*>>+/).slice(1);
  const map = new Map();
  for (let i = 0; i + 1 < chunks.length; i += 2) map.set(Number(chunks[i]), chunks[i + 1]);

  const result = [];
  let failures = 0;
  for (let index = 0; index < batch.length; index += 1) {
    const [key, value] = batch[index];
    if (map.has(index)) {
      const raw = map.get(index);
      const candidate = fix(raw, value);
      // A batch chunk that swallowed its neighbour can still look translated.
      // Reject it when it carries foreign placeholders or is implausibly long.
      const overlong = letterCount(raw) > letterCount(value) * 2.2 + 12;
      if (!isContaminated(raw, value) && !overlong && !isDegenerate(candidate, value, target)) {
        result.push([key, candidate]);
        continue;
      }
    }

    try {
      const single = await requestTranslation(target, value);
      await sleep(REQUEST_DELAY_MS);
      const candidate = fix(single, value);
      if (isDegenerate(candidate, value, target)) throw new Error("Degenerate translation");
      result.push([key, candidate]);
    } catch {
      failures += 1;
    }
  }

  return { result, failures };
}

async function translateLanguage(lang) {
  const file = path.join(outDir, `${lang.id}.json`);
  let existing = null;
  if (fs.existsSync(file)) {
    try {
      existing = JSON.parse(fs.readFileSync(file, "utf8"));
    } catch {
      existing = null;
    }
  }

  const result = force ? {} : { ...(existing ?? {}) };
  // Empty, degenerate, contaminated, or placeholder-drifted values are
  // unfinished work, never finished keys.
  if (existing && !force && !selectedKeys && entries.every(([key, value]) => !needsWork(result[key], value, lang.id)))
  {
    console.log(`skip ${lang.id}`);
    return true;
  }

  const batches = [];
  // --keys means "rewrite exactly these strings", even when the old value passed
  // the structural checks.
  const pending = entries.filter(([key, value]) => selectedKeys !== null || needsWork(result[key], value, lang.id));
  for (let i = 0; i < pending.length; i += BATCH) batches.push(pending.slice(i, i + BATCH));
  for (const batch of batches) {
    let remainingBatch = batch;
    for (let attempt = 0; attempt < MAX_ATTEMPTS && remainingBatch.length > 0; attempt += 1) {
      let translated = [];
      try {
        const response = await translateBatch(lang.id, remainingBatch);
        translated = response.result;
      } catch {
        // A rejected batch is retried after a cooldown; nothing is lost.
      }

      for (const [key, value] of translated) result[key] = value;

      // Persist after every attempt so an interrupted run keeps its progress.
      writeCatalog(file, result);
      remainingBatch = remainingBatch.filter(([key, value]) => needsWork(result[key], value, lang.id));
      if (remainingBatch.length === 0) {
        break;
      }

      await sleep(Math.max(3000 * (attempt + 1), REQUEST_DELAY_MS * 4));
    }

    await sleep(REQUEST_DELAY_MS);
  }

  writeCatalog(file, result);
  const remaining = entries.filter(([key, value]) => needsWork(result[key], value, lang.id)).length;
  console.log(`${remaining === 0 ? "done" : `partial(${remaining})`} ${lang.id} ${Object.keys(result).length}/${entries.length}`);
  return remaining === 0;
}

function writeCatalog(file, result) {
  const ordered = {};
  for (const key of Object.keys(source)) {
    if (result[key]) ordered[key] = result[key];
  }

  fs.writeFileSync(file, JSON.stringify(ordered, null, 2) + "\n", "utf8");
}

const queue = languages.filter((lang) => lang.id !== "en" && (!only || only.has(lang.id)));
// English catalog is the source of truth, emitted first so a partial run is still buildable.
fs.writeFileSync(path.join(outDir, "en.json"), JSON.stringify(source, null, 2) + "\n", "utf8");

for (const lang of queue) {
  let ok = await translateLanguage(lang);
  for (let pass = 0; pass < 3 && !ok; pass += 1) {
    console.log(`retrying ${lang.id} after cooldown`);
    await sleep(20000);
    ok = await translateLanguage(lang);
  }

  if (!ok) {
    console.log(`incomplete ${lang.id}`);
  }
}

console.log("all locales ready");
