#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"
work="$(mktemp -d /tmp/dsharp-app-example.XXXXXX)"
trap 'rm -rf "$work"' EXIT
sources=(examples/inventory-app/inventory/stock.ds examples/inventory-app/app/summary.ds)
dotnet run --project src/DSharp.Cli -- check "${sources[@]}"
dotnet run --project src/DSharp.Cli -- compile "${sources[@]}" "$work/generated"
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
CS
dotnet run --project "$work/Verify.csproj"
echo 'Inventory example checked, compiled and returned 42.'
