# Build 00-banner.png from docs/screenshots (1280x640 social preview)
Add-Type -AssemblyName System.Drawing

$srcDir = Join-Path $PSScriptRoot "..\docs\screenshots" | Resolve-Path
$bannerPath = Join-Path $srcDir "00-banner.png"

function Resize-ToHeight {
    param([System.Drawing.Image]$Image, [int]$Height)
    $ratio = $Height / [double]$Image.Height
    $w = [Math]::Max(1, [int][Math]::Round($Image.Width * $ratio))
    $bmp = New-Object System.Drawing.Bitmap $w, $Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($Image, 0, 0, $w, $Height)
    $g.Dispose()
    return $bmp
}

function Crop-Image {
    param([System.Drawing.Image]$Image, [int]$X, [int]$Y, [int]$W, [int]$H)
    $rect = New-Object System.Drawing.Rectangle $X, $Y, $W, $H
    return $Image.Clone($rect, $Image.PixelFormat)
}

$bannerW = 1280; $bannerH = 640
$banner = New-Object System.Drawing.Bitmap $bannerW, $bannerH
$bg = [System.Drawing.Graphics]::FromImage($banner)
$bg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$bg.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
$bg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush (
    (New-Object System.Drawing.Rectangle 0, 0, $bannerW, $bannerH),
    [System.Drawing.Color]::FromArgb(255, 15, 23, 42),
    [System.Drawing.Color]::FromArgb(255, 30, 41, 59),
    [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal
)
$bg.FillRectangle($brush, 0, 0, $bannerW, $bannerH)
$brush.Dispose()

$titleFont = New-Object System.Drawing.Font "Segoe UI", 34, ([System.Drawing.FontStyle]::Bold)
$subFont = New-Object System.Drawing.Font "Segoe UI", 13
$tagFont = New-Object System.Drawing.Font "Segoe UI", 10.5
$bg.DrawString("AiDocAssistant", $titleFont, [System.Drawing.Brushes]::White, 40, 56)
$accent = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 96, 165, 250))
$muted = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 148, 163, 184))
$bg.DrawString("Document AI on .NET 10", $subFont, $accent, 40, 112)
$y = 175
foreach ($t in @(
    "PDF extraction + OCR",
    "Agent chat (goal → tool)",
    "RAG with citations (pgvector)",
    "47 tests · 14 eval cases · MCP"
)) {
    $bg.DrawString([char]0x2022 + " " + $t, $tagFont, $muted, 44, $y)
    $y += 28
}
$titleFont.Dispose(); $subFont.Dispose(); $tagFont.Dispose(); $accent.Dispose(); $muted.Dispose()

$agentFull = [System.Drawing.Image]::FromFile((Join-Path $srcDir "03-agent-reconcile.png"))
$cropH = [Math]::Min($agentFull.Height, [int]($agentFull.Height * 0.72))
$agentCrop = Crop-Image $agentFull 0 0 $agentFull.Width $cropH
$agentFull.Dispose()
$agentHero = Resize-ToHeight -Image $agentCrop -Height 560
$agentCrop.Dispose()
$agentX = [Math]::Max(380, $bannerW - $agentHero.Width - 24)
$bg.DrawImage($agentHero, $agentX, 40)
$agentHero.Dispose()

$docsFull = [System.Drawing.Image]::FromFile((Join-Path $srcDir "01-documents.png"))
$docsCrop = Crop-Image $docsFull 0 0 $docsFull.Width ([Math]::Min($docsFull.Height, [int]($docsFull.Height * 0.55)))
$docsFull.Dispose()
$docsThumb = Resize-ToHeight -Image $docsCrop -Height 120
$docsCrop.Dispose()
$bg.DrawImage($docsThumb, 40, 480)
$docsThumb.Dispose()

$mcpFull = [System.Drawing.Image]::FromFile((Join-Path $srcDir "05-mcp-cursor.png"))
$mcpCrop = Crop-Image $mcpFull ([Math]::Min(200, $mcpFull.Width / 4)) 0 ($mcpFull.Width - [Math]::Min(200, $mcpFull.Width / 4)) $mcpFull.Height
$mcpFull.Dispose()
$mcpThumb = Resize-ToHeight -Image $mcpCrop -Height 120
$mcpCrop.Dispose()
$bg.DrawImage($mcpThumb, 220, 480)
$mcpThumb.Dispose()

$bg.Dispose()
$banner.Save($bannerPath, [System.Drawing.Imaging.ImageFormat]::Png)
$banner.Dispose()
Write-Host "Saved $bannerPath"
