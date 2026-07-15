using Fri3ndsDrive.Api.Data;
using Fri3ndsDrive.Api.DTOs;
using Fri3ndsDrive.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fri3ndsDrive.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AuthController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("registro")]
        public async Task<IActionResult> Registro([FromBody] RegistroDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Nombre) ||
                string.IsNullOrWhiteSpace(dto.Correo) ||
                string.IsNullOrWhiteSpace(dto.Password))
            {
                return BadRequest(new { mensaje = "Todos los campos son obligatorios." });
            }

            var existeUsuario = await _context.Usuarios
                .AnyAsync(u => u.Correo == dto.Correo);

            if (existeUsuario)
            {
                return BadRequest(new { mensaje = "Este correo ya está registrado." });
            }

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            var usuario = new Usuario
            {
                Nombre = dto.Nombre,
                Correo = dto.Correo,
                PasswordHash = passwordHash
            };

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = "Usuario registrado correctamente.",
                usuario.IdUsuario,
                usuario.Nombre,
                usuario.Correo
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO dto)
        {
            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Correo == dto.Correo);

            if (usuario == null)
            {
                return Unauthorized(new { mensaje = "Correo o contraseña incorrectos." });
            }

            bool passwordValida = BCrypt.Net.BCrypt.Verify(dto.Password, usuario.PasswordHash);

            if (!passwordValida)
            {
                return Unauthorized(new { mensaje = "Correo o contraseña incorrectos." });
            }

            return Ok(new
            {
                mensaje = "Inicio de sesión correcto.",
                usuario.IdUsuario,
                usuario.Nombre,
                usuario.Correo
            });
        }

        [HttpPost("solicitar-recuperacion")]
        public async Task<IActionResult> SolicitarRecuperacion([FromBody] RecuperacionDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Correo))
            {
                return BadRequest(new { mensaje = "El correo es obligatorio." });
            }

            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Correo == dto.Correo);

            if (usuario == null)
            {
                return Ok(new
                {
                    mensaje = "Si el correo está registrado, se generará un código de recuperación."
                });
            }

            string codigo = new Random().Next(100000, 999999).ToString();

            var token = new PasswordResetToken
            {
                IdUsuario = usuario.IdUsuario,
                Codigo = codigo,
                Expira = DateTime.Now.AddMinutes(10),
                Usado = false
            };

            _context.PasswordResetTokens.Add(token);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = "Código de recuperación generado correctamente.",
                codigoTemporal = codigo
            });
        }

        [HttpPost("restablecer-password")]
        public async Task<IActionResult> RestablecerPassword([FromBody] RestablecerPasswordDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Correo) ||
                string.IsNullOrWhiteSpace(dto.Codigo) ||
                string.IsNullOrWhiteSpace(dto.NuevaPassword))
            {
                return BadRequest(new { mensaje = "Todos los campos son obligatorios." });
            }

            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Correo == dto.Correo);

            if (usuario == null)
            {
                return BadRequest(new { mensaje = "Código o correo incorrecto." });
            }

            var token = await _context.PasswordResetTokens
                .Where(t => t.IdUsuario == usuario.IdUsuario &&
                            t.Codigo == dto.Codigo &&
                            t.Usado == false &&
                            t.Expira > DateTime.Now)
                .OrderByDescending(t => t.Expira)
                .FirstOrDefaultAsync();

            if (token == null)
            {
                return BadRequest(new { mensaje = "El código es inválido o ya expiró." });
            }

            usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NuevaPassword);
            token.Usado = true;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = "Contraseña restablecida correctamente. Ya puedes iniciar sesión."
            });
        }
    }
}