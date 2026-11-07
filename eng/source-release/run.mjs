import { appendFileSync, readFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';
import semanticRelease from 'semantic-release';
import config from '../../release.config.mjs';
import { GateError, binding, qualifyActual, saveReceipt } from './gate.mjs';

export async function runRelease({ qualify = qualifyActual, release = semanticRelease, save = saveReceipt, previous = () => JSON.parse(readFileSync(process.env.SOURCE_RELEASE_RECEIPT, 'utf8')) } = {}) {
  const receipt = await qualify();
  save(receipt);
  if (!receipt.eligible) return receipt;
  try {
    const result = await release(config);
    // A no-release return must not hide semantic-release declining a checkout that became stale.
    if (!result?.nextRelease) {
      const final = await qualify();
      if (!final.eligible || binding(final) !== binding(receipt)) throw new GateError('qualification-changed');
    }
    const recorded = previous();
    const finished = result?.nextRelease
      ? { ...recorded, status: 'published', computedVersion: result.nextRelease.version, tag: result.nextRelease.gitTag,
        releaseUrls: result.releases.map(r => r.url).filter(Boolean) }
      : { ...recorded, status: 'no-release' };
    save(finished);
    return finished;
  } catch (error) {
    const prior = previous();
    save(['failed', 'published', 'refused'].includes(prior.status) ? prior : { ...prior, lastCompletedPhase: prior.status, status: 'failed', reason: error instanceof GateError ? error.message : 'semantic-release-failed' });
    throw error;
  }
}

export function recordCliFailure(error, { read = () => JSON.parse(readFileSync(process.env.SOURCE_RELEASE_RECEIPT, 'utf8')), save = saveReceipt,
  source = process.env.SOURCE_RELEASE_SHA } = {}) {
  let prior = {};
  try { prior = read(); } catch {}
  if (['failed', 'published', 'refused'].includes(prior.status)) { save(prior); return prior; }
  const refused = { ...prior, source: /^[a-f0-9]{40}$/.test(source || '') ? source : prior.source,
    schema: 'hexalith.platform.source-release.v1', status: 'refused', reason: error instanceof GateError ? error.message : 'release-operation-failed',
    nativeAcceptance: false, candidateAcceptance: false, storyAcceptance: false };
  save(refused); return refused;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    if (process.argv.includes('--check')) {
      const receipt = await qualifyActual(); saveReceipt(receipt);
      appendFileSync(process.env.GITHUB_OUTPUT, `eligible=${receipt.eligible}\nsource=${receipt.source}\n`);
    } else await runRelease();
  } catch (error) {
    // Preserve an already recorded post-qualification failure or published first outcome.
    recordCliFailure(error);
    process.exitCode = 1;
  }
}
