/* ============================================================
   FRI3NDS DRIVE — Lógica de la aplicación
   Se conserva la conexión original con el backend:
   nombres de funciones, IDs, rutas y API_URL sin cambios.
   ============================================================ */

const API_URL = "https://non-purpose-molecular-currently.trycloudflare.com/api";

let usuarioActual = null;

/* ------------------------------------------------------------
   Utilidades de interfaz (avisos, mensajes, confirmación)
   ------------------------------------------------------------ */

/** Muestra un aviso flotante en la esquina inferior. */
function mostrarAviso(texto, tipo = "info") {
    const zona = document.getElementById("toastZona");

    // Si la página no tiene zona de avisos, no se rompe nada.
    if (!zona) return;

    const toast = document.createElement("div");
    toast.className = `toast ${tipo}`;
    toast.innerText = texto;
    zona.appendChild(toast);

    setTimeout(() => {
        toast.classList.add("saliendo");
        setTimeout(() => toast.remove(), 220);
    }, 3200);
}

/** Escribe un mensaje dentro de la tarjeta (login, registro, recuperación). */
function escribirMensaje(idElemento, texto, tipo = "info") {
    const elemento = document.getElementById(idElemento);
    if (!elemento) return;

    elemento.className = `mensaje ${tipo}`;
    elemento.innerText = texto || "";
}

/** Confirmación visual dentro de la página. Devuelve una promesa booleana. */
function confirmarAccion(titulo, texto) {
    const modal = document.getElementById("modalConfirmar");

    // Respaldo si el modal no existe en la página.
    if (!modal) return Promise.resolve(confirm(texto));

    const btnAceptar = document.getElementById("modalAceptar");
    const btnCancelar = document.getElementById("modalCancelar");

    document.getElementById("modalTitulo").innerText = titulo;
    document.getElementById("modalTexto").innerText = texto;
    modal.classList.add("abierto");

    return new Promise(resolve => {
        const cerrar = (respuesta) => {
            modal.classList.remove("abierto");
            btnAceptar.onclick = null;
            btnCancelar.onclick = null;
            modal.onclick = null;
            resolve(respuesta);
        };

        btnAceptar.onclick = () => cerrar(true);
        btnCancelar.onclick = () => cerrar(false);
        modal.onclick = (e) => { if (e.target === modal) cerrar(false); };
    });
}

/** Convierte bytes a un texto legible. */
function formatearTamano(bytes) {
    const n = Number(bytes);
    if (!n || n < 0) return "0 KB";
    if (n < 1024) return `${n} B`;
    if (n < 1024 * 1024) return `${(n / 1024).toFixed(1)} KB`;
    return `${(n / (1024 * 1024)).toFixed(2)} MB`;
}

/** Obtiene la extensión para el ícono de la tarjeta de archivo. */
function obtenerExtension(nombre) {
    if (!nombre || !nombre.includes(".")) return "FILE";
    return nombre.split(".").pop().toUpperCase().slice(0, 4);
}

/** Evita que un texto del backend rompa el HTML de la tarjeta. */
function limpiarTexto(texto) {
    const div = document.createElement("div");
    div.innerText = texto ?? "";
    return div.innerHTML;
}

/* ------------------------------------------------------------
   Navegación entre pantallas
   ------------------------------------------------------------ */

function abrirRegistro() {
    window.open("registro.html", "_blank");
}

function abrirRecuperacion() {
    window.open("recuperacion.html", "_blank");
}

/* ------------------------------------------------------------
   Autenticación
   ------------------------------------------------------------ */

async function registrarUsuario() {
    const nombre = document.getElementById("nombreRegistro").value;
    const correo = document.getElementById("correoRegistro").value;
    const password = document.getElementById("passwordRegistro").value;
    const confirmarPassword = document.getElementById("confirmarPasswordRegistro").value;

    if (!nombre || !correo || !password || !confirmarPassword) {
        escribirMensaje("mensajeRegistro", "Completa todos los campos.", "error");
        return;
    }

    if (password !== confirmarPassword) {
        escribirMensaje("mensajeRegistro", "Las contraseñas no coinciden.", "error");
        return;
    }

    try {
        const respuesta = await fetch(`${API_URL}/auth/registro`, {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                nombre,
                correo,
                password
            })
        });

        const data = await respuesta.json();

        escribirMensaje("mensajeRegistro", data.mensaje, respuesta.ok ? "ok" : "error");

        if (respuesta.ok) {
            mostrarAviso("Cuenta creada. Ya puedes iniciar sesión.", "ok");

            document.getElementById("nombreRegistro").value = "";
            document.getElementById("correoRegistro").value = "";
            document.getElementById("passwordRegistro").value = "";
            document.getElementById("confirmarPasswordRegistro").value = "";
        }

    } catch (error) {
        escribirMensaje("mensajeRegistro", "Error al conectar con el servidor.", "error");
        console.error(error);
    }
}

async function iniciarSesion() {
    const correo = document.getElementById("correoLogin").value;
    const password = document.getElementById("passwordLogin").value;

    if (!correo || !password) {
        escribirMensaje("mensajeLogin", "Escribe correo y contraseña.", "error");
        return;
    }

    try {
        const respuesta = await fetch(`${API_URL}/auth/login`, {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                correo,
                password
            })
        });

        const data = await respuesta.json();

        escribirMensaje("mensajeLogin", data.mensaje, respuesta.ok ? "ok" : "error");

        if (respuesta.ok) {
            usuarioActual = data;

            document.querySelector(".login-page").style.display = "none";
            document.getElementById("dashboard").style.display = "block";
            document.getElementById("usuarioTexto").innerText = `Bienvenido, ${usuarioActual.nombre}`;

            mostrarAviso(`Sesión iniciada como ${usuarioActual.nombre}.`, "ok");

            cargarArchivos();
        }

    } catch (error) {
        escribirMensaje("mensajeLogin", "Error al conectar con el servidor.", "error");
        console.error(error);
    }
}

async function solicitarRecuperacion() {
    const correo = document.getElementById("correoRecuperacion").value;
    const codigoBox = document.getElementById("codigoBox");

    if (!correo) {
        escribirMensaje("mensajeRecuperacion", "Escribe tu correo electrónico.", "error");
        return;
    }

    try {
        const respuesta = await fetch(`${API_URL}/auth/solicitar-recuperacion`, {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                correo
            })
        });

        const data = await respuesta.json();

        escribirMensaje("mensajeRecuperacion", data.mensaje, respuesta.ok ? "ok" : "error");

        // El backend puede devolver el código temporal para pruebas.
        if (data.codigoTemporal) {
            codigoBox.style.display = "block";
            codigoBox.innerText = `Código temporal para pruebas: ${data.codigoTemporal}`;
            mostrarAviso("Código de recuperación generado.", "ok");
        }

    } catch (error) {
        escribirMensaje("mensajeRecuperacion", "Error al conectar con el servidor.", "error");
        console.error(error);
    }
}

async function restablecerPassword() {
    const correo = document.getElementById("correoRecuperacion").value;
    const codigo = document.getElementById("codigoRecuperacion").value;
    const nuevaPassword = document.getElementById("nuevaPassword").value;
    const confirmarNuevaPassword = document.getElementById("confirmarNuevaPassword").value;

    if (!correo || !codigo || !nuevaPassword || !confirmarNuevaPassword) {
        escribirMensaje("mensajeRecuperacion", "Completa todos los campos.", "error");
        return;
    }

    if (nuevaPassword !== confirmarNuevaPassword) {
        escribirMensaje("mensajeRecuperacion", "Las contraseñas no coinciden.", "error");
        return;
    }

    try {
        const respuesta = await fetch(`${API_URL}/auth/restablecer-password`, {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                correo,
                codigo,
                nuevaPassword
            })
        });

        const data = await respuesta.json();

        escribirMensaje("mensajeRecuperacion", data.mensaje, respuesta.ok ? "ok" : "error");

        if (respuesta.ok) {
            mostrarAviso("Contraseña actualizada.", "ok");

            document.getElementById("codigoRecuperacion").value = "";
            document.getElementById("nuevaPassword").value = "";
            document.getElementById("confirmarNuevaPassword").value = "";
        }

    } catch (error) {
        escribirMensaje("mensajeRecuperacion", "Error al conectar con el servidor.", "error");
        console.error(error);
    }
}

/* ------------------------------------------------------------
   Archivos
   ------------------------------------------------------------ */

async function subirArchivo() {
    if (!usuarioActual) {
        mostrarAviso("Primero inicia sesión.", "error");
        return;
    }

    const archivo = document.getElementById("archivo").files[0];

    if (!archivo) {
        mostrarAviso("Selecciona un archivo.", "error");
        return;
    }

    const formData = new FormData();
    formData.append("archivo", archivo);
    formData.append("idUsuario", usuarioActual.idUsuario);

    mostrarAviso("Subiendo archivo. La IA lo está analizando...");

    try {
        const respuesta = await fetch(`${API_URL}/archivos/subir`, {
            method: "POST",
            body: formData
        });

        const data = await respuesta.json();

        if (respuesta.ok) {
            mostrarAviso(data.mensaje || "Archivo subido.", "ok");

            document.getElementById("archivo").value = "";
            actualizarNombreArchivo();

            cargarArchivos();
        } else {
            mostrarAviso(data.mensaje || "No se pudo subir el archivo.", "error");
        }

    } catch (error) {
        mostrarAviso("Error al conectar con el servidor.", "error");
        console.error(error);
    }
}

async function cargarArchivos() {
    if (!usuarioActual) return;

    try {
        const respuesta = await fetch(`${API_URL}/archivos/usuario/${usuarioActual.idUsuario}`);
        const archivos = await respuesta.json();

        mostrarArchivos(archivos);

    } catch (error) {
        console.error(error);
        mostrarAviso("No se pudieron cargar los archivos.", "error");
    }
}

async function buscarArchivos() {
    if (!usuarioActual) return;

    const texto = document.getElementById("textoBusqueda").value;

    if (!texto) {
        cargarArchivos();
        return;
    }

    try {
        const respuesta = await fetch(`${API_URL}/archivos/buscar?idUsuario=${usuarioActual.idUsuario}&texto=${encodeURIComponent(texto)}`);
        const archivos = await respuesta.json();

        mostrarArchivos(archivos);

    } catch (error) {
        console.error(error);
        mostrarAviso("No se pudo completar la búsqueda.", "error");
    }
}

function mostrarArchivos(archivos) {
    const lista = document.getElementById("listaArchivos");
    lista.innerHTML = "";

    if (!archivos || archivos.length === 0) {
        lista.innerHTML = "<p>No se encontraron archivos. Sube uno para empezar.</p>";
        return;
    }

    archivos.forEach((archivo, indice) => {
        const div = document.createElement("div");
        div.className = "file-card";
        div.style.animationDelay = `${Math.min(indice, 8) * 55}ms`;

        // Las etiquetas IA llegan como texto: se convierten en chips.
        const etiquetas = (archivo.etiquetasIA || "")
            .split(/[,;|]/)
            .map(e => e.trim())
            .filter(e => e.length > 0);

        const chipsHTML = etiquetas.length > 0
            ? etiquetas.map(e => `<span class="chip">${limpiarTexto(e)}</span>`).join("")
            : `<span class="chip vacio">Sin etiquetas</span>`;

        div.innerHTML = `
            <div class="file-top">
                <span class="file-ext">${limpiarTexto(obtenerExtension(archivo.nombreOriginal))}</span>
                <h3>${limpiarTexto(archivo.nombreOriginal)}</h3>
            </div>

            <div class="file-meta">
                <span class="meta-item">${limpiarTexto(archivo.tipoArchivo || "Tipo no especificado")}</span>
                <span class="meta-item">${formatearTamano(archivo.tamanoBytes)}</span>
                <span class="meta-item">${new Date(archivo.fechaSubida).toLocaleString()}</span>
            </div>

            <div class="resumen-ia">
                <span class="resumen-ia-titulo">Resumen IA</span>
                ${limpiarTexto(archivo.resumenIA || "Sin resumen disponible.")}
            </div>

            <div class="etiquetas-ia">${chipsHTML}</div>

            <div class="file-actions">
                <button class="btn btn-primario" onclick="descargarArchivo(${archivo.idArchivo})">Descargar</button>
                <button class="btn btn-eliminar" onclick="eliminarArchivo(${archivo.idArchivo})">Eliminar</button>
            </div>
        `;

        lista.appendChild(div);
    });
}

function descargarArchivo(idArchivo) {
    if (!usuarioActual) {
        mostrarAviso("Primero inicia sesión.", "error");
        return;
    }

    const url = `${API_URL}/archivos/descargar/${idArchivo}?idUsuario=${usuarioActual.idUsuario}`;
    window.open(url, "_blank");
}

async function eliminarArchivo(idArchivo) {
    if (!usuarioActual) {
        mostrarAviso("Primero inicia sesión.", "error");
        return;
    }

    const confirmar = await confirmarAccion(
        "Eliminar archivo",
        "El archivo se borrará de tu nube junto con su resumen y etiquetas. Esta acción no se puede deshacer."
    );

    if (!confirmar) {
        return;
    }

    try {
        const respuesta = await fetch(`${API_URL}/archivos/eliminar/${idArchivo}?idUsuario=${usuarioActual.idUsuario}`, {
            method: "DELETE"
        });

        const data = await respuesta.json();

        mostrarAviso(data.mensaje || "Archivo eliminado.", respuesta.ok ? "ok" : "error");

        if (respuesta.ok) {
            cargarArchivos();
        }

    } catch (error) {
        mostrarAviso("Error al conectar con el servidor.", "error");
        console.error(error);
    }
}

function cerrarSesion() {
    usuarioActual = null;

    document.querySelector(".login-page").style.display = "grid";
    document.getElementById("dashboard").style.display = "none";
    document.getElementById("mensajeLogin").innerText = "";
    document.getElementById("correoLogin").value = "";
    document.getElementById("passwordLogin").value = "";

    mostrarAviso("Sesión cerrada.");
    window.scrollTo({ top: 0, behavior: "smooth" });
}

/* ------------------------------------------------------------
   Detalles de interfaz
   ------------------------------------------------------------ */

/** Muestra el nombre del archivo elegido dentro de la zona de carga. */
function actualizarNombreArchivo() {
    const input = document.getElementById("archivo");
    const etiqueta = document.getElementById("nombreArchivoSeleccionado");

    if (!input || !etiqueta) return;

    etiqueta.innerText = input.files.length > 0
        ? input.files[0].name
        : "Ningún archivo seleccionado";
}

document.addEventListener("DOMContentLoaded", () => {
    const inputArchivo = document.getElementById("archivo");
    if (inputArchivo) {
        inputArchivo.addEventListener("change", actualizarNombreArchivo);
    }

    // Enter para enviar en los formularios principales.
    const atajos = [
        { ids: ["correoLogin", "passwordLogin"], accion: iniciarSesion },
        { ids: ["nombreRegistro", "correoRegistro", "passwordRegistro", "confirmarPasswordRegistro"], accion: registrarUsuario },
        { ids: ["textoBusqueda"], accion: buscarArchivos }
    ];

    atajos.forEach(({ ids, accion }) => {
        ids.forEach(id => {
            const campo = document.getElementById(id);
            if (campo) {
                campo.addEventListener("keydown", (e) => {
                    if (e.key === "Enter") accion();
                });
            }
        });
    });
});
