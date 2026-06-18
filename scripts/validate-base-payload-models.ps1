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

$requiredBasePayloadModelFileNames = @(
    'depth_anything_metric_depth_indoor.pt',
    'iw3_row_flow_v3_20250627.pth'
)

$supportedModelExtensions = @(
    '.pth',
    '.pt',
    '.onnx',
    '.safetensors',
    '.ckpt',
    '.bin'
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

$modelsDirectory = Join-Path $publishDirectory 'engine\iw3\nunif\iw3\pretrained_models'
if (-not (Test-Path -LiteralPath $modelsDirectory -PathType Container)) {
    throw "Base payload pretrained models directory is missing: $modelsDirectory"
}

$optionalFileNameSet = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]$optionalModelPackCheckpointFileNames,
    [System.StringComparer]::OrdinalIgnoreCase)

$allowedFileNameSet = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]$allowedBasePayloadModelFileNames,
    [System.StringComparer]::OrdinalIgnoreCase)

$modelFiles = @(Get-ChildItem -LiteralPath $modelsDirectory -File -Recurse |
    Where-Object { $supportedModelExtensions -contains $_.Extension.ToLowerInvariant() })

$blockedFiles = @($modelFiles |
    Where-Object { $optionalFileNameSet.Contains($_.Name) })

if ($blockedFiles.Count -gt 0) {
    $relativePaths = $blockedFiles |
        Sort-Object FullName |
        ForEach-Object { ConvertTo-RelativePath -Root $publishDirectory -Path $_.FullName }
    $message = "Optional model-pack checkpoint found in base payload: $($relativePaths -join ', '). Rebuild publish output from a clean source bundle or import it through model packs."
    throw $message
}

$unexpectedFiles = @($modelFiles |
    Where-Object { -not $allowedFileNameSet.Contains($_.Name) })

if ($unexpectedFiles.Count -gt 0) {
    $relativePaths = $unexpectedFiles |
        Sort-Object FullName |
        ForEach-Object { ConvertTo-RelativePath -Root $publishDirectory -Path $_.FullName }
    $message = "Unexpected model file found in base payload: $($relativePaths -join ', '). Allowed base/runtime model files are: $($allowedBasePayloadModelFileNames -join ', ')."
    throw $message
}

$presentModelFileNameSet = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]($modelFiles | ForEach-Object { $_.Name }),
    [System.StringComparer]::OrdinalIgnoreCase)
$missingRequiredFiles = @($requiredBasePayloadModelFileNames |
    Where-Object { -not $presentModelFileNameSet.Contains($_) })

if ($missingRequiredFiles.Count -gt 0) {
    throw "Required base/runtime model file(s) missing from base payload: $($missingRequiredFiles -join ', ')."
}

Write-Host "[OK] Base payload model-file guard passed: $publishDirectory"
Write-Host "[OK] Allowed base/runtime model files: $($allowedBasePayloadModelFileNames -join ', ')"
Write-Host "[OK] Required base/runtime model files found: $($requiredBasePayloadModelFileNames -join ', ')"
