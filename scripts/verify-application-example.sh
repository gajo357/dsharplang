#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"
work="$(mktemp -d /tmp/dsharp-app-example.XXXXXX)"
trap 'rm -rf "$work"' EXIT
sources=(examples/inventory-app/inventory/stock.ds examples/inventory-app/app/summary.ds)
# Build the checkout's compiler sources in a private directory. Other checks may
# build the checkout at the same time, so they must not share intermediate files.
mkdir -p "$work/compiler"
cp global.json Directory.Build.props NuGet.Config "$work/compiler/"
tar -cf - --exclude=bin --exclude=obj src/DSharp.Cli src/DSharp.Compiler | tar -xf - -C "$work/compiler"
chmod -R u+w "$work/compiler"
dotnet build "$work/compiler/src/DSharp.Cli/DSharp.Cli.csproj" --nologo
cli="$work/compiler/src/DSharp.Cli/bin/Debug/net11.0/dsharp.dll"
dotnet "$cli" check "${sources[@]}"
dotnet "$cli" compile "${sources[@]}" "$work/generated"
cat > "$work/global.json" <<'JSON'
{"sdk":{"version":"11.0.100","rollForward":"disable"}}
JSON
cat > "$work/NuGet.Config" <<'XML'
<configuration><packageSources><clear /></packageSources></configuration>
XML
cat > "$work/Verify.csproj" <<'XML'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net11.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
  <ItemGroup><Compile Include="Program.cs" /><Reference Include="Generated"><HintPath>generated/bin/Debug/net11.0/Generated.dll</HintPath></Reference></ItemGroup>
</Project>
XML
cat > "$work/Program.cs" <<'CS'
if (DSharp.Generated.InventorySummary.available() != 42)
    throw new System.Exception("Inventory result was not 42.");
if (DSharp.Generated.Inventory.remaining(9, 4) != 5 ||
    DSharp.Generated.Inventory.remaining(0, 0) != 0 ||
    DSharp.Generated.Inventory.remaining(3, 5) != -2)
    throw new System.Exception("Inventory remaining calculation was incorrect.");
if (DSharp.Generated.InventorySummary.availableAfterSale(50, 8, 3) != 39 ||
    DSharp.Generated.InventorySummary.availableAfterSale(10, 8, 2) != 0 ||
    DSharp.Generated.InventorySummary.availableAfterSale(3, 4, 2) != -3)
    throw new System.Exception("Cross-module stock calculation was incorrect.");
if (DSharp.Generated.Inventory.afterSale(50, 8, 3) != 39 ||
    DSharp.Generated.Inventory.afterSale(10, 8, 2) != 0 ||
    DSharp.Generated.Inventory.afterSale(3, 4, 2) != -3 ||
    !DSharp.Generated.Inventory.originalUnchanged(50, 8, 3) ||
    !DSharp.Generated.Inventory.originalUnchanged(3, 4, 2))
    throw new System.Exception("Inventory snapshot update was incorrect.");
// Observe both actual generated records independently of the D# helper's boolean.
var sell = typeof(DSharp.Generated.Inventory).GetMethod("sell", System.Reflection.BindingFlags.Public |
    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy)!;
var stockType = sell.GetParameters()[0].ParameterType;
var before = System.Activator.CreateInstance(stockType, 50, 4)!;
var after = sell.Invoke(null, new object[] { before, 17 })!;
int Field(object stock, string name) => (int)stockType.GetProperty(name)!.GetValue(stock)!;
if (object.ReferenceEquals(before, after) || Field(before, "received") != 50 || Field(before, "sold") != 4 ||
    Field(after, "received") != 50 || Field(after, "sold") != 21)
    throw new System.Exception("Inventory direct snapshot observation was incorrect.");
CS
dotnet run --project "$work/Verify.csproj"
echo 'Inventory example checked, compiled and returned 42.'
