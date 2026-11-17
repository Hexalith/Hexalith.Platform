import { execFileSync } from 'node:child_process';
import { readFileSync, writeFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';

const [repository, base, range, output, preset] = process.argv.slice(2);
const git = (...args) => execFileSync('git', ['-C', repository, ...args], { encoding: 'utf8' }).trim();
const { analyzeCommits } = await import(pathToFileURL(`${repository}/node_modules/@semantic-release/commit-analyzer/index.js`));
const { default: semver } = await import(pathToFileURL(`${repository}/node_modules/semver/index.js`));
const commits = git('rev-list', '--reverse', `${base}..HEAD`).split('\n').filter(Boolean)
  .map(hash => ({ hash, message: git('show', '-s', '--format=%B', hash) }));
if (!commits.length) throw new Error('A real maintenance commit range is required');
const options = preset ? { preset } : {};
const increment = await analyzeCommits(options, { cwd: repository, commits, env: process.env,
  options: { repositoryUrl: git('remote', 'get-url', 'origin') }, logger: { log() {} } });
if (increment !== 'patch') throw new Error(`Unexpected maintenance increment: ${increment}`);
const tags = git('tag', '--merged', 'HEAD').split('\n').filter(tag => /^v\d+\.\d+\.\d+$/.test(tag) && semver.satisfies(tag.slice(1), range));
tags.sort((a, b) => semver.rcompare(a.slice(1), b.slice(1)));
if (!tags.length) throw new Error('No genuine reachable maintenance tag');
const baseTag = tags[0];
if (git('rev-parse', `${baseTag}^{commit}`) !== base) throw new Error('Reachable maintenance base tag changed');
const version = semver.inc(baseTag.slice(1), increment);
if (!semver.satisfies(version, range)) throw new Error('Version escaped its approved maintenance range');
const collision = git('ls-remote', '--tags', 'origin', `refs/tags/v${version}`);
if (collision) throw new Error('The computed release tag already exists; do not invent a replacement');
const result = { kind: 'locked commit analysis only; no tag or release created', source: git('rev-parse', 'HEAD'), base,
  range, baseTag, increment, computedVersion: version, analyzerOptions: options, commits,
  analyzerVersion: JSON.parse(readFileSync(`${repository}/node_modules/@semantic-release/commit-analyzer/package.json`)).version,
  node: process.version };
writeFileSync(output, JSON.stringify(result, null, 2) + '\n', { flag: 'wx' });
console.log(JSON.stringify({ source: result.source, computedVersion: version, increment }));
