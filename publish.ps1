$version = "1.0.1"
$configuration = "Release"
$runtimes = @(
    "win-x64",
    "win-x86",
    "win-arm64",
    "linux-x64",
    "linux-musl-x64",
    "linux-musl-arm64",
    "linux-arm64",
    "osx-x64",
    "osx-arm64"
)

# Create releases directory if it doesn't exist
$releasesDir = "..\IPE_releases\$version"
if (!(Test-Path $releasesDir)) {
    New-Item -ItemType Directory -Path $releasesDir | Out-Null
}

foreach ($runtime in $runtimes) {
    Write-Host "Publishing for $runtime..."
    
    $outputDir = Join-Path $releasesDir $runtime
    
    dotnet publish -c $configuration `
                  -r $runtime `
                  --self-contained true `
                  /p:PublishSingleFile=true `
                  /p:PublishTrimmed=true `
                  /p:IncludeNativeLibrariesForSelfExtract=true `
                  -o $outputDir
    
    # Optionally create zip archives for each platform
    Compress-Archive -Path "$outputDir\*" -DestinationPath "$releasesDir\IslandPerilExtractor-$version-$runtime.zip" -Force
    
    Write-Host "Published to $outputDir"
}

Write-Host "All platforms published to $releasesDir"
