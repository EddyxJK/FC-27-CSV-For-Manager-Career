#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { zstdDecompressSync } from 'node:zlib';
import { parseEaFile, loadTemplateMetadata } from 'eafc-db-kit';

const args = parseArgs(process.argv.slice(2));
if (!args.save || !args.out) {
  console.error('Uso: node parse-fc27-career.mjs --save <guardado> --out <jugadores.csv> [--names-db <base-FC27.fifa_ng_db>]');
  process.exit(2);
}

const raw = fs.readFileSync(args.save);
const metadata = await loadTemplateMetadata('FC 27');
const parsed = parseEmbeddedDatabases(raw, metadata, path.basename(args.save));
const careerDb = parsed.databases.find(db => db.tables.some(t => t.name === 'players'));
if (!careerDb) throw new Error('No encontré la tabla players en las bases del guardado.');

const players = table(careerDb, 'players');
const links = table(careerDb, 'teamplayerlinks');
const teams = table(careerDb, 'teams');
const nameMap = new Map();
const careerFirstNames = new Map();
const careerLastNames = new Map();
const careerCommonNames = new Map();
const bundledNames = path.join(path.dirname(fileURLToPath(import.meta.url)), 'playernames-fc27.json');
if (fs.existsSync(bundledNames)) {
  for (const [id, name] of Object.entries(JSON.parse(fs.readFileSync(bundledNames, 'utf8')))) nameMap.set(Number(id), name);
}
const bundledCareerNames = path.join(path.dirname(fileURLToPath(import.meta.url)), 'career-names-fc27.json');
if (fs.existsSync(bundledCareerNames)) loadCareerNames(JSON.parse(fs.readFileSync(bundledCareerNames, 'utf8')));
if (args['names-db']) {
  const gameDb = parseEaFile(fs.readFileSync(args['names-db']), { metadata, fileName: path.basename(args['names-db']) });
  const allTables = gameDb.databases.flatMap(db => db.tables);
  const nameTable = allTables.find(t => t.name === 'playernames');
  if (!nameTable) throw new Error('La base indicada no contiene la tabla playernames.');
  for (const row of nameTable.records) if (Number.isInteger(row.nameid) && row.name) nameMap.set(row.nameid, row.name);
  loadCareerNames(Object.fromEntries(['career_firstnames','career_lastnames','career_commonnames'].map(name => [name, allTables.find(t => t.name === name)?.records ?? []])));
}

const teamNames = new Map(teams.records.map(t => [t.teamid, t.teamname ?? '']));
const playerTeams = new Map();
for (const link of links.records) {
  if (!playerTeams.has(link.playerid)) playerTeams.set(link.playerid, []);
  playerTeams.get(link.playerid).push(link);
}

const columns = [
  'player_id','nombre','nombre_pila','apellido','nombre_camiseta','equipo','equipo_id',
  'overall','potencial','fecha_nacimiento_juego','nacionalidad_id','posicion_1','posicion_2',
  'posicion_3','pie_preferido','pierna_debil','filigranas','altura_cm','peso_kg','salario',
  'dorsal','lesion','apariciones_liga','goles_liga'
];
const csv = [columns.join(',')];
let unresolved = 0;
for (const p of players.records) {
  const fullName = resolveName(p, nameMap);
  if (fullName.includes('[ID ')) unresolved++;
  const linksForPlayer = playerTeams.get(p.playerid) ?? [null];
  for (const link of linksForPlayer) {
    const row = [
      p.playerid, fullName, lookupFirst(p.firstnameid), lookupLast(p.lastnameid), lookupJersey(p),
      link ? teamNames.get(link.teamid) ?? '' : '', link?.teamid ?? '', p.overallrating, p.potential,
      p.birthdate, p.nationality, p.preferredposition1, p.preferredposition2, p.preferredposition3,
      p.preferredfoot, p.weakfootabilitytypecode, p.skillmoves, p.height, p.weight, p.wage,
      link?.jerseynumber ?? '', link?.injury ?? '', link?.leagueappearances ?? '', link?.leaguegoals ?? ''
    ];
    csv.push(row.map(escapeCsv).join(','));
  }
}
fs.mkdirSync(path.dirname(path.resolve(args.out)), { recursive: true });
fs.writeFileSync(args.out, '\uFEFF' + csv.join('\r\n') + '\r\n', 'utf8');
console.log(`Exportados ${players.records.length.toLocaleString('es-CL')} jugadores; ${csv.length - 1} filas con enlaces de equipo.`);
console.log(`Nombres sin resolver: ${unresolved.toLocaleString('es-CL')}.`);
console.log(`CSV: ${path.resolve(args.out)}`);

function parseEmbeddedDatabases(bytes, template, fileName) {
  const magic = Buffer.from([0x28, 0xb5, 0x2f, 0xfd]);
  let cursor = 0;
  while ((cursor = bytes.indexOf(magic, cursor)) !== -1) {
    try {
      const result = zstdDecompressSync(bytes.subarray(cursor), { info: true, maxOutputLength: 128 * 1024 * 1024 });
      const decompressed = result.buffer;
      const parsed = parseEaFile(decompressed, { metadata: template, fileName, kind: 'career' });
      if (parsed.databases.some(db => db.tables.some(t => t.name === 'players'))) return parsed;
    } catch { /* This signature can occur inside unrelated compressed data. */ }
    cursor += magic.length;
  }
  throw new Error('No pude encontrar y descomprimir una base de carrera FC27 válida.');
}

function table(db, name) {
  const found = db.tables.find(t => t.name === name);
  if (!found) throw new Error(`Falta la tabla ${name}.`);
  return found;
}
function loadCareerNames(data) {
  if (data.career_firstnames) {
    for (const row of data.career_firstnames) if (row.firstnameid && row.firstname) careerFirstNames.set(row.firstnameid, typeof row.firstname === 'string' ? row.firstname : nameMap.get(row.firstname) ?? '');
  }
  if (data.career_lastnames) {
    for (const row of data.career_lastnames) if (row.lastnameid && row.lastname) careerLastNames.set(row.lastnameid, typeof row.lastname === 'string' ? row.lastname : nameMap.get(row.lastname) ?? '');
  }
  if (data.career_commonnames) {
    for (const row of data.career_commonnames) {
      const first = typeof row.firstname === 'string' ? row.firstname : nameMap.get(row.firstname);
      const last = typeof row.lastname === 'string' ? row.lastname : nameMap.get(row.lastname);
      const name = [first, last].filter(Boolean).join(' ').trim();
      if (row.commonnameid && name) careerCommonNames.set(row.commonnameid, name);
    }
  }
}
function generatedName(id, pool) {
  if (id < 46000) return '';
  return pool.get(id - 45999) || undefined;
}
function lookupFirst(id) { return nameMap.get(id) ?? generatedName(id, careerFirstNames) ?? (id > 0 ? `[ID ${id}]` : ''); }
function lookupLast(id) { return nameMap.get(id) ?? generatedName(id, careerLastNames) ?? (id > 0 ? `[ID ${id}]` : ''); }
function lookupJersey(p) {
  const id = p.playerjerseynameid;
  return nameMap.get(id) ?? generatedName(id, careerLastNames) ?? generatedName(id, careerFirstNames) ?? (id > 0 ? `[ID ${id}]` : '');
}
function resolveName(p) {
  if (p.commonnameid && (nameMap.has(p.commonnameid) || careerCommonNames.has(p.commonnameid - 45999)))
    return nameMap.get(p.commonnameid) ?? careerCommonNames.get(p.commonnameid - 45999);
  const first = lookupFirst(p.firstnameid), last = lookupLast(p.lastnameid);
  return [first, last].filter(Boolean).join(' ').trim() || `Jugador ${p.playerid}`;
}
function escapeCsv(value) {
  const text = value == null ? '' : String(value);
  return /[",\r\n]/.test(text) ? `"${text.replaceAll('"', '""')}"` : text;
}
function parseArgs(argv) {
  const out = {};
  for (let i = 0; i < argv.length; i++) {
    if (argv[i].startsWith('--')) out[argv[i].slice(2)] = argv[++i];
  }
  return out;
}
