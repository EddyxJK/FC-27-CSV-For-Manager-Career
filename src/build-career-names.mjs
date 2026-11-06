#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { parseEaFile, loadTemplateMetadata } from 'eafc-db-kit';

const args = parseArgs(process.argv.slice(2));
if (!args['game-db'] || !args.out) {
  console.error('Uso: node build-career-names.mjs --game-db <base-FC27.fifa_ng_db> --out <career-names-fc27.json> [--names-out <playernames-fc27.json>]');
  process.exit(2);
}

const metadata = await loadTemplateMetadata('FC 27');
const parsed = parseEaFile(fs.readFileSync(args['game-db']), {
  metadata,
  fileName: path.basename(args['game-db'])
});
const tables = parsed.databases.flatMap(db => db.tables);
const nameById = new Map((tables.find(t => t.name === 'playernames')?.records ?? [])
  .map(row => [row.nameid, row.name]));
if (!nameById.size) throw new Error('No encontré la tabla playernames de FC27 en la base indicada.');

const output = {
  career_firstnames: rows('career_firstnames', row => ({ firstnameid: row.firstnameid, firstname: nameById.get(row.firstname) ?? '' })),
  career_lastnames: rows('career_lastnames', row => ({ lastnameid: row.lastnameid, lastname: nameById.get(row.lastname) ?? '' })),
  career_commonnames: rows('career_commonnames', row => ({
    commonnameid: row.commonnameid,
    firstname: nameById.get(row.firstname) ?? '',
    lastname: nameById.get(row.lastname) ?? ''
  }))
};
if (!output.career_firstnames.length || !output.career_lastnames.length) {
  throw new Error('La base indicada no contiene las listas de nombres generados de Carrera. Prueba otra base del juego.');
}
const target = path.resolve(args.out);
fs.mkdirSync(path.dirname(target), { recursive: true });
fs.writeFileSync(target, JSON.stringify(output), 'utf8');
if (args['names-out']) {
  const namesTarget = path.resolve(args['names-out']);
  fs.mkdirSync(path.dirname(namesTarget), { recursive: true });
  fs.writeFileSync(namesTarget, JSON.stringify(Object.fromEntries(nameById)), 'utf8');
}
console.log(`Diccionario local creado: ${target}`);
console.log(`Listas de carrera: ${output.career_firstnames.length} nombres, ${output.career_lastnames.length} apellidos y ${output.career_commonnames.length} nombres compuestos.`);
if (args['names-out']) console.log(`Diccionario general creado: ${path.resolve(args['names-out'])} (${nameById.size} entradas).`);

function rows(name, convert) {
  return (tables.find(table => table.name === name)?.records ?? []).map(convert).filter(row => Object.values(row).every(Boolean));
}
function parseArgs(argv) {
  const result = {};
  for (let i = 0; i < argv.length; i++) if (argv[i].startsWith('--')) result[argv[i].slice(2)] = argv[++i];
  return result;
}
