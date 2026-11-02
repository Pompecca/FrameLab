# Compila con el compilador que ya trae Windows: no hace falta instalar nada.
# Con -Prueba genera una copia sin permisos de administrador para pruebas automáticas.
param([switch]$Prueba)
$base = $PSScriptRoot
$fw = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$wpf = "$fw\WPF"
New-Item -ItemType Directory -Force "$base\dist" | Out-Null
if (-not (Test-Path "$base\recursos\logo.ico")) { & "$base\crear_logo.ps1" }
$salida = if ($Prueba) { "$base\dist\prueba.exe" } else { "$base\dist\FrameLab.exe" }
$args = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/codepage:65001', "/out:$salida",
    "/resource:$base\catalogo.json,catalogo.json", "/resource:$base\src\Tema.xaml,Tema.xaml", "/resource:$base\recursos\logo.png,logo.png",
    "/win32icon:$base\recursos\logo.ico",
    "/r:$wpf\PresentationFramework.dll", "/r:$wpf\PresentationCore.dll", "/r:$wpf\WindowsBase.dll",
    "/r:$fw\System.Xaml.dll", "/r:$fw\System.Management.dll", "/r:$fw\System.Web.Extensions.dll", "/r:$fw\System.Core.dll")
if (-not $Prueba) { $args += "/win32manifest:$base\app.manifest" }
$args += "$base\src\*.cs"
& "$fw\csc.exe" @args
exit $LASTEXITCODE
