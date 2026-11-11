"""Trusted orchestration: locked restore, offline credential-free build/tests, host-side TRX validation.

Candidate .cs is executable untrusted input. Never run it in the publishing job.
Only data crosses back out of the container. No host credentials/socket/home mount.
"""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

trusted = Path(__file__).resolve().parents[2]
candidate = Path(sys.argv[1]).resolve()
reports = Path(sys.argv[2]).resolve()
reports.mkdir(parents=True, exist_ok=True)
evidence = {"attempted": True, "build": "failure"}

def run(args, **kwargs):
    subprocess.run(args, check=True, **kwargs)

def results(path):
    run([sys.executable, str(trusted / 'scripts/check-test-results.py'), str(path)])
    root = ET.parse(path).getroot()
    total = len(root.findall('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}UnitTestResult'))
    return {"total": total, "passed": total, "skipped": 0}

try:
    # Restore only the approved project/manifests. MSBuild inputs are from trusted.
    cache = Path(os.environ.get('AI_NUGET_CACHE', str(reports.parent / 'dsharp-nuget-cache'))).resolve()
    clean_env = {k: v for k, v in os.environ.items() if k in ('PATH', 'HOME', 'DOTNET_ROOT', 'DOTNET_CLI_HOME', 'HTTP_PROXY', 'HTTPS_PROXY', 'NO_PROXY', 'http_proxy', 'https_proxy', 'no_proxy', 'SSL_CERT_FILE')}
    clean_env.update(NUGET_PACKAGES=str(cache), DOTNET_CLI_TELEMETRY_OPTOUT='1')
    run(['dotnet', 'restore', str(trusted / 'DSharp.slnx'), '--locked-mode', '--packages', str(cache)], cwd=trusted, env=clean_env)
    with tempfile.TemporaryDirectory(prefix='dsharp-candidate-') as temporary:
        config = Path(temporary)
        stage = config / 'workspace'
        stage.mkdir()
        (config / 'passwd').write_text(f'root:x:0:0:root:/root:/bin/bash\nverifier:x:{os.getuid()}:{os.getgid()}:verifier:/tmp/home:/bin/bash\n')
        (config / 'group').write_text(f'root:x:0:\nverifier:x:{os.getgid()}:\n')
        (config / 'nsswitch.conf').write_text('passwd: files\ngroup: files\nhosts: files dns\n')
        # Construct a fresh SDK workspace from trusted manifests and trusted acceptance tests.
        for name in ('global.json', 'Directory.Build.props', 'NuGet.Config', 'DSharp.slnx'):
            shutil.copyfile(trusted / name, stage / name)
        for folder in ('src', 'tests'):
            for source in (trusted / folder).rglob('*'):
                relative = source.relative_to(trusted)
                if not source.is_file() or 'obj' in relative.parts or 'bin' in relative.parts: continue
                if source.is_symlink(): raise ValueError(f'Symlink: {source}')
                destination = stage / relative
                destination.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(source, destination)
        # Replace source only, preserving reviewed project files and package locks.
        for source in (stage / 'src').rglob('*.cs'): source.unlink()
        for folder in ('src', 'tests/features'):
            for source in (candidate / folder).rglob('*.cs'):
                relative = source.relative_to(candidate)
                if 'obj' in relative.parts or 'bin' in relative.parts: continue
                if source.is_symlink(): raise ValueError(f'Symlink: {source}')
                destination = stage / relative
                destination.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(source, destination)
        features = any((stage / 'tests/features').rglob('*.cs'))
        # Resolve image from trusted file. AI_COMPILER_IMAGE is a local operator-only
        # override for environments unable to pull MCR; workflows never set it.
        image = os.environ.get('AI_COMPILER_IMAGE', (trusted / '.github/ai/compiler-image.txt').read_text().strip())
        script = r"""
set -euo pipefail
cd /work
test "$(dotnet --version)" = 10.0.100
dotnet restore DSharp.slnx --locked-mode --packages /packages --source /packages -p:NuGetAudit=false
dotnet build DSharp.slnx --no-restore -c Release
echo success > build-status
dotnet test tests/DSharp.Compiler.Tests --no-build --no-restore -c Release --logger 'trx;LogFileName=acceptance.trx' --results-directory /work/results
if [[ "$1" = yes ]]; then
  dotnet test tests/DSharp.Compiler.Tests --no-restore -c Release -p:DSharpTestGroup=Features --logger 'trx;LogFileName=features.trx' --results-directory /work/results
fi
"""
        try:
            completed = subprocess.run(['docker', 'run', '--rm', '--cidfile', str(config / 'cid'), '--network=none', '--cap-drop=ALL', '--security-opt=no-new-privileges',
                 '--pids-limit=256', '--memory=2g', '--cpus=2', '--read-only', '--user', f'{os.getuid()}:{os.getgid()}',
                 '--tmpfs', '/tmp:rw,nosuid,nodev,size=512m',
                 '--mount', f'type=bind,src={stage},dst=/work',
                 '--mount', f'type=bind,src={config / "passwd"},dst=/etc/passwd,readonly',
                 '--mount', f'type=bind,src={config / "group"},dst=/etc/group,readonly',
                 '--mount', f'type=bind,src={config / "nsswitch.conf"},dst=/etc/nsswitch.conf,readonly',
                 '--mount', f'type=bind,src={cache},dst=/packages,readonly',
                 '-e', 'HTTP_PROXY=', '-e', 'HTTPS_PROXY=', '-e', 'ALL_PROXY=', '-e', 'http_proxy=', '-e', 'https_proxy=',
                 image, '/usr/bin/env', '-i', 'PATH=/usr/share/dotnet:/usr/bin:/bin', 'DOTNET_ROOT=/usr/share/dotnet',
                 'HOME=/tmp/home', 'DOTNET_CLI_HOME=/tmp/home', 'NUGET_PACKAGES=/packages',
                 'DOTNET_CLI_TELEMETRY_OPTOUT=1', 'DOTNET_PROCESSOR_COUNT=2', 'USER=verifier', 'LOGNAME=verifier', '/bin/bash', '-c', script, 'verify', 'yes' if features else 'no'], timeout=480)
        finally:
            cid = config / 'cid'
            if cid.exists():
                subprocess.run(['docker', 'rm', '-f', cid.read_text().strip()], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        marker = stage / 'build-status'
        evidence['build'] = 'success' if marker.is_file() and not marker.is_symlink() and marker.read_text().strip() == 'success' else 'failure'
        for name in ('acceptance', 'features'):
            path = stage / 'results' / f'{name}.trx'
            if name == 'features' and not features: continue
            if not path.exists() and completed.returncode != 0: continue
            # Reports are bounded, parsed as data and never executed on the host.
            if path.is_symlink() or path.stat().st_size > 512000: raise ValueError('Unsafe/oversized report')
            shutil.copyfile(path, reports / path.name)
            evidence[name] = results(reports / path.name)
        assert completed.returncode == 0, 'Offline compiler build/test failed'
        assert evidence['acceptance']['total'] >= 18, 'Trusted acceptance tests missing'
finally:
    (reports / 'compiler.json').write_text(json.dumps(evidence))
