<#
    Arranque de la app para quien la prueba en su propia máquina. Se ejecuta con
    doble clic en Iniciar.cmd (un nivel arriba de esta carpeta), siempre el mismo,
    la primera vez y todas las siguientes:

      1. Elige el SQL Server: uno instalado y en marcha si lo hay y deja crear la
         base; si no, LocalDB. La elección se guarda en appsettings.Production.json
         (junto al ejecutable) y no se vuelve a hacer, para no cambiar de base de
         datos sin querer entre una ejecución y otra.
      2. Genera, solo la primera vez, la clave HMAC y el token de configuración
         inicial, y los guarda como variables de entorno del usuario de Windows.
         Nunca en un archivo de esta carpeta: si la carpeta se comparte, no viajan.
      3. Crea o actualiza el esquema con efbundle.exe (idempotente, así que también
         aplica las migraciones de una versión nueva).
      4. Arranca la app y abre el navegador.

    La cadena de conexión NO va en una variable de entorno: ConnectionStrings__
    DefaultConnection la leería cualquier otra app ASP.NET de ese usuario.
#>

$ErrorActionPreference = 'Stop'

$carpetaApp   = $PSScriptRoot
$ejecutable   = Join-Path $carpetaApp 'EDEEste.ControlCajaChica.Presentation.exe'
$bundle       = Join-Path $carpetaApp 'efbundle.exe'
$configLocal  = Join-Path $carpetaApp 'appsettings.Production.json'
$nombreBase   = 'ControlCajaChica'
$puerto       = 5055
$url          = "http://localhost:$puerto"

$varClave = 'Criptografia__ClaveHmac'
$varToken = 'ConfiguracionInicial__TokenArranque'

function Escribir-Paso($texto)  { Write-Host "`n>> $texto" -ForegroundColor Cyan }
function Escribir-Ok($texto)    { Write-Host "   $texto" -ForegroundColor Green }
function Escribir-Aviso($texto) { Write-Host "   $texto" -ForegroundColor Yellow }

function Detener($texto) {
    Write-Host "`n$texto" -ForegroundColor Red
    Read-Host "`nPresione Enter para cerrar"
    exit 1
}

function Nueva-CadenaAleatoria([int]$bytes) {
    $buffer = New-Object byte[] $bytes
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($buffer) } finally { $rng.Dispose() }
    return $buffer
}

function Nueva-Cadena([string]$servidor) {
    return "Server=$servidor;Database=$nombreBase;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
}

<# Devuelve $null si no se puede conectar, o un objeto con si la base ya existe y
   si este usuario de Windows podría crearla. #>
function Probar-Servidor([string]$servidor) {
    $cadena = "Server=$servidor;Database=master;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=15"
    $conexion = New-Object System.Data.SqlClient.SqlConnection $cadena
    try {
        $conexion.Open()
        $comando = $conexion.CreateCommand()
        $comando.CommandText = "SELECT CAST(CASE WHEN DB_ID(@base) IS NULL THEN 0 ELSE 1 END AS int), " +
                               "ISNULL(IS_SRVROLEMEMBER('sysadmin'), 0) + ISNULL(IS_SRVROLEMEMBER('dbcreator'), 0)"
        [void]$comando.Parameters.AddWithValue('@base', $nombreBase)
        $lector = $comando.ExecuteReader()
        [void]$lector.Read()
        $resultado = [pscustomobject]@{
            BaseExiste  = $lector.GetInt32(0) -eq 1
            PuedeCrear  = $lector.GetInt32(1) -gt 0
        }
        $lector.Close()
        return $resultado
    }
    catch { return $null }
    finally { $conexion.Dispose() }
}

<# Mismo criterio que la pantalla /configuracion-inicial: mientras nadie tenga el
   rol Administrador, hace falta mostrar el token. Se consulta después de crear
   las tablas, así que existen. #>
function Existe-Administrador([string]$cadenaBase) {
    $conexion = New-Object System.Data.SqlClient.SqlConnection $cadenaBase
    try {
        $conexion.Open()
        $comando = $conexion.CreateCommand()
        $comando.CommandText = "SELECT COUNT(*) FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id = ur.RoleId WHERE r.Name = 'Administrador'"
        return [int]$comando.ExecuteScalar() -gt 0
    }
    catch { return $false }
    finally { $conexion.Dispose() }
}

function Buscar-SqlLocalDb {
    $comando = Get-Command 'SqlLocalDB.exe' -ErrorAction SilentlyContinue
    if ($comando) { return $comando.Source }

    # El instalador no siempre lo deja en el PATH de una consola ya abierta.
    $candidato = Get-ChildItem 'C:\Program Files\Microsoft SQL Server\*\Tools\Binn\SqlLocalDB.exe' -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1
    if ($candidato) { return $candidato.FullName }
    return $null
}

<# Arranca (o crea) la instancia por defecto de LocalDB y espera a que acepte
   conexiones: la primera conexión a una instancia detenida puede fallar mientras
   arranca (es justo el error 4060 que dio el ejecutable publicado). #>
function Preparar-LocalDb([string]$sqlLocalDb) {
    # En Windows PowerShell 5.1, redirigir el stderr de un .exe con 'Stop' lo
    # convierte en excepción aunque el comando solo esté avisando.
    $ErrorActionPreference = 'Continue'
    & $sqlLocalDb info MSSQLLocalDB *> $null
    if ($LASTEXITCODE -ne 0) {
        & $sqlLocalDb create MSSQLLocalDB -s | Out-Null
    }
    else {
        & $sqlLocalDb start MSSQLLocalDB | Out-Null
    }

    for ($intento = 1; $intento -le 10; $intento++) {
        $prueba = Probar-Servidor '(localdb)\MSSQLLocalDB'
        if ($prueba) { return $prueba }
        Start-Sleep -Seconds 3
    }
    return $null
}

function Elegir-Servidor {
    # Primero un SQL Server instalado y en marcha. Las instancias detenidas se
    # ignoran: arrancarlas pide permisos de administrador.
    $servicios = Get-Service -Name 'MSSQLSERVER', 'MSSQL$*' -ErrorAction SilentlyContinue |
        Where-Object Status -eq 'Running'

    foreach ($servicio in $servicios) {
        $servidor = if ($servicio.Name -eq 'MSSQLSERVER') { '.' } else { '.\' + $servicio.Name.Substring(6) }
        $prueba = Probar-Servidor $servidor
        if ($prueba -and ($prueba.BaseExiste -or $prueba.PuedeCrear)) {
            Escribir-Ok "Se usará el SQL Server instalado: $servidor"
            return $servidor
        }
        Escribir-Aviso "Hay un SQL Server ($servidor), pero su usuario de Windows no puede crear bases en él. Se intenta con LocalDB."
    }

    $sqlLocalDb = Buscar-SqlLocalDb
    if ($sqlLocalDb) {
        if (Preparar-LocalDb $sqlLocalDb) {
            Escribir-Ok 'Se usará SQL Server LocalDB.'
            return '(localdb)\MSSQLLocalDB'
        }
        Detener 'LocalDB está instalado, pero no respondió. Pruebe reiniciar la computadora y volver a ejecutar Iniciar.cmd.'
    }

    Detener (
        "No se encontró ningún SQL Server en esta computadora.`n`n" +
        "Instale SQL Server Express LocalDB (gratuito, unos 50 MB):`n" +
        "  1. Abra https://www.microsoft.com/sql-server/sql-server-downloads`n" +
        "  2. En 'Express', descargue el instalador y elija 'Descargar medios' > 'LocalDB'.`n" +
        "  3. Ejecute SqlLocalDB.msi y luego vuelva a ejecutar Iniciar.cmd.")
}

function Obtener-VariableUsuario([string]$nombre) {
    return [Environment]::GetEnvironmentVariable($nombre, 'User')
}

function Fijar-VariableUsuario([string]$nombre, [string]$valor) {
    [Environment]::SetEnvironmentVariable($nombre, $valor, 'User')
    Set-Item -Path "Env:$nombre" -Value $valor
}

# ---------------------------------------------------------------------------

Write-Host 'Sistema de Control de Caja Chica - preparación' -ForegroundColor White

if (-not (Test-Path $ejecutable) -or -not (Test-Path $bundle)) {
    Detener "Faltan archivos de la aplicación en $carpetaApp. Vuelva a extraer el .zip completo."
}

# En la máquina de un desarrollador, las variables de entorno de usuario
# sobrescriben los user-secrets: la clave HMAC nueva reemplazaría la de
# desarrollo y todos sus datos se leerían como alterados.
$userSecrets = Join-Path $env:APPDATA 'Microsoft\UserSecrets\aspnet-EDEEste_ControlCajaChica_Presentation-52193ef4-fb0d-490f-abf3-b96c1e3debcf'
if ((Test-Path $userSecrets) -and -not (Obtener-VariableUsuario $varClave)) {
    Detener 'Esta computadora tiene los user-secrets de desarrollo del proyecto. Este paquete es para otra máquina; aquí use dotnet run.'
}

# Un .zip descargado marca cada archivo como "de Internet" y Windows puede
# bloquear el .exe. Quitar la marca no cambia nada más.
Get-ChildItem -Path $carpetaApp -Recurse -File | Unblock-File -ErrorAction SilentlyContinue

# Si la app ya está abierta, solo se abre el navegador.
$enMarcha = Get-Process -Name 'EDEEste.ControlCajaChica.Presentation' -ErrorAction SilentlyContinue
if ($enMarcha) {
    Escribir-Ok 'La aplicación ya estaba abierta.'
    Start-Process $url
    exit 0
}

# 1. Base de datos -----------------------------------------------------------
Escribir-Paso 'Base de datos'

$cadena = $null
if (Test-Path $configLocal) {
    $cadena = (Get-Content $configLocal -Raw | ConvertFrom-Json).ConnectionStrings.DefaultConnection
}

if ($cadena) {
    $servidor = ([regex]::Match($cadena, 'Server=([^;]+)')).Groups[1].Value
    Escribir-Ok "Configurada en una ejecución anterior: $servidor"
    if ($servidor -like '(localdb)*') {
        $sqlLocalDb = Buscar-SqlLocalDb
        if ($sqlLocalDb) { [void](Preparar-LocalDb $sqlLocalDb) }
    }
}
else {
    $servidor = Elegir-Servidor
    $cadena = Nueva-Cadena $servidor
    $json = @{ ConnectionStrings = @{ DefaultConnection = $cadena } } | ConvertTo-Json -Depth 3
    [IO.File]::WriteAllText($configLocal, $json, (New-Object Text.UTF8Encoding $false))
}

$estado = Probar-Servidor $servidor
if (-not $estado) {
    Detener "No se pudo conectar a $servidor. Si quiere elegir otro servidor, borre $configLocal y vuelva a ejecutar Iniciar.cmd."
}

# 2. Secretos ----------------------------------------------------------------
Escribir-Paso 'Clave de integridad'

$clave = Obtener-VariableUsuario $varClave
if ($clave) {
    $env:Criptografia__ClaveHmac = $clave
    Escribir-Ok 'Ya existía; se reutiliza.'
}
elseif ($estado.BaseExiste) {
    # Generar una clave nueva sobre una base que ya tiene datos marcaría cada
    # registro como alterado. Mejor parar que "arreglarlo" en silencio.
    Detener (
        "La base '$nombreBase' ya existe en $servidor, pero este usuario de Windows no tiene la clave de integridad con que se firmaron sus datos.`n" +
        "Ejecute Iniciar.cmd con el mismo usuario de Windows que la creó, o pida ayuda a quien le envió el sistema.")
}
else {
    Fijar-VariableUsuario $varClave ([Convert]::ToBase64String((Nueva-CadenaAleatoria 32)))
    Escribir-Ok 'Generada y guardada en las variables de entorno de su usuario.'
}

$token = Obtener-VariableUsuario $varToken
if (-not $token) {
    $token = -join ((Nueva-CadenaAleatoria 12) | ForEach-Object { $_.ToString('x2') })
    Fijar-VariableUsuario $varToken $token
}
else {
    $env:ConfiguracionInicial__TokenArranque = $token
}

# 3. Esquema -----------------------------------------------------------------
Escribir-Paso 'Creando o actualizando las tablas (puede tardar un minuto la primera vez)'

Push-Location $carpetaApp
try {
    & $bundle --connection $cadena
    $codigoBundle = $LASTEXITCODE
}
finally { Pop-Location }

if ($codigoBundle -ne 0) {
    Detener 'No se pudieron crear las tablas. Revise el mensaje de arriba.'
}
Escribir-Ok 'Tablas al día.'

# 4. Arranque ----------------------------------------------------------------
Escribir-Paso 'Iniciando la aplicación'

# El directorio de trabajo importa: la app guarda los comprobantes en App_Data
# relativo a él.
Start-Process -FilePath $ejecutable -ArgumentList "--urls $url" -WorkingDirectory $carpetaApp

$lista = $false
for ($intento = 1; $intento -le 30; $intento++) {
    Start-Sleep -Seconds 2
    try {
        Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 5 | Out-Null
        $lista = $true
        break
    }
    catch [System.Net.WebException] {
        # Cualquier respuesta HTTP (aunque sea un error) significa que ya escucha.
        if ($_.Exception.Response) { $lista = $true; break }
    }
}

if (-not $lista) {
    Detener 'La aplicación no respondió. Revise la otra ventana negra que se abrió: ahí aparece el error.'
}

if (-not (Existe-Administrador $cadena)) {
    Write-Host ''
    Write-Host '   Primera vez: cree el Administrador en la página que se va a abrir.' -ForegroundColor White
    Write-Host "   Token de configuración inicial: $token" -ForegroundColor White
    try {
        Set-Clipboard -Value $token
        Write-Host '   (ya está copiado; péguelo con Ctrl+V)' -ForegroundColor White
    } catch { }
    Start-Process "$url/configuracion-inicial"
}
else {
    Start-Process $url
}

Write-Host ''
Escribir-Ok "Listo. La aplicación está en $url"
Escribir-Ok 'Para cerrarla, cierre la ventana negra de la aplicación.'
Read-Host "`nPresione Enter para cerrar esta ventana"
