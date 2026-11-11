"""Operator-run offline compiler pilot; no GitHub/model calls or publishing authority.

Uses the same trusted verifier as CI. The temporary candidate changes a source
comment and adds a feature regression test; it does not implement new syntax.
"""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile

trusted = Path(__file__).resolve().parents[2]
reports = Path(sys.argv[1] if len(sys.argv) > 1 else trusted / 'artifacts/pilot').resolve()
reports.mkdir(parents=True, exist_ok=True)
summary = {}
with tempfile.TemporaryDirectory(prefix='dsharp-pilot-') as temporary:
    candidate = Path(temporary)
    shutil.copytree(trusted / 'src', candidate / 'src', ignore=shutil.ignore_patterns('bin', 'obj'))
    source = candidate / 'src/DSharp.Compiler/Compiler.cs'
    original = source.read_text()
    source.write_text(original + '\n// Offline code-task pilot.\n')
    feature = candidate / 'tests/features/PilotTests.cs'
    feature.parent.mkdir(parents=True)
    feature.write_text('''using Xunit;
using DSharp.Compiler;
public sealed class PilotTests {
    [Fact] public void CommentedLiteralBindsTo42() {
        var syntax = new Parser(Lexer.Lex("module Demo // pilot\\nfn answer(): int { 42 }")).Parse();
        Assert.Equal(42, Assert.Single(Binder.Bind(syntax).Functions).Value);
    }
}
''')
    for scenario in ('success', 'build-failure', 'zero-tests'):
        if scenario == 'build-failure': source.write_text(original + '\nthis will not compile\n')
        if scenario == 'zero-tests':
            source.write_text(original)
            feature.write_text('// No discoverable tests: this must fail verification.\n')
        destination = reports / scenario
        destination.mkdir()
        with (destination / 'verification.log').open('w') as log:
            result = subprocess.run([sys.executable, str(trusted / '.github/ai/verify-compiler.py'), str(candidate), str(destination)], stdout=log, stderr=subprocess.STDOUT, timeout=540)
        evidence = json.loads((destination / 'compiler.json').read_text())
        if scenario == 'success':
            assert result.returncode == 0, (destination / 'verification.log').read_text()
            assert evidence['acceptance']['total'] >= 18 and evidence['features']['total'] > 0
        else: assert result.returncode != 0, f'{scenario} incorrectly passed'
        summary[scenario] = {"exitCode": result.returncode, "compiler": evidence}
(reports / 'pilot.json').write_text(json.dumps(summary, indent=2))
print(json.dumps(summary, indent=2))
