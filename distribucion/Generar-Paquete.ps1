<#
    Genera el paquete para que otra persona pruebe el sistema en su máquina:

      <Salida>\Caja Chica (prueba)\
          Iniciar.cmd      <- lo único que esa persona ejecuta
          LEAME.txt
          app\             <- publicación self-contained + efbundle.exe + Iniciar.ps1
      <Salida>\Caja Chica (prueba).zip

    Self-contained para que no tenga que instalar .NET; efbundle.exe para que cree
    el esquema sin el SDK ni dotnet-ef. Ningún secreto va en el paquete: la clave
    HMAC y el token de arranque los genera Iniciar.ps1 en la máquina de destino.

    Uso, desde la raíz del repo:
      .\distribucion\Generar-Paquete.ps1
#>
param(
    [string]$Salida = (Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts\paquete-prueba')
)

$ErrorActionPreference = 'Stop'

$raiz          = Split-Path $PSScriptRoot -Parent
$presentation  = Join-Path $raiz 'EDEEste.ControlCajaChica.Presentation'
$infrastructure = Join-Path $raiz 'EDEEste.ControlCajaChica.Infrastructure'
$nombre        = 'Caja Chica (prueba)'
$paquete       = Join-Path $Salida $nombre
$carpetaApp    = Join-Path $paquete 'app'
$zip           = Join-Path $Salida "$nombre.zip"

function Verificar-Salida($paso) {
    if ($LASTEXITCODE -ne 0) { throw "Falló: $paso (código $LASTEXITCODE)." }
}

# Carpeta limpia: publicar encima de una anterior dejaría archivos viejos (y un
# appsettings.Production.json de una prueba local) dentro del paquete.
if (Test-Path $paquete) { Remove-Item $paquete -Recurse -Force }
if (Test-Path $zip)     { Remove-Item $zip -Force }
New-Item -ItemType Directory -Path $carpetaApp -Force | Out-Null

Write-Host '>> Publicando la aplicación (self-contained, win-x64)' -ForegroundColor Cyan
dotnet publish $presentation -c Release -r win-x64 --self-contained true -o $carpetaApp
Verificar-Salida 'dotnet publish'

Write-Host '>> Generando efbundle.exe' -ForegroundColor Cyan
dotnet ef migrations bundle --self-contained -r win-x64 --configuration Release --force `
    --project $infrastructure --startup-project $presentation `
    -o (Join-Path $carpetaApp 'efbundle.exe')
Verificar-Salida 'dotnet ef migrations bundle'

# Development no aplica en la máquina de destino; quitarlo evita confusiones.
Remove-Item (Join-Path $carpetaApp 'appsettings.Development.json') -ErrorAction SilentlyContinue

Copy-Item (Join-Path $PSScriptRoot 'Iniciar.ps1') $carpetaApp
Copy-Item (Join-Path $PSScriptRoot 'Iniciar.cmd') $paquete
Copy-Item (Join-Path $PSScriptRoot 'LEAME.txt')   $paquete

# Última barrera: si alguien volvió a pegar un secreto en un appsettings, el
# paquete no sale. Es justo lo que pasó con la primera publicación.
$filtrados = Get-ChildItem $carpetaApp -Filter 'appsettings*.json' |
    Select-String -Pattern 'ClaveHmac|TokenArranque|ApiKey' -List
if ($filtrados) {
    throw "Hay un secreto en $($filtrados.Path -join ', '). Quítelo y vuelva a generar el paquete."
}

Write-Host '>> Comprimiendo' -ForegroundColor Cyan
Compress-Archive -Path $paquete -DestinationPath $zip

Write-Host "`nListo: $zip" -ForegroundColor Green
