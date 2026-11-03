set -euo pipefail
package_properties=(
  -p:HexalithEventStoreVersion=4.0.4
  -p:HexalithTenantsVersion=6.0.0
  -p:HexalithMemoriesVersion=3.0.18
  -p:UseHexalithProjectReferences=false
  -p:UseNuGetDeps=true
  -p:EventStoreFromSource=false
  -p:HexalithEventStoreFromSource=false
  -p:HexalithTenantsFromSource=false
  -p:HexalithMemoriesFromSource=false
  -p:HexalithCommonsFromSource=false
  -p:HexalithCommonsHttpFromSource=false
  -p:HexalithMcpCliFromSource=false
  -p:RestoreSources=https://api.nuget.org/v3/index.json
)
# Use the published client with controlled HTTP replies; no private runtime is started.
dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj \
  --configuration Release --disable-build-servers -m:1 -warnaserror "${package_properties[@]}"
dotnet test --project tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj \
  --no-build --no-restore --configuration Release "${package_properties[@]}" \
  --filter-class Hexalith.Parties.Tests.Search.PartyMemoryRestClientRecoveryTests \
  --filter-class Hexalith.Parties.Tests.Search.PartyMemoryRecoveryTests \
  --minimum-expected-tests 52 --fail-skips on
