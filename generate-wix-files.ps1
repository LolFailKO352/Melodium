# generate-wix-files.ps1
param(
    [string]$PublishDir = "publish",
    [string]$OutputFile = "wix\Files.wxs"
)
if (-not [System.IO.Path]::IsPathRooted($PublishDir)) {
    $PublishDir = Join-Path $PSScriptRoot $PublishDir
}
if (-not [System.IO.Path]::IsPathRooted($OutputFile)) {
    $OutputFile = Join-Path $PSScriptRoot $OutputFile
}

$publishPath = (Resolve-Path $PublishDir).Path
if (-not $publishPath.EndsWith('\')) {
    $publishPath += '\'
}
$allFiles = Get-ChildItem -Path $publishPath -Recurse -File

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine('<?xml version="1.0" encoding="utf-8"?>')
[void]$sb.AppendLine('<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">')
[void]$sb.AppendLine('  <Fragment>')
[void]$sb.AppendLine('    <ComponentGroup Id="PublishedFiles" Directory="INSTALLFOLDER">')

$fileIndex = 0
foreach ($file in $allFiles) {
    $relPath = $file.FullName
    if ($relPath.StartsWith($publishPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        $relPath = $relPath.Substring($publishPath.Length)
    }
    
    # Melodium.exe is handled separately in Package.wxs to attach shortcuts
    if ($relPath -eq "Melodium.exe") {
        continue
    }

    $fileIndex++
    $compGuid = [System.Guid]::NewGuid().ToString().ToUpper()
    $fileId = "fil_" + [System.Guid]::NewGuid().ToString("N")
    
    # If file is in subdirectory, handle relative subfolder
    $dirName = [System.IO.Path]::GetDirectoryName($relPath)
    if ([string]::IsNullOrEmpty($dirName)) {
        [void]$sb.AppendLine("      <Component Id=`"cmp_$fileIndex`" Guid=`"{$compGuid}`">")
        [void]$sb.AppendLine("        <File Id=`"$fileId`" Source=`"publish\$relPath`" KeyPath=`"yes`" />")
        [void]$sb.AppendLine("      </Component>")
    } else {
        # Note: WiX Subdirectory attribute on File puts it into proper subfolder
        $wixSubDir = $dirName.Replace('/', '\')
        [void]$sb.AppendLine("      <Component Id=`"cmp_$fileIndex`" Guid=`"{$compGuid}`" Subdirectory=`"$wixSubDir`">")
        [void]$sb.AppendLine("        <File Id=`"$fileId`" Source=`"publish\$relPath`" KeyPath=`"yes`" />")
        [void]$sb.AppendLine("      </Component>")
    }
}

[void]$sb.AppendLine('    </ComponentGroup>')
[void]$sb.AppendLine('  </Fragment>')
[void]$sb.AppendLine('</Wix>')

$outDir = Split-Path $OutputFile -Parent
if (-not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}
[System.IO.File]::WriteAllText($OutputFile, $sb.ToString(), [System.Text.Encoding]::UTF8)
Write-Host "Generated $OutputFile with $fileIndex files."
