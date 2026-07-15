using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Fri3ndsDrive.Api.Models
{
    [Table("PasswordResetTokens")]
    public class PasswordResetToken
    {
        [Key]
        public int IdToken { get; set; }

        public int IdUsuario { get; set; }

        public string Codigo { get; set; } = string.Empty;

        public DateTime Expira { get; set; }

        public bool Usado { get; set; } = false;

        [ForeignKey("IdUsuario")]
        public Usuario? Usuario { get; set; }
    }
}