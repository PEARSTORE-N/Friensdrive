using Fri3ndsDrive.Api.Data;
using Fri3ndsDrive.Api.DTOs;
using Fri3ndsDrive.Api.Models;
using Fri3ndsDrive.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fri3ndsDrive.Api.Controllers
{
    [ApiController]
    [Route("api/archivos")]
    public class ArchivosController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly GeminiService _geminiService;
        private readonly TextExtractorService _textExtractorService;

        public ArchivosController(
    AppDbContext context,
    IWebHostEnvironment env,
    GeminiService geminiService,
    TextExtractorService textExtractorService)
        {
            _context = context;
            _env = env;
            _geminiService = geminiService;
            _textExtractorService = textExtractorService;
        }

        [HttpPost("subir")]
        public async Task<IActionResult> SubirArchivo([FromForm] SubirArchivoDTO dto)
        {
            var archivo = dto.Archivo;
            var idUsuario = dto.IdUsuario;

            if (archivo == null || archivo.Length == 0)
            {
                return BadRequest(new { mensaje = "No se seleccionó ningún archivo." });
            }

            var usuarioExiste = await _context.Usuarios.AnyAsync(u => u.IdUsuario == idUsuario);

            if (!usuarioExiste)
            {
                return NotFound(new { mensaje = "Usuario no encontrado." });
            }

            string carpetaUploads = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");

            if (!Directory.Exists(carpetaUploads))
            {
                Directory.CreateDirectory(carpetaUploads);
            }

            string nombreGuardado = $"{Guid.NewGuid()}_{archivo.FileName}";
            string rutaCompleta = Path.Combine(carpetaUploads, nombreGuardado);

            using (var stream = new FileStream(rutaCompleta, FileMode.Create))
            {
                await archivo.CopyToAsync(stream);
            }

            string textoExtraido = "";
            string resumenIA = "";
            string etiquetasIA = "";

            textoExtraido = _textExtractorService.ExtraerTexto(rutaCompleta, archivo.FileName);

            if (!string.IsNullOrWhiteSpace(textoExtraido))
            {
                if (textoExtraido.Length > 6000)
                {
                    textoExtraido = textoExtraido.Substring(0, 6000);
                }

                resumenIA = await _geminiService.GenerarResumenAsync(textoExtraido);
                etiquetasIA = await _geminiService.GenerarEtiquetasAsync(textoExtraido);
            }
            else
            {
                resumenIA = "Este tipo de archivo no contiene texto legible o requiere OCR.";
                etiquetasIA = "archivo, sin-texto-legible";
            }

            var nuevoArchivo = new Archivo
            {
                IdUsuario = idUsuario,
                NombreOriginal = archivo.FileName,
                NombreGuardado = nombreGuardado,
                TipoArchivo = archivo.ContentType,
                RutaArchivo = rutaCompleta,
                TamanoBytes = archivo.Length,
                TextoExtraido = textoExtraido,
                ResumenIA = resumenIA,
                EtiquetasIA = etiquetasIA
            };

            _context.Archivos.Add(nuevoArchivo);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = "Archivo subido correctamente.",
                nuevoArchivo.IdArchivo,
                nuevoArchivo.NombreOriginal
            });
        }

        [HttpGet("usuario/{idUsuario}")]
        public async Task<IActionResult> ObtenerArchivos(int idUsuario)
        {
            var archivos = await _context.Archivos
                .Where(a => a.IdUsuario == idUsuario)
                .OrderByDescending(a => a.FechaSubida)
                .ToListAsync();

            return Ok(archivos);
        }

        [HttpGet("buscar")]
        public async Task<IActionResult> BuscarArchivos([FromQuery] int idUsuario, [FromQuery] string texto)
        {
            var archivos = await _context.Archivos
                .Where(a => a.IdUsuario == idUsuario &&
                    (
                        a.NombreOriginal.Contains(texto) ||
                        (a.TipoArchivo != null && a.TipoArchivo.Contains(texto)) ||
                        (a.ResumenIA != null && a.ResumenIA.Contains(texto)) ||
                        (a.TextoExtraido != null && a.TextoExtraido.Contains(texto)) ||
                        (a.EtiquetasIA != null && a.EtiquetasIA.Contains(texto))
                    ))
                .ToListAsync();

            return Ok(archivos);
        }

        [HttpGet("descargar/{idArchivo}")]
        public async Task<IActionResult> DescargarArchivo(int idArchivo, [FromQuery] int idUsuario)
        {
            var archivo = await _context.Archivos
                .FirstOrDefaultAsync(a => a.IdArchivo == idArchivo && a.IdUsuario == idUsuario);

            if (archivo == null)
            {
                return NotFound(new { mensaje = "Archivo no encontrado." });
            }

            if (!System.IO.File.Exists(archivo.RutaArchivo))
            {
                return NotFound(new { mensaje = "El archivo físico no existe en el servidor." });
            }

            string tipoArchivo = archivo.TipoArchivo ?? "application/octet-stream";

            return PhysicalFile(archivo.RutaArchivo, tipoArchivo, archivo.NombreOriginal);
        }

        [HttpDelete("eliminar/{idArchivo}")]
        public async Task<IActionResult> EliminarArchivo(int idArchivo, [FromQuery] int idUsuario)
        {
            var archivo = await _context.Archivos
                .FirstOrDefaultAsync(a => a.IdArchivo == idArchivo && a.IdUsuario == idUsuario);

            if (archivo == null)
            {
                return NotFound(new { mensaje = "Archivo no encontrado." });
            }

            if (System.IO.File.Exists(archivo.RutaArchivo))
            {
                System.IO.File.Delete(archivo.RutaArchivo);
            }

            _context.Archivos.Remove(archivo);
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Archivo eliminado correctamente." });
        }
    }
}