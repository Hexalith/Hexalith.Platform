import { readFileSync } from 'node:fs';
import { GateError, binding, qualifyActual, saveReceipt } from './gate.mjs';

export async function recheck(receipt, qualify = qualifyActual, save = saveReceipt, phase = 'ready-to-tag') {
  const fresh = await qualify();
  if (!fresh.eligible || binding(fresh) !== binding(receipt)) throw new GateError('qualification-changed');
  const next = { ...receipt, status: phase };
  save(next);
  return next;
}
const recorded = () => JSON.parse(readFileSync(process.env.SOURCE_RELEASE_RECEIPT, 'utf8'));
export async function verifyRelease(_options, { nextRelease }) {
  if (!/^\d+\.\d+\.\d+$/.test(nextRelease.version) || nextRelease.gitHead !== recorded().source) throw new GateError('wrong-release-source');
  saveReceipt({ ...recorded(), computedVersion: nextRelease.version });
}
export async function generateNotes() {
  const receipt = recorded();
  return `Source: \`${receipt.source}\`.\n\n` + receipt.qualifyingRuns.map(run =>
    `[${run.workflow}, run ${run.run}, attempt ${run.attempt}](${run.url})`).join('\n\n') +
    '\n\nSource release only. Native, candidate and Story acceptance: false.';
}
// semantic-release calls prepare immediately before its tag creation/push and publish before the GitHub plugin.
export async function prepare() { await recheck(recorded()); }
export async function publish() { await recheck(recorded(), qualifyActual, saveReceipt, 'ready-to-release'); }
