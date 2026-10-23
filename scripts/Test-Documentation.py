"""Check local Markdown links, Git hygiene and recognizable secrets (Python stdlib only).

Run from any folder: python scripts/Test-Documentation.py [--scan-history].
This is a pattern scan, not a guarantee that no secret exists.
"""
from pathlib import Path
from urllib.parse import unquote, urlsplit
from collections import defaultdict
import json
import re
import subprocess
import sys
import unicodedata

ROOT = Path(__file__).resolve().parent.parent


def git(*args, binary=False, stdin=None):
    return subprocess.check_output(
        ['git', *args], cwd=ROOT, input=stdin,
        text=not binary, encoding=None if binary else 'utf-8')


def without_fences(text):
    return re.sub(r'^```[^\n]*\n.*?^```\s*$', '', text, flags=re.M | re.S)


def anchors(path):
    counts = defaultdict(int)
    result = set()
    for title in re.findall(r'^#{1,6}\s+(.+?)\s*#*$', without_fences(path.read_text(encoding='utf-8-sig')), re.M):
        title = re.sub(r'\[([^]]+)\]\([^)]*\)', r'\1', title)
        slug = ''.join(c for c in title.lower() if c in '-_ ' or unicodedata.category(c)[0] in 'LN').replace(' ', '-')
        number = counts[slug]
        counts[slug] += 1
        result.add(slug if not number else f'{slug}-{number}')
    return result


tracked = set(git('ls-files').splitlines())
selected = sorted(set(git('ls-files', '--cached', '--others', '--exclude-standard').splitlines()))
errors = []
for name in git('ls-files', '-ci', '--exclude-standard').splitlines():
    errors.append(f'{name}: already tracked despite ignore rule; review required')
link_count = 0
for name in selected:
    path = ROOT / name
    if not path.is_file() or path.suffix.lower() != '.md':
        continue
    text = without_fences(path.read_text(encoding='utf-8-sig'))
    for target in re.findall(r'!?\[[^\]]*\]\((<[^>]+>|[^)\s]+)(?:\s+"[^"]*")?\)', text):
        target = target.strip('<>')
        uri = urlsplit(target)
        if uri.scheme or uri.netloc:
            continue  # Remote URLs and localhost endpoints are verified separately.
        link_count += 1
        destination = (path.parent / unquote(uri.path)).resolve() if uri.path else path
        if not destination.is_relative_to(ROOT):
            errors.append(f'{name}: link escapes repository: {target}')
        elif not destination.exists():
            errors.append(f'{name}: missing link: {target}')
        elif uri.fragment and destination.suffix == '.md' and unquote(uri.fragment) not in anchors(destination):
            errors.append(f'{name}: missing anchor: {target}')

patterns = {
    'private key': rb'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----',
    'GitHub token': rb'(?:ghp_|github_pat_)[A-Za-z0-9_]{20,}',
    'AWS access key': rb'\bAKIA[A-Z0-9]{16}\b',
    'literal connection password': rb'(?i)Password\s*=\s*(?!\$|<|\{|;|\s|\"|\x27)([A-Za-z0-9!@#%&*+_=.-]{8,})(?=;)',
}


def scan(data, label):
    for kind, pattern in patterns.items():
        if re.search(pattern, data):
            errors.append(f'{label}: potential {kind}; value suppressed')


bad_extensions = {'.pem', '.pfx', '.p12', '.key', '.mdf', '.ldf', '.ndf', '.bak', '.credentials'}
for name in selected:
    path = ROOT / name
    parts = Path(name).parts
    if any(part in {'bin', 'obj', 'node_modules', 'dist', 'TestResults', '.secrets'} for part in parts):
        errors.append(f'{name}: generated/secret directory selected for Git')
    if path.name.startswith('.env') and not path.name.endswith('.example'):
        errors.append(f'{name}: local environment file selected for Git')
    if path.suffix.lower() in bad_extensions or path.name == 'secrets.json':
        errors.append(f'{name}: local credentials/database file selected for Git')
    if path.is_file() and path.suffix.lower() not in {'.pdf', '.png', '.jpg'}:
        scan(path.read_bytes(), name)

ignore_samples = [
    '.vs/settings', '.vscode/local.code-workspace', '.idea/workspace.xml',
    'src/Test/bin/output.dll', 'src/Test/obj/assets.json', 'src/Web/node_modules/pkg/index.js',
    'src/Web/dist/index.html', '.env', '.env.local', '.env.dev.local', '.secrets/token',
    'secrets.json', 'local.credentials', 'signing.pem', 'local.key',
    'tests/TestResults/out.trx', 'coverage/report.html', 'coverage.cobertura.xml',
    'runtime.log', 'scratch.tmp', 'Thumbs.db', '.DS_Store']
for sample in ignore_samples:
    result = subprocess.run(['git', 'check-ignore', '--no-index', '-q', sample], cwd=ROOT)
    if result.returncode != 0:
        errors.append(f'ignore rule missing: {sample}')
keep_samples = [
    '.env.example', '.env.development.example', 'README.md', 'docs/API-DOCUMENTATION.md',
    'src/Contracts/MedEquipSLA.Contracts.Grpc/Protos/equipment.proto',
    'src/Services/Equipment/Equipment.Api/Program.cs',
    'src/Services/Equipment/Equipment.Infrastructure/Persistence/Migrations/20261023044354_InitialCreate.cs',
    'src/Services/Equipment/Equipment.Api/Dockerfile', 'compose.yaml',
    '.github/workflows/ci.yml', '.vscode/extensions.json']
for sample in keep_samples:
    if subprocess.run(['git', 'check-ignore', '--no-index', '-q', sample], cwd=ROOT).returncode == 0:
        errors.append(f'important source/sample is ignored: {sample}')

history_count = 0
if '--scan-history' in sys.argv:
    # Only blobs, deduplicated across all reachable refs. Never print matching values.
    objects = git('rev-list', '--objects', '--all').splitlines()
    ids = list(dict.fromkeys(line.split(' ', 1)[0] for line in objects))
    info = git('cat-file', '--batch-check', stdin='\n'.join(ids)+'\n').splitlines()
    for line in info:
        oid, kind, size = line.split()
        if kind != 'blob':
            continue
        history_count += 1
        if int(size) > 10_000_000:
            errors.append(f'blob {oid}: too large for current pattern scan; review required')
            continue
        scan(git('cat-file', 'blob', oid, binary=True), f'Git blob {oid}')

print(json.dumps({
    'result': 'FAIL' if errors else 'PASS',
    'local_markdown_links': link_count,
    'selected_files': len(selected), 'tracked_files': len(tracked),
    'history_blobs_scanned': history_count,
    'ignore_positive_cases': len(ignore_samples), 'ignore_keep_cases': len(keep_samples),
    'errors': errors,
    'limits': 'Recognizable secret patterns only; remote URLs/runtime endpoints checked separately.'
}, ensure_ascii=False, indent=2))
sys.exit(1 if errors else 0)
