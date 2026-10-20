$ErrorActionPreference = 'Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    dotnet tool restore
    if ($LASTEXITCODE) { throw 'Tool restore failed.' }
    dotnet restore BusinessSuite.sln
    if ($LASTEXITCODE) { throw 'Restore failed.' }
    dotnet build BusinessSuite.sln --no-restore -c Release
    if ($LASTEXITCODE) { throw 'Build failed.' }
    dotnet test BusinessSuite.sln --no-build --no-restore -c Release --logger trx
    if ($LASTEXITCODE) { throw 'Tests failed.' }
    dotnet ef migrations has-pending-model-changes --project src/BusinessSuite.Infrastructure --configuration Release --no-build
    if ($LASTEXITCODE) { throw 'Migration model is out of date.' }
    dotnet ef migrations script --idempotent --project src/BusinessSuite.Infrastructure --configuration Release --no-build --output .artifacts/migrations.sql
    if ($LASTEXITCODE) { throw 'Migration script generation failed.' }
}
finally { Pop-Location }
