"""Check actual repository documentation, without following symlinks or fetching URLs."""
import json
from pathlib import Path
import re
import sys
from urllib.parse import unquote

root = Path(sys.argv[1]).resolve()
files = [root / 'README.md', *sorted((root / 'docs').rglob('*.md'))]
assert len(files) >= 5, 'Expected design documentation was not discovered'
links = 0
for path in files:
    assert not path.is_symlink() and path.resolve().is_relative_to(root), path
    text = path.read_text()
    assert text.strip(), f'Empty documentation: {path}'
    assert not re.search(r'^(<<<<<<< |=======\s*$|>>>>>>> )', text, re.M), path
    # Remove fenced examples so conceptual/example links are not treated as files.
    text = re.sub(r'^```.*?^```[^\n]*', '', text, flags=re.M | re.S)
    for href in re.findall(r'(?<!!)\[[^\]\n]+\]\(([^)\s]+)(?:\s+"[^"]*")?\)', text):
        if re.match(r'[a-zA-Z][\w+.-]*:', href) or href.startswith('#'):
            continue
        destination = unquote(href.split('#')[0])
        target = (path.parent / destination).resolve()
        assert target.is_relative_to(root) and target.exists(), f'{path}: broken link {href}'
        links += 1
print(json.dumps({'suite': 'documentation', 'files': len(files), 'localLinks': links, 'passed': True}))
