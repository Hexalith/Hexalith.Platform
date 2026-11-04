import { createHash } from 'node:crypto';
import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { expect, type Page, type TestInfo } from '@playwright/test';

// Use the exact evaluated build selection, including NuGet fallback folders.
// A package pass cannot certify the newer producer source or its reconciler.
export async function qualifyFrontComposerAssets(page: Page, testInfo?: TestInfo): Promise<'source' | 'package'> {
  const configuration = process.env.CI ? 'Release' : 'Debug';
  const output = new URL(`../../../src/Hexalith.Parties.UI/bin/${configuration}/net10.0/`, import.meta.url);
  const [fromSource, mode, root] = (await readFile(new URL('Hexalith.Parties.UI.source-metadata.txt', output), 'utf8'))
    .trim().split(/\r?\n/);
  const [version, ...folders] = (await readFile(new URL('Hexalith.Parties.UI.package-metadata.txt', output), 'utf8'))
    .trim().split(/\r?\n/);
  const source = fromSource === 'true';
  if (source) {
    expect(mode, 'Source qualification requires the validated pinned-tool mapping.').toBe('source');
    expect(path.isAbsolute(root)).toBe(true);
  } else {
    expect(version, 'This adoption authorizes exactly the selected 4.7.5 package capabilities.').toBe('4.7.5');
    expect(folders.length, 'Read the restored package folders from the current build.').toBeGreaterThan(0);
  }
  const assets = [];
  for (const asset of ['js/fc-focus.js', 'js/fc-keyboard.js', 'js/fc-download.js', 'css/fc-shell.css']) {
    const candidates = source
      ? [path.join(root, 'src/Hexalith.FrontComposer.Shell/wwwroot', asset)]
      : folders.map(folder => path.join(folder, 'hexalith.frontcomposer.shell', version, 'staticwebassets', asset));
    let selected: Buffer | undefined;
    let selectedPath = '';
    for (const candidate of candidates) {
      try { selected = await readFile(candidate); selectedPath = candidate; break; }
      catch (error) { if ((error as NodeJS.ErrnoException).code !== 'ENOENT') throw error; }
    }
    expect(selected, `Missing selected FrontComposer asset: ${candidates.join(', ')}`).toBeDefined();
    const servedPath = `/_content/Hexalith.FrontComposer.Shell/${asset}`;
    const served = await page.request.get(servedPath);
    expect(served.ok(), servedPath).toBe(true);
    const bytes = await served.body();
    const sha256 = createHash('sha256').update(bytes).digest('hex');
    expect(sha256, `Served ${source ? 'source' : 'package'} bytes: ${asset}`).toBe(
      createHash('sha256').update(selected!).digest('hex'));
    assets.push({ servedPath, selectedPath: source
      ? path.relative(root, selectedPath)
      : path.join('hexalith.frontcomposer.shell', version, 'staticwebassets', asset), sha256, bytes: bytes.length });
  }
  if (testInfo) await testInfo.attach('frontcomposer-assets.json', {
    body: JSON.stringify({ mode: source ? 'source' : 'package', version, sourceRoot: source ? path.basename(root) : null, assets }, null, 2),
    contentType: 'application/json',
  });
  return source ? 'source' : 'package';
}
