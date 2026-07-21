# ArreglarTunel.ps1
# Reinicia el tunel de Cloudflare, toma la URL nueva, la mete en app.js
# y sube el cambio a GitHub. Pensado para correr con doble clic en el .bat,
# o solo, sin ventana, desde la Tarea Programada al iniciar sesion (-Auto).
param(
    [switch]$Auto
)

$logPath = "$PSScriptRoot\ArreglarTunel_log.txt"
"=== $(Get-Date) ===" | Out-File $logPath

# --- Auto-elevacion: si no somos administradores, nos relanzamos con UAC ---
# (la Tarea Programada ya arranca elevado, asi que esto no dispara con -Auto)
$esAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)
if (-not $esAdmin) {
    Start-Process powershell -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    exit
}

function Pausar {
    if (-not $Auto) { Read-Host "Presiona Enter para cerrar" }
}

$rutaProyecto = "C:\Users\nahum\Desktop\Fri3ndsDrive"
$rutaAppJs    = "$rutaProyecto\Fri3ndsDrive.Frontend\app.js"
$rutaLog      = "C:\Fri3ndsDrive\tunnel.log"
$rutaLock     = "$PSScriptRoot\.ejecutando.lock"

if (Test-Path $rutaLock) {
    $edad = (Get-Date) - (Get-Item $rutaLock).LastWriteTime
    if ($edad.TotalMinutes -lt 5) {
        Write-Host "Ya hay una ejecucion en curso (o muy reciente). Cerrando para evitar choques." -ForegroundColor Yellow
        Pausar
        exit
    }
}
New-Item -ItemType File -Path $rutaLock -Force | Out-Null

function Log($texto) {
    Write-Host $texto
    $texto | Out-File $logPath -Append
}

Log "=== Reparando el tunel de Fri3ndsDrive ==="

try {
    Log "1) Reiniciando el servicio CloudflaredTunnel..."
    if (Test-Path $rutaLog) { Remove-Item $rutaLog -Force }
    Restart-Service -Name "CloudflaredTunnel" -Force
    Start-Sleep -Seconds 8
    Log "   Servicio reiniciado OK"
} catch {
    Log "   ERROR reiniciando servicio: $_"
}

Log "2) Buscando la URL nueva..."
$intentos = 0
$urlNueva = $null
while (-not $urlNueva -and $intentos -lt 10) {
    if (Test-Path $rutaLog) {
        $contenido = Get-Content $rutaLog -Raw
        $matches = [regex]::Matches($contenido, 'https://[a-z0-9\-]+\.trycloudflare\.com')
        if ($matches.Count -gt 0) { $urlNueva = $matches[$matches.Count - 1].Value }
    }
    if (-not $urlNueva) { Start-Sleep -Seconds 2; $intentos++ }
}

if (-not $urlNueva) {
    Log "   ERROR: no se encontro la URL nueva en el log."
    Log "=== Terminado con errores ==="
    Pausar
    exit 1
}
Log "   URL nueva: $urlNueva"

try {
    Log "3) Actualizando app.js..."
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    $contenidoJs = [System.IO.File]::ReadAllText($rutaAppJs, [System.Text.Encoding]::UTF8)
    $nuevoContenido = [regex]::Replace($contenidoJs, 'const API_URL = "https://[^"]+";', "const API_URL = `"$urlNueva/api`";")
    if ($nuevoContenido -eq $contenidoJs) {
        Log "   AVISO: el reemplazo no cambio nada (revisar el patron)."
    }
    [System.IO.File]::WriteAllText($rutaAppJs, $nuevoContenido, $utf8NoBom)
    Log "   app.js actualizado OK"
} catch {
    Log "   ERROR actualizando app.js: $_"
}

try {
    Log "4) Verificando que el tunel responda..."
    $resp = Invoke-WebRequest -Uri "$urlNueva/swagger/index.html" -UseBasicParsing -TimeoutSec 15
    Log "   Respuesta: HTTP $($resp.StatusCode)"
} catch {
    Log "   AVISO: el tunel todavia no responde: $_"
}

try {
    Log "5) Subiendo el cambio a GitHub..."
    Push-Location $rutaProyecto
    git add "Fri3ndsDrive.Frontend/app.js" | Out-Null
    $fecha = Get-Date -Format "yyyy-MM-dd HH:mm"
    git commit -m "Actualizar URL del tunel ($fecha)" | Out-Null
    git push
    Pop-Location
    Log "   Git: OK (push realizado)"
} catch {
    Log "   ERROR con git: $_"
}

Log ""
Log "=== Listo ==="
Log "URL publica actual: $urlNueva"
Log ""
Log "Netlify esta conectado a GitHub, asi que se redeploya solo con el push de arriba."
Log ""
Remove-Item $rutaLock -Force -ErrorAction SilentlyContinue
Pausar
