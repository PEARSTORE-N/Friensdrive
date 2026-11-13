# ArreglarTunel.ps1
# Levanta la API si no esta corriendo, reinicia el tunel rapido de Cloudflare,
# toma la URL nueva, la mete en app.js y sube el cambio a GitHub.
# Netlify esta conectado a GitHub, asi que se redeploya solo con el push.
#
# Uso:
#   ArreglarTunel.bat              doble clic (pausa al final para leer el resultado)
#   ArreglarTunel.bat -Auto        sin pausa (para una Tarea Programada al iniciar sesion)
#   ArreglarTunel.bat -Detener     apaga el tunel y la API
#
# No necesita administrador: el tunel corre como proceso normal del usuario
# (en la laptop anterior era un servicio de Windows y por eso pedia UAC, lo que
# ademas hacia que git push se colgara esperando credenciales).
param(
    [switch]$Auto,
    [switch]$Detener
)

# --- Rutas: todo relativo a este script, para que funcione en cualquier laptop ---
$rutaRepo     = Split-Path $PSScriptRoot -Parent
$rutaAppJs    = Join-Path $rutaRepo 'Fri3ndsDrive.Frontend\app.js'
# API que se levanta si no esta corriendo. Por defecto, la carpeta Fri3ndsDrive
# que esta junto al repo (...\Proyectos\Fri3ndsDrive\Fri3ndsDrive.Api).
# Si la API esta en otro lado, cambia esta linea.
$rutaApi      = Join-Path (Split-Path $rutaRepo -Parent) 'Fri3ndsDrive\Fri3ndsDrive.Api'
$puertoApi    = 5126

# Archivos de trabajo (ignorados por .gitignore)
$logPath      = Join-Path $PSScriptRoot 'ArreglarTunel_log.txt'
$rutaLogTunel = Join-Path $PSScriptRoot 'tunnel.log'
$rutaLogApi   = Join-Path $PSScriptRoot 'api.log'
$rutaLock     = Join-Path $PSScriptRoot '.ejecutando.lock'

# Programas (la Tarea Programada no siempre tiene el PATH actualizado)
$env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')
$cloudflared = (Get-Command cloudflared -ErrorAction SilentlyContinue).Source
if (-not $cloudflared) { $cloudflared = 'C:\Program Files (x86)\cloudflared\cloudflared.exe' }

"=== $(Get-Date) ===" | Out-File $logPath

function Log($texto) {
    Write-Host $texto
    $texto | Out-File $logPath -Append
}

function Pausar {
    if (-not $Auto) { Read-Host "Presiona Enter para cerrar" | Out-Null }
}

function PuertoAbierto($puerto) {
    $cliente = New-Object System.Net.Sockets.TcpClient
    try {
        $intento = $cliente.BeginConnect('127.0.0.1', $puerto, $null, $null)
        return ($intento.AsyncWaitHandle.WaitOne(1000) -and $cliente.Connected)
    } catch {
        return $false
    } finally {
        $cliente.Close()
    }
}

# Procesos de cloudflared que hacen tunel rapido hacia nuestra API
function ProcesosTunel {
    Get-CimInstance Win32_Process -Filter "Name='cloudflared.exe'" |
        Where-Object { $_.CommandLine -match "localhost:$puertoApi" }
}

# Proceso que escucha en el puerto de la API (y el 'dotnet run' que lo lanzo)
function ProcesosApi {
    $conexiones = Get-NetTCPConnection -LocalPort $puertoApi -State Listen -ErrorAction SilentlyContinue
    foreach ($idProc in ($conexiones.OwningProcess | Sort-Object -Unique)) {
        $proc = Get-CimInstance Win32_Process -Filter "ProcessId=$idProc"
        if ($proc) {
            $proc
            $padre = Get-CimInstance Win32_Process -Filter "ProcessId=$($proc.ParentProcessId)"
            if ($padre -and $padre.Name -eq 'dotnet.exe') { $padre }
        }
    }
}

# --- Modo -Detener ---
if ($Detener) {
    Log "=== Deteniendo tunel y API de Fri3ndsDrive ==="
    foreach ($p in @(ProcesosTunel) + @(ProcesosApi)) {
        if (-not $p) { continue }
        Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue
        Log "   Detenido: $($p.Name) (PID $($p.ProcessId))"
    }
    Log "=== Listo ==="
    Pausar
    exit 0
}

if (Test-Path $rutaLock) {
    $edad = (Get-Date) - (Get-Item $rutaLock).LastWriteTime
    if ($edad.TotalMinutes -lt 5) {
        Write-Host "Ya hay una ejecucion en curso (o muy reciente). Cerrando para evitar choques." -ForegroundColor Yellow
        Pausar
        exit
    }
}
New-Item -ItemType File -Path $rutaLock -Force | Out-Null

Log "=== Reparando el tunel de Fri3ndsDrive ==="
$codigoSalida = 0

try {
    # 1) API
    Log "1) Revisando la API (puerto $puertoApi)..."
    if (PuertoAbierto $puertoApi) {
        Log "   La API ya esta corriendo"
    } elseif (-not (Test-Path (Join-Path $rutaApi 'Fri3ndsDrive.Api.csproj'))) {
        throw "No encontre la API en $rutaApi (revisa `$rutaApi al inicio del script)"
    } else {
        Log "   Arrancando la API desde $rutaApi ..."
        Start-Process -FilePath 'dotnet' -ArgumentList 'run', '--launch-profile', 'http' `
            -WorkingDirectory $rutaApi -WindowStyle Hidden `
            -RedirectStandardOutput $rutaLogApi -RedirectStandardError (Join-Path $PSScriptRoot 'api_err.log') | Out-Null
        $espera = 0
        while (-not (PuertoAbierto $puertoApi) -and $espera -lt 90) { Start-Sleep -Seconds 2; $espera += 2 }
        if (-not (PuertoAbierto $puertoApi)) { throw "La API no arranco en 90 s. Revisa $rutaLogApi" }
        Log "   API arriba OK"
    }

    # 2) Tunel
    Log "2) Reiniciando el tunel de Cloudflare..."
    if (-not (Test-Path $cloudflared)) { throw "No encontre cloudflared ($cloudflared). Instala con: winget install Cloudflare.cloudflared" }
    foreach ($p in ProcesosTunel) {
        Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue
        Log "   Tunel anterior detenido (PID $($p.ProcessId))"
    }
    Start-Sleep -Seconds 1
    if (Test-Path $rutaLogTunel) { Remove-Item $rutaLogTunel -Force }
    Start-Process -FilePath $cloudflared `
        -ArgumentList 'tunnel', '--no-autoupdate', '--url', "http://localhost:$puertoApi", '--logfile', "`"$rutaLogTunel`"" `
        -WindowStyle Hidden | Out-Null

    Log "3) Buscando la URL nueva..."
    $urlNueva = $null
    $intentos = 0
    while (-not $urlNueva -and $intentos -lt 20) {
        Start-Sleep -Seconds 2
        $intentos++
        if (Test-Path $rutaLogTunel) {
            $contenido = Get-Content $rutaLogTunel -Raw
            $encontradas = [regex]::Matches($contenido, 'https://[a-z0-9\-]+\.trycloudflare\.com') |
                Where-Object { $_.Value -ne 'https://api.trycloudflare.com' }
            if ($encontradas) { $urlNueva = @($encontradas)[-1].Value }
        }
    }
    if (-not $urlNueva) { throw "No aparecio la URL del tunel. Revisa $rutaLogTunel" }
    Log "   URL nueva: $urlNueva"

    Push-Location $rutaRepo
    try {
        # 4) Traer lo ultimo de GitHub (para que el push no choque) y actualizar app.js
        Log "4) Actualizando el repo y app.js..."
        $env:GIT_TERMINAL_PROMPT = '0'
        git pull --rebase --autostash --quiet 2>&1 | ForEach-Object { Log "   $_" }
        $utf8SinBom = New-Object System.Text.UTF8Encoding($false)
        $js = [System.IO.File]::ReadAllText($rutaAppJs, [System.Text.Encoding]::UTF8)
        $jsNuevo = [regex]::Replace($js, 'const API_URL = "https://[^"]+";', "const API_URL = `"$urlNueva/api`";")
        if ($jsNuevo -eq $js) {
            Log "   AVISO: app.js no cambio (ya tenia esa URL o el patron no coincide)."
        } else {
            [System.IO.File]::WriteAllText($rutaAppJs, $jsNuevo, $utf8SinBom)
            Log "   app.js actualizado OK"
        }

        # 5) Verificar el tunel. El DNS de la URL nueva tarda unos segundos en
        # existir y Windows guarda en cache el "no existe", asi que se espera un
        # poco y se limpia la cache DNS entre intentos (no requiere administrador).
        Log "5) Verificando que el tunel responda..."
        Start-Sleep -Seconds 10
        $respondio = $false
        for ($i = 0; $i -lt 20 -and -not $respondio; $i++) {
            Clear-DnsClientCache -ErrorAction SilentlyContinue
            try {
                $resp = Invoke-WebRequest -Uri "$urlNueva/swagger/index.html" -UseBasicParsing -TimeoutSec 15
                Log "   Respuesta: HTTP $($resp.StatusCode)"
                $respondio = $true
            } catch {
                Start-Sleep -Seconds 5
            }
        }
        if (-not $respondio) { Log "   AVISO: el tunel todavia no responde desde esta laptop (puede tardar un par de minutos en propagarse)." }

        # 6) Subir a GitHub
        Log "6) Subiendo el cambio a GitHub..."
        git add -- 'Fri3ndsDrive.Frontend/app.js'
        git diff --cached --quiet
        if ($LASTEXITCODE -eq 0) {
            Log "   No hay cambios en app.js que subir."
        } else {
            $fecha = Get-Date -Format 'yyyy-MM-dd HH:mm'
            git commit --quiet -m "Actualizar URL del tunel ($fecha)"
        }
        # git push con limite de tiempo, por si el administrador de credenciales
        # se queda esperando una autenticacion que nadie puede aprobar.
        $job = Start-Job -ScriptBlock {
            param($ruta)
            Set-Location $ruta
            $env:GIT_TERMINAL_PROMPT = '0'
            git push 2>&1 | ForEach-Object { "$_" }
            "EXIT:$LASTEXITCODE"
        } -ArgumentList $rutaRepo
        if (Wait-Job $job -Timeout 60) {
            $salida = Receive-Job $job | Out-String
            $salida.Trim() -split "`r?`n" | Where-Object { $_ -and $_ -notmatch '^EXIT:' } | ForEach-Object { Log "   $_" }
            if ($salida -match 'EXIT:0') { Log "   Git: OK" } else { Log "   ERROR: git push fallo."; $codigoSalida = 1 }
        } else {
            Stop-Job $job
            Log "   ERROR: git push tardo mas de 60 s (probable prompt de credenciales). Cancelado."
            Log "   Inicia sesion con: gh auth login"
            $codigoSalida = 1
        }
        Remove-Job $job -Force -ErrorAction SilentlyContinue
    } finally {
        Pop-Location
    }

    Log ""
    Log "=== Listo ==="
    Log "URL publica actual: $urlNueva"
    Log "Netlify se redeploya solo con el push (tarda unos segundos)."
} catch {
    Log "   ERROR: $_"
    Log "=== Terminado con errores ==="
    $codigoSalida = 1
} finally {
    Remove-Item $rutaLock -Force -ErrorAction SilentlyContinue
}

Log ""
Pausar
exit $codigoSalida
