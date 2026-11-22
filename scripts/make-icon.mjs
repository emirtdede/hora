// Renders the Hora application icon (multi-resolution ICO) with no external assets.
// Usage: node scripts/make-icon.mjs [--out assets/hora.ico]
import fs from "node:fs";
import path from "node:path";
import zlib from "node:zlib";

const root = path.resolve(import.meta.dirname, "..");
const args = process.argv.slice(2);
const outIndex = args.indexOf("--out");
const out = outIndex >= 0 ? path.resolve(root, args[outIndex + 1]) : path.join(root, "assets", "hora.ico");

const SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256];
const TOP = [0x98, 0xa6, 0xfa];
const BOTTOM = [0x63, 0x76, 0xe8];

const clamp01 = (value) => Math.min(1, Math.max(0, value));

function roundedRectDistance(x, y, left, top, right, bottom, radius) {
  const cx = Math.max(left + radius, Math.min(x, right - radius));
  const cy = Math.max(top + radius, Math.min(y, bottom - radius));
  const dx = x - cx;
  const dy = y - cy;
  return Math.hypot(dx, dy) - radius;
}

function segmentDistance(px, py, ax, ay, bx, by) {
  const vx = bx - ax;
  const vy = by - ay;
  const wx = px - ax;
  const wy = py - ay;
  const length = vx * vx + vy * vy;
  const t = length === 0 ? 0 : clamp01((wx * vx + wy * vy) / length);
  return Math.hypot(px - (ax + t * vx), py - (ay + t * vy));
}

// Draws one icon at the requested size using 4x supersampling for clean edges.
function render(size) {
  const scale = size / 256;
  const inset = 8 * scale;
  const left = inset;
  const top = inset;
  const right = size - inset;
  const bottom = size - inset;
  const radius = 56 * scale;
  const center = size / 2;
  const ringRadius = (118 * scale) / 2;
  const stroke = (18 * scale) / 2;
  const hand = 46 * scale;
  const dot = (18 * scale) / 2;
  const samples = 4;
  const pixels = Buffer.alloc(size * size * 4);

  for (let py = 0; py < size; py += 1) {
    for (let px = 0; px < size; px += 1) {
      let background = 0;
      let glyph = 0;
      for (let sy = 0; sy < samples; sy += 1) {
        for (let sx = 0; sx < samples; sx += 1) {
          const x = px + (sx + 0.5) / samples;
          const y = py + (sy + 0.5) / samples;
          const rectDistance = roundedRectDistance(x, y, left, top, right, bottom, radius);
          if (rectDistance <= 0) {
            background += 1;
          }

          const radial = Math.hypot(x - center, y - center);
          if (Math.abs(radial - ringRadius) <= stroke) {
            glyph += 1;
            continue;
          }

          if (segmentDistance(x, y, center, center, center, center - hand) <= stroke) {
            glyph += 1;
            continue;
          }

          if (segmentDistance(x, y, center, center, center + hand * 0.72, center + hand * 0.42) <= stroke) {
            glyph += 1;
            continue;
          }

          if (Math.hypot(x - center, y - center) <= dot) {
            glyph += 1;
          }
        }
      }

      const total = samples * samples;
      const coverage = background / total;
      const glyphCoverage = glyph / total;
      const offset = (py * size + px) * 4;
      if (coverage <= 0) {
        pixels[offset + 3] = 0;
        continue;
      }

      const mix = clamp01(0.5 - (py - top) / Math.max(1, bottom - top));
      const base = TOP.map((channel, index) => Math.round(channel + (BOTTOM[index] - channel) * mix));
      const color = base.map((channel) => Math.round(channel + (255 - channel) * (glyphCoverage / Math.max(coverage, 1e-6))));
      pixels[offset] = color[0];
      pixels[offset + 1] = color[1];
      pixels[offset + 2] = color[2];
      pixels[offset + 3] = Math.round(coverage * 255);
    }
  }

  return encodePng(size, size, pixels);
}

const CRC_TABLE = (() => {
  const table = new Uint32Array(256);
  for (let index = 0; index < 256; index += 1) {
    let value = index;
    for (let bit = 0; bit < 8; bit += 1) {
      value = value & 1 ? 0xedb88320 ^ (value >>> 1) : value >>> 1;
    }
    table[index] = value >>> 0;
  }
  return table;
})();

function crc32(buffer) {
  let crc = 0xffffffff;
  for (const byte of buffer) {
    crc = CRC_TABLE[(crc ^ byte) & 0xff] ^ (crc >>> 8);
  }
  return (crc ^ 0xffffffff) >>> 0;
}

function chunk(type, data) {
  const length = Buffer.alloc(4);
  length.writeUInt32BE(data.length, 0);
  const name = Buffer.from(type, "ascii");
  const payload = Buffer.concat([name, data]);
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(payload), 0);
  return Buffer.concat([length, payload, crc]);
}

function encodePng(width, height, rgba) {
  const header = Buffer.alloc(13);
  header.writeUInt32BE(width, 0);
  header.writeUInt32BE(height, 4);
  header[8] = 8;
  header[9] = 6;
  const raw = Buffer.alloc((width * 4 + 1) * height);
  for (let row = 0; row < height; row += 1) {
    raw[row * (width * 4 + 1)] = 0;
    rgba.copy(raw, row * (width * 4 + 1) + 1, row * width * 4, (row + 1) * width * 4);
  }

  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk("IHDR", header),
    chunk("IDAT", zlib.deflateSync(raw, { level: 9 })),
    chunk("IEND", Buffer.alloc(0)),
  ]);
}

const pngs = SIZES.map(render);
const header = Buffer.alloc(6);
header.writeUInt16LE(0, 0);
header.writeUInt16LE(1, 2);
header.writeUInt16LE(SIZES.length, 4);
const directory = Buffer.alloc(16 * SIZES.length);
let offset = 6 + directory.length;
SIZES.forEach((size, index) => {
  const entry = directory.subarray(index * 16, (index + 1) * 16);
  const dimension = size >= 256 ? 0 : size;
  entry[0] = dimension;
  entry[1] = dimension;
  entry[2] = 0;
  entry[3] = 0;
  entry.writeUInt16LE(1, 4);
  entry.writeUInt16LE(32, 6);
  entry.writeUInt32LE(pngs[index].length, 8);
  entry.writeUInt32LE(offset, 12);
  offset += pngs[index].length;
});

fs.mkdirSync(path.dirname(out), { recursive: true });
fs.writeFileSync(out, Buffer.concat([header, directory, ...pngs]));
console.log(`Wrote ${path.relative(root, out)} (${fs.statSync(out).size} bytes)`);
