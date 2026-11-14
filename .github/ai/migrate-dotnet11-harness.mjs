// One-time, exact SDK migration for #151. It modifies only a temporary trusted
// checkout, after validating the expected base and all file contents. Test and
// verifier logic remain intact; pins and the host runtime-directory allowlist change.
import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

// GitHub can deliver a pull-request event with the earlier base SHA after main
// has advanced. Both reviewed bases have identical allowlisted verifier files.
const baseRevisions = new Set([
  'f22efb53fdedbb17339d798d61c9d25577497cd8',
  '5e0c1d366a9f434501ec3c5dbe32cbe65def90b3',
]);
export function isReviewedBase(revision) { return baseRevisions.has(revision); }
const files = [
  ['global.json', '0aa4b85ae248d59bbc4138f346d952b7fae4fe18cc701d921fa1c003211be777'],
  ['Directory.Build.props', '963d69bfb6737ee529c923fc39777f511da043c83ebd1ba3c507227269aecebb'],
  ['.github/ai/bootstrap.sh', 'ab470acb3db9e7360d4f6521f01244abdd9985ce0a87f56aad5386b464ec6101'],
  ['.github/ai/compiler-image.txt', 'b5fa13025702da215bc8fb62b1cfaf1f1b075201fdce1c50db27f12f674e4a61'],
  ['.github/ai/tests/prelude-resource.test.mjs', '73610fedbcaeeb72e5735606505f630941d34e6d8218388b83ff864524858119'],
  ['.github/ai/verify-compiler.mjs', 'a0babe0527f4d330b86206493074192ae2bcff9acda7b7487165e3c071713b45'],
  ['scripts/verify-compiler.sh', '5ca5bae10139e2ed4b36a5b39d572cc756a173124996b0efc6704712d943b536'],
  ['src/DSharp.Cli/Program.cs', '1b68ff719ddeb5e48bc01091cde68a87d4010d3aa2ed65db87046ac8608c4ae0'],
  ['src/DSharp.Cli/packages.lock.json', '2d36f18ae58a3bdb8db273c2998493c14d3b62bbeb405fb921c1655fff14f546'],
  ['src/DSharp.Compiler/packages.lock.json', 'a29c6aa8cfb81874ff8bb78dc369d7416f28c9b8cc47e99592bfc019b20c41eb'],
  ['tests/DSharp.Compiler.Tests/packages.lock.json', '409dc0d8a7ed54247c61fe238b79a80db4671f2cccf43267292d6cd071bf9bed'],
  ['tests/DSharp.Compiler.Tests/CompilerTests.cs', '3da121cd3ee1d553c0dcc1e2c616cedff980c0b1f7d4e781962e47fa0e19cff8'],
];
const oldImage = 'b84b696efd5c236918761d35975209ad15918c4216cc6f41a573ff62e962ce72';
const newImage = '65f3c7c6a25a239ab3e6719f2896147e7b68242ff2d8818dd70498bde6ef8bea';

export function migrateContent(content, expectedHash) {
  const hash = createHash('sha256').update(content).digest('hex');
  if (hash !== expectedHash) throw new Error('Unexpected trusted file content');
  const migrated = content.replaceAll('10.0.100', '11.0.100')
    .replaceAll('net10.0', 'net11.0').replaceAll(oldImage, newImage)
    .replace("'DOTNET_CLI_HOME', 'HTTP_PROXY'", "'DOTNET_CLI_HOME', 'XDG_RUNTIME_DIR', 'HTTP_PROXY'");
  if (migrated === content) throw new Error('No reviewed version pin found');
  return migrated;
}

function main() {
  const [trusted, baseSha] = process.argv.slice(2);
  if (!trusted || !baseSha) throw new Error('Expected trusted checkout and base SHA');
  if (!isReviewedBase(baseSha)) {
    // Future bases that already use .NET 11 need no temporary migration.
    const sdk = JSON.parse(readFileSync(join(trusted, 'global.json'), 'utf8')).sdk.version;
    if (sdk === '11.0.100') return;
    throw new Error('Unreviewed base: cannot migrate trusted .NET 10 harness');
  }
  // Validate the entire allowlist before writing any file.
  const changes = files.map(([path, hash]) => {
    const target = join(trusted, path);
    return [target, migrateContent(readFileSync(target, 'utf8'), hash)];
  });
  for (const [target, content] of changes) writeFileSync(target, content);
  console.log('Applied the exact .NET 11 pin migration to the temporary trusted harness.');
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) main();
