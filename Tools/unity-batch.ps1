# Runs a static editor method of the CoolSchool project in Unity batch mode (no editor window).
# Refuses to run while the project is open in the Unity editor.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File Tools/unity-batch.ps1 -Method CoolSchool.EditorTools.CommandLineBuild.BuildPlaygroundFromCommandLine
#   powershell -ExecutionPolicy Bypass -File Tools/unity-batch.ps1 -Method CoolSchool.EditorTools.VoiceBatchGenerator.GenerateMissingFromCommandLine -ExtraArgs '-voiceLanguage','en'
#
# The log goes to <repo>/Logs/batch-<Method>.log. The exit code is Unity's.

param(
    [Parameter(Mandatory = $true)][string]$Method,
    [string[]]$ExtraArgs = @()
)

$ErrorActionPreference = "Stop"

$unity = "C:\Program Files\Unity\Hub\Editor\6000.3.24f1\Editor\Unity.exe"
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$project = Join-Path $repo "CoolSchool"
if (-not (Test-Path $unity)) { Write-Error "Unity not found at $unity"; exit 2 }
if (-not (Test-Path $project)) { Write-Error "Project not found at $project"; exit 2 }

# --- Refuse to run while the project is open ---
$lock = Join-Path $project "Temp\UnityLockfile"
if (Test-Path $lock)
{
    $locked = $false
    try
    {
        $stream = [System.IO.File]::Open($lock, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
        $stream.Close()
    }
    catch { $locked = $true }
    if ($locked)
    {
        Write-Error "The project is open in Unity (Temp\UnityLockfile is locked). Close the editor first."
        exit 3
    }
}
$running = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" |
    Where-Object { $_.CommandLine -and $_.CommandLine.Replace('/', '\').ToLowerInvariant().Contains($project.ToLowerInvariant()) }
if ($running)
{
    Write-Error "A Unity.exe is already running for this project (pid $($running.ProcessId)). Close it first."
    exit 3
}

# --- Run ---
$logDir = Join-Path $repo "Logs"
New-Item -ItemType Directory -Force $logDir | Out-Null
$safeName = $Method -replace '[^A-Za-z0-9._-]', '_'
$log = Join-Path $logDir "batch-$safeName.log"
if (Test-Path $log) { Remove-Item $log -Force }

$unityArgs = @("-batchmode", "-quit", "-projectPath", "`"$project`"", "-executeMethod", $Method, "-logFile", "`"$log`"") + $ExtraArgs
Write-Output "unity-batch: running $Method (log: $log)"
$process = Start-Process -FilePath $unity -ArgumentList $unityArgs -Wait -PassThru -NoNewWindow
$exitCode = $process.ExitCode

# --- Report ---
if (Test-Path $log)
{
    # Unity echoes its command line into the log, so a secret passed as an argument must not stay there.
    $secretFlags = @("-deepVoiceInvoice")
    $content = [System.IO.File]::ReadAllText($log)
    for ($i = 0; $i -lt $ExtraArgs.Count - 1; $i++)
    {
        if ($secretFlags -contains $ExtraArgs[$i] -and $ExtraArgs[$i + 1])
        {
            $content = $content.Replace($ExtraArgs[$i + 1], "***")
        }
    }
    [System.IO.File]::WriteAllText($log, $content, (New-Object System.Text.UTF8Encoding($false)))

    $all = [System.IO.File]::ReadAllLines($log)
    Write-Output "----- last 40 log lines -----"
    $all | Select-Object -Last 40
    Write-Output "----- lines with 'error' or 'CoolSchool' -----"
    $all | Where-Object { $_ -match "error|CoolSchool" }
}
else
{
    Write-Output "unity-batch: no log file was written."
}

Write-Output "unity-batch: Unity exit code $exitCode"
exit $exitCode
