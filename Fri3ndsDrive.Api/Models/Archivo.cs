using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Fri3ndsDrive.Api.Models
{
    [Table("Archivos")]
    public class Archivo
    {
        [Key]
        public int IdArchivo { get; set; }

        public int IdUsuario { get; set; }

        public string NombreOriginal { get; set; } = string.Empty;
        public string NombreGuardado { get; set; } = string.Empty;
        public string? TipoArchivo { get; set; }
        public string RutaArchivo { get; set; } = string.Empty;
        public long TamanoBytes { get; set; }

        public string? TextoExtraido { get; set; }
        public string? ResumenIA { get; set; }
        public string? EtiquetasIA { get; set; }

        public DateTime FechaSubida { get; set; } = DateTime.Now;

        [ForeignKey("IdUsuario")]
        public Usuario? Usuario { get; set; }
    }
}