namespace Fri3ndsDrive.Api.DTOs
{
    public class RestablecerPasswordDTO
    {
        public string Correo { get; set; } = string.Empty;
        public string Codigo { get; set; } = string.Empty;
        public string NuevaPassword { get; set; } = string.Empty;
    }
}