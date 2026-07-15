using Microsoft.AspNetCore.Http;

namespace Fri3ndsDrive.Api.DTOs
{
    public class SubirArchivoDTO
    {
        public IFormFile Archivo { get; set; } = null!;
        public int IdUsuario { get; set; }
    }
}