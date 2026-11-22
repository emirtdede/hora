// Quality gate for the embedded localization catalogs.
// Fails when a catalog is missing, a key is absent, a dash or emoji leaks in,
// or a placeholder set drifts from English.
import fs from "node:fs";
import path from "node:path";

const root = path.resolve(import.meta.dirname, "..");
const source = JSON.parse(fs.readFileSync(path.join(root, "scripts", "locales", "en.json"), "utf8"));
const languages = JSON.parse(fs.readFileSync(path.join(root, "scripts", "languages.json"), "utf8"));
const dir = path.join(root, "src", "Hora.Core", "Localization", "locales");
const keys = Object.keys(source);

const dash = /[\u2012\u2013\u2014\u2015]/u;
const emoji = /[\u2700-\u27bf\u{1f000}-\u{1faff}\ufe0f]/u;
const placeholders = (value) => (value.match(/\{\d+\}/g) ?? []).sort().join(",");

// Scripts without spaces are naturally shorter than English.
const letterCount = (text) => (String(text).match(/\p{L}/gu) ?? []).length;

// A string that lost all of its words is a broken translation, not a short one.
function isDegenerate(value, original, id) {
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

const problems = [];
for (const language of languages) {
  const file = path.join(dir, `${language.id}.json`);
  if (!fs.existsSync(file)) {
    problems.push(`${language.id}: catalog file missing`);
    continue;
  }

  const catalog = JSON.parse(fs.readFileSync(file, "utf8"));
  for (const key of keys) {
    const value = catalog[key];
    if (typeof value !== "string" || value.trim().length === 0) {
      problems.push(`${language.id}.${key}: empty`);
      continue;
    }

    if (dash.test(value)) problems.push(`${language.id}.${key}: dash character`);
    if (emoji.test(value)) problems.push(`${language.id}.${key}: emoji`);
    if (isDegenerate(value, source[key], language.id)) {
      problems.push(`${language.id}.${key}: degenerate translation`);
    }
    if (placeholders(value) !== placeholders(source[key])) {
      problems.push(`${language.id}.${key}: placeholder drift`);
    }
  }

  const extra = Object.keys(catalog).filter((key) => !(key in source));
  if (extra.length > 0) problems.push(`${language.id}: unknown keys ${extra.join(",")}`);
}

if (problems.length > 0) {
  console.error(problems.slice(0, 60).join("\n"));
  console.error(`locale gate failed with ${problems.length} findings`);
  process.exit(1);
}

console.log(`locale gate passed: ${languages.length} catalogs, ${keys.length} keys each`);
