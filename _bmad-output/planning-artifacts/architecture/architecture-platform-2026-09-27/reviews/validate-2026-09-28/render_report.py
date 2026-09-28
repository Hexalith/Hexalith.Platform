"""Render the standalone validation's triaged evidence as Markdown and HTML."""
from pathlib import Path
import html
import json
import os
import re

HERE = Path(__file__).resolve().parent
ROOT = next(p for p in HERE.parents if (p / '_bmad').is_dir())
data = json.loads((HERE / 'findings.json').read_text())
findings = data['findings']
esc = lambda value: html.escape(str(value), quote=True)


def source_link(ref):
    match = re.fullmatch(r'(.+?):(\d+)', ref)
    path, line = (match[1], match[2]) if match else (ref, '')
    if path.startswith('https://'):
        return f'<a href="{esc(path)}">{esc(path)}</a>'
    absolute = Path(path) if Path(path).is_absolute() else ROOT / path
    relative = os.path.relpath(absolute, HERE)
    label = absolute.name + (f':{line}' if line else '')
    return f'<a href="{esc(relative)}" title="{esc(ref)}">{esc(label)}</a>'


def markdown_source_link(ref):
    match = re.fullmatch(r'(.+?):(\d+)', ref)
    path, line = (match[1], match[2]) if match else (ref, '')
    absolute = Path(path) if Path(path).is_absolute() else ROOT / path
    label = absolute.name + (f':{line}' if line else '')
    return f'[{label}]({os.path.relpath(absolute, HERE)})'


md = [
    '# Platform architecture validation — 2026-09-28', '',
    f"**{data['verdict']}** — {data['summary']}", '',
    f"Target: [ARCHITECTURE-SPINE.md](../../ARCHITECTURE-SPINE.md), updated 2026-09-28. SHA-256: `{data['spine_sha256']}`.", '',
    data['scope'], '',
    '## Checks', '',
]
for item in data['checks']:
    md.append(f'- {item}')
md += ['', '## Independent reviews', '', '| Review | Verdict | Evidence |', '| --- | --- | --- |']
for review in data['reviews']:
    md.append(f"| {review['name']} | {review['verdict']} | [{review['file']}]({review['file']}) |")
md += ['', '## Triaged findings', '']
for f in findings:
    md += [f"### {f['id']} · {f['severity'].upper()} · {f['title']}", '',
           f"**Proposed action:** {f['action']}. **Confidence:** {f.get('confidence', 'high')}.", '',
           f['summary'], '', f"**Consequence:** {f['impact']}", '',
           f"**Recommended correction:** {f['recommendation']}", '']
    if f.get('mitigation'):
        md += [f"**Existing safeguards considered:** {f['mitigation']}", '']
    md += ['**Evidence:** ' + '; '.join(markdown_source_link(r) for r in f['source_refs']), '',
           '**Reviewer findings:** ' + ', '.join(f['reviewer_ids']), '']
    if f.get('external_sources'):
        md += ['**Primary sources:** ' + ', '.join(f"[{s.get('title', s['url'])}]({s['url']})" for s in f['external_sources']), '']
md += ['## Disposition of all reviewer findings', '', '| Reviewer ID | Disposition | Reason |', '| --- | --- | --- |']
for item in data['dispositions']:
    md.append(f"| {item['id']} | {item['disposition']} | {item['reason']} |")
md += ['', '## What already holds', ''] + [f'- {p}' for p in data['positives']]
md += ['', '## Limits and next step', ''] + data['limitations'] + ['', data['next_step'], '']
(HERE / 'validation-report.md').write_text('\n'.join(md))

cards = []
for f in findings:
    sources = ' · '.join(source_link(r) for r in f['source_refs'])
    outside = ''.join(f'<li><a href="{esc(s["url"])}">{esc(s.get("title", s["url"]))}</a></li>' for s in f.get('external_sources', []))
    external = f'<ul>{outside}</ul>' if outside else ''
    mitigation = f'<p><strong>Existing safeguards considered.</strong> {esc(f["mitigation"])}</p>' if f.get('mitigation') else ''
    cards.append(f'''<article class="finding" id="{esc(f['id'])}" data-severity="{esc(f['severity'])}" data-action="{esc(f['action'])}">
      <div class="finding-meta"><span class="badge {esc(f['severity'])}">{esc(f['severity'])}</span><span>{esc(f['id'])}</span><span>Proposed: {esc(f['action'])}</span></div>
      <h3>{esc(f['title'])}</h3><p>{esc(f['summary'])}</p>
      <p><strong>Consequence.</strong> {esc(f['impact'])}</p>
      <div class="recommendation"><strong>Recommended correction</strong><p>{esc(f['recommendation'])}</p></div>
      <details><summary>Evidence and safeguards</summary>{mitigation}<p class="sources">{sources}</p>{external}<p class="muted">Reviewer findings: {esc(', '.join(f['reviewer_ids']))}. Confidence: {esc(f.get('confidence', 'high'))}.</p></details>
    </article>''')

counts = {s: sum(f['severity'] == s for f in findings) for s in ['critical', 'high', 'medium', 'low']}
review_rows = ''.join(f'<tr><td>{esc(r["name"])}</td><td>{esc(r["verdict"])}</td><td><a href="{esc(r["file"])}">Full review</a></td></tr>' for r in data['reviews'])
disposition_rows = ''.join(f'<tr><td>{esc(r["id"])}</td><td>{esc(r["disposition"])}</td><td>{esc(r["reason"])}</td></tr>' for r in data['dispositions'])
check_list = ''.join(f'<li>{esc(c)}</li>' for c in data['checks'])
positive_list = ''.join(f'<li>{esc(c)}</li>' for c in data['positives'])
limitations = ''.join(f'<p>{esc(c)}</p>' for c in data['limitations'])
page = '''<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<meta name="color-scheme" content="light"><title>Platform architecture validation · 28 September 2026</title>
<style>
:root{--ink:#182c3c;--muted:#546878;--paper:#f3f5f7;--line:#d9e1e7;--red:#aa293b;--blue:#145b82;--amber:#8a5106}
*{box-sizing:border-box}body{margin:0;background:var(--paper);font:16px/1.6 system-ui,-apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif;color:var(--ink)}a{color:var(--blue);text-underline-offset:3px;overflow-wrap:anywhere}a:hover{color:#0b3853}a:focus-visible,button:focus-visible,select:focus-visible,input:focus-visible,summary:focus-visible{outline:3px solid #3281b3;outline-offset:4px}.wrap{max-width:1160px;margin:auto;padding:0 30px}header{background:#122e42;color:#fff;padding:54px 0 40px}header a{color:#bde5ff}.eyebrow{font-size:12px;font-weight:700;letter-spacing:.15em;text-transform:uppercase;color:#b6ccdc}h1{font-size:clamp(30px,5vw,48px);line-height:1.12;letter-spacing:-.035em;margin:16px 0}header p{max-width:840px;color:#dce6ed}.verdict{display:inline-block;font-weight:750;background:#f9d3d9;color:#761d2b;padding:6px 12px;border-radius:6px;margin:8px 0}nav{display:flex;gap:22px;flex-wrap:wrap;font-size:14px;margin-top:24px}.metrics{display:grid;grid-template-columns:repeat(4,1fr);gap:14px;margin:28px 0}.metric{background:#fff;border:1px solid var(--line);border-radius:10px;padding:19px 22px}.metric strong{display:block;font-size:29px;line-height:1.2}.metric span{font-size:13px;color:var(--muted)}.metric.alert strong{color:var(--red)}section{margin:34px 0}h2{font-size:25px;letter-spacing:-.025em;margin-bottom:10px}h3{font-size:21px;line-height:1.35;margin:12px 0}.intro{max-width:880px}.panel{background:#fff;border:1px solid var(--line);border-radius:10px;padding:22px 26px}.split{display:grid;grid-template-columns:1fr 1fr;gap:18px}.filters{display:flex;gap:12px;align-items:end;flex-wrap:wrap;margin:20px 0}.filters label{display:grid;gap:5px;font-size:13px;color:var(--muted)}input,select,button{font:inherit;padding:9px 12px;border:1px solid #b9c8d2;border-radius:6px;background:#fff;color:var(--ink)}input{min-width:270px}button{cursor:pointer}#shown{font-size:13px;color:var(--muted);align-self:center;margin:0}.finding{background:#fff;border:1px solid var(--line);border-left:4px solid #d5964a;border-radius:8px;margin:17px 0;padding:22px 26px;scroll-margin-top:20px}.finding[data-severity=high],.finding[data-severity=critical]{border-left-color:var(--red)}.finding[data-severity=low]{border-left-color:#718896}.finding-meta{display:flex;gap:14px;flex-wrap:wrap;align-items:center;color:var(--muted);font-size:12px;font-weight:650}.badge{border-radius:4px;padding:3px 8px;text-transform:uppercase;letter-spacing:.06em;background:#f5e6cb;color:var(--amber)}.badge.high,.badge.critical{background:#f9e0e3;color:var(--red)}.badge.low{background:#e8edf1;color:#435b6b}.recommendation{background:#edf4f8;border-radius:6px;padding:14px 18px;margin:17px 0}.recommendation p{margin:4px 0}details{border-top:1px solid var(--line);padding-top:12px;font-size:14px}summary{cursor:pointer;font-weight:650;color:var(--blue)}.sources{line-height:1.9}.muted{color:var(--muted)}.table-wrap{overflow-x:auto}table{width:100%;border-collapse:collapse;font-size:14px;text-align:left}th{background:#eaf0f4;font-size:12px;text-transform:uppercase;letter-spacing:.04em}td,th{padding:12px 14px;border-bottom:1px solid var(--line);vertical-align:top}ul{padding-left:21px}li{margin:7px 0}.hash{font:12px/1.7 ui-monospace,monospace;overflow-wrap:anywhere}footer{border-top:1px solid var(--line);padding:22px 0 40px;color:var(--muted);font-size:13px}.next{background:#183d56;color:#fff;padding:23px 26px;border-radius:9px}.next p{margin:5px 0}.next h2{margin:0}.empty{padding:25px;border:1px dashed #aabcc8;border-radius:8px} [hidden]{display:none!important}
@media(max-width:760px){.wrap{padding:0 18px}header{padding-top:35px}.metrics{grid-template-columns:repeat(2,1fr)}.split{grid-template-columns:1fr}.finding{padding:18px}.filters label,input{width:100%;min-width:0}.metric{padding:16px}nav{gap:15px}}
@media print{body{background:#fff;font-size:11px}header{background:#fff;color:var(--ink);padding:10px 0}header p,.eyebrow,header a{color:var(--ink)}.filters,nav,button{display:none}.wrap{max-width:none;padding:0}.finding{break-inside:avoid}.finding[hidden]{display:block!important}.metrics{margin:15px 0}.next{background:#eee;color:var(--ink)}details{display:block}h1{font-size:30px}}
</style></head><body>
<header><div class="wrap"><div class="eyebrow">Hexalith Platform · Architecture gate · 28 September 2026</div>
<h1>Architecture validation</h1><div class="verdict">@@VERDICT@@</div><p>@@SUMMARY@@</p>
<nav aria-label="Report sections"><a href="#findings">Findings</a><a href="#evidence">Checks &amp; reviews</a><a href="#dispositions">Triage trail</a><a href="validation-report.md">Markdown report</a><a href="../../ARCHITECTURE-SPINE.md">Source spine</a></nav></div></header>
<main class="wrap"><div class="metrics" aria-label="Validation totals">
<div class="metric alert"><strong>@@HIGH@@</strong><span>High severity findings</span></div><div class="metric"><strong>@@OTHER@@</strong><span>Medium and low findings</span></div><div class="metric"><strong>0</strong><span>Structural lint findings</span></div><div class="metric"><strong>@@REVIEWS@@</strong><span>Independent review lenses</span></div></div>
<p class="intro">@@SCOPE@@</p><p class="muted">@@CRITICAL@@ critical findings. Proposed actions describe follow-up work; validation applies no architecture changes.</p>
<section id="findings"><h2>Findings that need a decision or correction</h2><p>Each finding combines overlapping reviewer observations and cites the current revision.</p>
<div class="filters"><label>Search findings<input id="search" type="search" placeholder="Recovery, records, Dapr…"></label><label>Severity<select id="severity"><option value="all">All severities</option><option value="critical">Critical</option><option value="high">High</option><option value="medium">Medium</option><option value="low">Low</option></select></label><label>Proposed action<select id="action"><option value="all">All actions</option><option value="autofix">Autofix</option><option value="discuss">Discuss</option><option value="defer">Defer</option></select></label><button id="reset" type="button">Reset</button><p id="shown" role="status" aria-live="polite"></p></div>
<div id="finding-list">@@CARDS@@</div><p id="empty" class="empty" hidden>No findings match these filters.</p></section>
<section id="evidence"><h2>Checks and independent evidence</h2><div class="split"><div class="panel"><h3>Verification performed</h3><ul>@@CHECKS@@</ul></div><div class="panel"><h3>What already holds</h3><ul>@@POSITIVES@@</ul></div></div>
<div class="table-wrap panel" style="margin-top:18px"><table><thead><tr><th>Review</th><th>Reviewer verdict</th><th>Evidence</th></tr></thead><tbody>@@REVIEW_ROWS@@</tbody></table></div></section>
<section id="dispositions"><h2>Disposition of every reviewer finding</h2><p>Accepted risks, assigned implementation work and duplicate observations are tracked here so the report does not imply they disappeared.</p><div class="table-wrap panel"><table><thead><tr><th>Reviewer ID</th><th>Disposition</th><th>Reason</th></tr></thead><tbody>@@DISPOSITIONS@@</tbody></table></div></section>
<section><h2>Scope and practical limits</h2><div class="panel">@@LIMITATIONS@@<p>Reviewed source SHA-256:</p><p class="hash">@@HASH@@</p><p><a href="validation-inputs.json">Input hashes and initial workspace status</a> · <a href="lint.json">Linter output</a> · <a href="structure-check.json">Structural checks</a> · <a href="findings.json">Triaged findings data</a></p></div></section>
<section class="next"><h2>Next step</h2><p>@@NEXT@@</p></section></main>
<footer><div class="wrap">Standalone bmad-architecture Validate · 15 architecture decisions · September 28 revision · Self-contained HTML; no external scripts or fonts.</div></footer>
<script>
const cards=[...document.querySelectorAll('.finding')], search=document.querySelector('#search'), severity=document.querySelector('#severity'), action=document.querySelector('#action');
function filter(){const q=search.value.toLowerCase().trim();let count=0;for(const card of cards){const visible=(!q||card.textContent.toLowerCase().includes(q))&&(severity.value==='all'||card.dataset.severity===severity.value)&&(action.value==='all'||card.dataset.action===action.value);card.hidden=!visible;if(visible)count++;}document.querySelector('#shown').textContent=`${count} of ${cards.length} findings`;document.querySelector('#empty').hidden=count!==0;}
search.addEventListener('input',filter);severity.addEventListener('change',filter);action.addEventListener('change',filter);document.querySelector('#reset').addEventListener('click',()=>{search.value='';severity.value='all';action.value='all';filter();});filter();
addEventListener('beforeprint',()=>document.querySelectorAll('details').forEach(d=>{d.dataset.preprint=d.open;d.open=true;}));addEventListener('afterprint',()=>document.querySelectorAll('details').forEach(d=>d.open=d.dataset.preprint==='true'));
</script></body></html>'''
values = {'VERDICT': esc(data['verdict']), 'SUMMARY': esc(data['summary']), 'HIGH': counts['high'],
          'OTHER': counts['medium'] + counts['low'], 'CRITICAL': counts['critical'], 'REVIEWS': len(data['reviews']),
          'SCOPE': esc(data['scope']), 'CARDS': '\n'.join(cards), 'CHECKS': check_list, 'POSITIVES': positive_list,
          'REVIEW_ROWS': review_rows, 'DISPOSITIONS': disposition_rows, 'LIMITATIONS': limitations,
          'HASH': esc(data['spine_sha256']), 'NEXT': esc(data['next_step'])}
for key, value in values.items():
    page = page.replace(f'@@{key}@@', str(value))
(HERE / 'validation-report.html').write_text(page)
print(f'Rendered {len(findings)} findings to validation-report.md and validation-report.html')
