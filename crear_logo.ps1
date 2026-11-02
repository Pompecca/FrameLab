# Dibuja el logo original de la app y genera logo.png y logo.ico en la carpeta recursos.
Add-Type -AssemblyName System.Drawing
$dir = "$PSScriptRoot\recursos"
New-Item -ItemType Directory -Force $dir | Out-Null

function Dibujar($t) {
    $b = New-Object Drawing.Bitmap $t, $t
    $g = [Drawing.Graphics]::FromImage($b)
    $g.SmoothingMode = 'AntiAlias'
    $r = [int]($t * 0.22); $d = $r * 2; $m = $t - 1
    $p = New-Object Drawing.Drawing2D.GraphicsPath
    $p.AddArc(0, 0, $d, $d, 180, 90); $p.AddArc($m - $d, 0, $d, $d, 270, 90)
    $p.AddArc($m - $d, $m - $d, $d, $d, 0, 90); $p.AddArc(0, $m - $d, $d, $d, 90, 90); $p.CloseFigure()
    $fondo = New-Object Drawing.Drawing2D.LinearGradientBrush((New-Object Drawing.Point 0, 0), (New-Object Drawing.Point $t, $t), [Drawing.Color]::FromArgb(14, 36, 58), [Drawing.Color]::FromArgb(12, 92, 112))
    $g.FillPath($fondo, $p)

    # Medidor abierto por abajo con una línea de pulso que lo cruza
    $grosor = [Math]::Max(1.5, $t * 0.075)
    $acento = [Drawing.Color]::FromArgb(34, 199, 214)
    $arco = New-Object Drawing.Pen $acento, $grosor
    $arco.StartCap = 'Round'; $arco.EndCap = 'Round'
    $mg = $t * 0.2
    $g.DrawArc($arco, $mg, $mg, $t - 2 * $mg, $t - 2 * $mg, 145, 250)
    $pulso = New-Object Drawing.Pen ([Drawing.Color]::White), $grosor
    $pulso.StartCap = 'Round'; $pulso.EndCap = 'Round'; $pulso.LineJoin = 'Round'
    $pts = @(@(0.30, 0.55), @(0.43, 0.55), @(0.50, 0.36), @(0.58, 0.70), @(0.64, 0.55), @(0.72, 0.55)) | ForEach-Object { New-Object Drawing.PointF ($_[0] * $t), ($_[1] * $t) }
    $g.DrawLines($pulso, [Drawing.PointF[]]$pts)
    $g.Dispose()
    return $b
}

$tamanos = 16, 24, 32, 48, 64, 128, 256
$imgs = foreach ($t in $tamanos) {
    $b = Dibujar $t
    $ms = New-Object IO.MemoryStream
    $b.Save($ms, [Drawing.Imaging.ImageFormat]::Png)
    if ($t -eq 256) { $b.Save("$dir\logo.png", [Drawing.Imaging.ImageFormat]::Png) }
    $b.Dispose()
    , $ms.ToArray()
}
$fs = [IO.File]::Create("$dir\logo.ico")
$w = New-Object IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$tamanos.Count)
$pos = 6 + 16 * $tamanos.Count
for ($i = 0; $i -lt $tamanos.Count; $i++) {
    $t = $tamanos[$i]
    $w.Write([byte]($(if ($t -ge 256) { 0 } else { $t }))); $w.Write([byte]($(if ($t -ge 256) { 0 } else { $t })))
    $w.Write([byte]0); $w.Write([byte]0); $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$imgs[$i].Length); $w.Write([uint32]$pos)
    $pos += $imgs[$i].Length
}
foreach ($d in $imgs) { $w.Write([byte[]]$d) }
$w.Close()
