# PowerShell script to update version numbers across the project
# Usage: .\update-version.ps1 -NewVersion "0.3.4"

param(
    [Parameter(Mandatory=$true)]
    [string]$NewVersion
)

Write-Host "Updating version to $NewVersion..." -ForegroundColor Green

# Update project file
$projectFile = "MameSelector\MameSelector.csproj"
if (Test-Path $projectFile) {
    $content = Get-Content $projectFile -Raw
    $content = $content -replace '<AssemblyVersion>\d+\.\d+\.\d+\.\d+</AssemblyVersion>', "<AssemblyVersion>$NewVersion.0</AssemblyVersion>"
    $content = $content -replace '<FileVersion>\d+\.\d+\.\d+\.\d+</FileVersion>', "<FileVersion>$NewVersion.0</FileVersion>"
    Set-Content $projectFile $content
    Write-Host "✓ Updated $projectFile" -ForegroundColor Green
}

# Update MainForm title
$mainFormFile = "MameSelector\MainForm.cs"
if (Test-Path $mainFormFile) {
    $content = Get-Content $mainFormFile -Raw
    $content = $content -replace 'Text = "MAME ROM Selector v\d+\.\d+\.\d+"', "Text = `"MAME ROM Selector v$NewVersion`""
    Set-Content $mainFormFile $content
    Write-Host "✓ Updated $mainFormFile" -ForegroundColor Green
}

# Update AboutForm version
$aboutFormFile = "MameSelector\Forms\AboutForm.Designer.cs"
if (Test-Path $aboutFormFile) {
    $content = Get-Content $aboutFormFile -Raw
    $content = $content -replace 'versionLabel\.Text = "Version \d+\.\d+\.\d+"', "versionLabel.Text = `"Version $NewVersion`""
    Set-Content $aboutFormFile $content
    Write-Host "✓ Updated $aboutFormFile" -ForegroundColor Green
}

# Update README version
$readmeFile = "README.md"
if (Test-Path $readmeFile) {
    $content = Get-Content $readmeFile -Raw
    $content = $content -replace '### Current Version: v\d+\.\d+\.\d+', "### Current Version: v$NewVersion"
    $content = $content -replace '## Recent Improvements \(v\d+\.\d+\.\d+\)', "## Recent Improvements (v$NewVersion)"
    Set-Content $readmeFile $content
    Write-Host "✓ Updated $readmeFile" -ForegroundColor Green
}

# Update GitHub Actions workflow
$workflowFile = ".github\workflows\release.yml"
if (Test-Path $workflowFile) {
    $content = Get-Content $workflowFile -Raw
    $content = $content -replace "default: 'v\d+\.\d+\.\d+'", "default: 'v$NewVersion'"
    $content = $content -replace "description: 'Release version \(e\.g\., v\d+\.\d+\.\d+\)'", "description: 'Release version (e.g., v$NewVersion)'"
    Set-Content $workflowFile $content
    Write-Host "✓ Updated $workflowFile" -ForegroundColor Green
}

Write-Host "`nVersion update complete! Remember to:" -ForegroundColor Yellow
Write-Host "1. Test the application" -ForegroundColor Yellow
Write-Host "2. Commit changes: git add . && git commit -m `"Update version to v$NewVersion`"" -ForegroundColor Yellow
Write-Host "3. Push to GitHub: git push origin master" -ForegroundColor Yellow
Write-Host "4. Create release using GitHub Actions workflow" -ForegroundColor Yellow
