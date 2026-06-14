[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDir
)

$ErrorActionPreference = 'Stop'

$optionalModelPackCheckpointFileNames = @(
    'depth_anything_metric_depth_outdoor.pt',
    'depth_anything_vits14.pth',
    'depth_anything_vitb14.pth',
    'depth_anything_vitl14.pth',
    'depth_anything_v2_vits.pth',
    'depth_anything_v2_vitb.pth',
    'depth_anything_v2_vitl.pth',
    'depth_anything_v2_metric_hypersim_vits.pth',
    'depth_anything_v2_metric_hypersim_vitb.pth',
    'depth_anything_v2_metric_hypersim_vitl.pth',
    'depth_anything_v2_metric_vkitti_vits.pth',
    'depth_anything_v2_metric_vkitti_vitb.pth',
    'depth_anything_v2_metric_vkitti_vitl.pth',
    'distill_any_depth_vits.safetensors',
    'distill_any_depth_vitb.safetensors',
    'distill_any_depth_vitl.safetensors',
    'da3mono-large.safetensors',
    'depth_pro.pt',
    'video_depth_anything_vits.pth',
    'video_depth_anything_vitb.pth',
    'video_depth_anything_vitl.pth',
    'metric_video_depth_anything_vits.pth',
    'metric_video_depth_anything_vitb.pth',
    'metric_video_depth_anything_vitl.pth',
    'ZoeD_M12_N.pt',
    'ZoeD_M12_K.pt',
    'ZoeD_M12_NK.pt'
)

$allowedBasePayloadModelFileNames = @(
    'depth_anything_metric_depth_indoor.pt',
    'iw3_row_flow_v3_20250627.pth',
    'iw3_depth_aa_20250530.pth'
)

function ConvertTo-RelativePath {
    param(
        [string]$Root,
        [string]$Path
    )

    $rootWithSeparator = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if ($fullPath.StartsWith($rootWithSeparator, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $fullPath.Substring($rootWithSeparator.Length)
    }

    return $fullPath
}

$publishDirectory = if ([System.IO.Path]::IsPathRooted($PublishDir)) {
    [System.IO.Path]::GetFullPath($PublishDir)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $PublishDir))
}

if (-not (Test-Path -LiteralPath $publishDirectory -PathType Container)) {
    throw "Publish directory is missing: $publishDirectory"
}

$optionalFileNameSet = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]$optionalModelPackCheckpointFileNames,
    [System.StringComparer]::OrdinalIgnoreCase)

$blockedFiles = @(Get-ChildItem -LiteralPath $publishDirectory -File -Recurse |
    Where-Object { $optionalFileNameSet.Contains($_.Name) })

if ($blockedFiles.Count -gt 0) {
    $relativePaths = $blockedFiles |
        Sort-Object FullName |
        ForEach-Object { ConvertTo-RelativePath -Root $publishDirectory -Path $_.FullName }
    $message = "Optional model-pack checkpoint found in base payload: $($relativePaths -join ', '). Rebuild publish output from a clean source bundle or import it through model packs."
    throw $message
}

Write-Host "[OK] Base payload model-file guard passed: $publishDirectory"
Write-Host "[OK] Allowed base/runtime model files: $($allowedBasePayloadModelFileNames -join ', ')"
