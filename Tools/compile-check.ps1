# Compiles every script under CoolSchool/Assets/CoolSchool with Unity's own Roslyn compiler,
# against the same references Unity uses (read from Assembly-CSharp-Editor.csproj).
# Works while the Unity editor is open. Prints compiler errors; exit code 0 = clean.
#
# Usage:  powershell -ExecutionPolicy Bypass -File Tools/compile-check.ps1

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "..\CoolSchool" | Resolve-Path
$csproj = Join-Path $project "Assembly-CSharp-Editor.csproj"
if (-not (Test-Path $csproj)) { throw "Missing $csproj. Open the project in Unity once so it generates the .csproj files." }

[xml]$proj = Get-Content $csproj
$unityData = "C:\Program Files\Unity\Hub\Editor\6000.3.24f1\Editor\Data"
$outDir = Join-Path $env:TEMP "coolschool-compile-check"
New-Item -ItemType Directory -Force $outDir | Out-Null

$lines = @("/target:library", "/nologo", "/nostdlib+", "/langversion:9",
    "/nowarn:0169,0649,0414,0618,0108,0067", "/out:$outDir\check.dll")
$defines = ($proj.Project.PropertyGroup | Where-Object { $_.DefineConstants } | Select-Object -First 1).DefineConstants
$lines += "/define:$defines"
foreach ($hint in $proj.Project.ItemGroup.Reference.HintPath)
{
    if (-not $hint) { continue }
    if ([System.IO.Path]::IsPathRooted($hint)) { $lines += "/reference:`"$hint`"" }
    else { $lines += "/reference:`"$project\$hint`"" }
}
Get-ChildItem (Join-Path $project "Assets\CoolSchool") -Recurse -Filter *.cs | ForEach-Object { $lines += "`"$($_.FullName)`"" }

$rsp = Join-Path $outDir "check.rsp"
$lines | Out-File -Encoding ascii $rsp

& "$unityData\NetCoreRuntime\dotnet.exe" "$unityData\DotNetSdkRoslyn\csc.dll" -noconfig "@$rsp"
if ($LASTEXITCODE -eq 0) { Write-Output "compile-check: OK" }
exit $LASTEXITCODE
