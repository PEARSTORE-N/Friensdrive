using Fri3ndsDrive.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Fri3ndsDrive.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Archivo> Archivos { get; set; }
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Usuario>(entity =>
            {
                entity.ToTable("Usuarios");
                entity.HasKey(u => u.IdUsuario);

                entity.Property(u => u.Nombre)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(u => u.Correo)
                    .HasMaxLength(150)
                    .IsRequired();

                entity.HasIndex(u => u.Correo)
                    .IsUnique();

                entity.Property(u => u.PasswordHash)
                    .HasMaxLength(255)
                    .IsRequired();
            });

            modelBuilder.Entity<Archivo>(entity =>
            {
                entity.ToTable("Archivos");
                entity.HasKey(a => a.IdArchivo);

                entity.Property(a => a.NombreOriginal)
                    .HasMaxLength(255)
                    .IsRequired();

                entity.Property(a => a.NombreGuardado)
                    .HasMaxLength(255)
                    .IsRequired();

                entity.Property(a => a.TipoArchivo)
                    .HasMaxLength(100);

                entity.Property(a => a.RutaArchivo)
                    .HasMaxLength(500)
                    .IsRequired();

                entity.HasOne(a => a.Usuario)
                    .WithMany(u => u.Archivos)
                    .HasForeignKey(a => a.IdUsuario)
                    .HasPrincipalKey(u => u.IdUsuario);
            });

            modelBuilder.Entity<PasswordResetToken>(entity =>
            {
                entity.ToTable("PasswordResetTokens");
                entity.HasKey(t => t.IdToken);

                entity.Property(t => t.Codigo)
                    .HasMaxLength(10)
                    .IsRequired();

                entity.HasOne(t => t.Usuario)
                    .WithMany()
                    .HasForeignKey(t => t.IdUsuario)
                    .HasPrincipalKey(u => u.IdUsuario);
            });
        }
    }
}