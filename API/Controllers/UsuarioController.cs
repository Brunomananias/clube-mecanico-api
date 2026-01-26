using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YourProject.DTOs;
using ClubeMecanico_API.Domain.Entities;
using ClubeMecanico_API.Domain.Interfaces;

namespace YourProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsuariosController : ControllerBase
    {
        private readonly IUsuarioService _usuarioService;

        public UsuariosController(IUsuarioService usuarioService)
        {
            _usuarioService = usuarioService;
        }

        // GET: api/usuarios
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UsuarioResponse>>> GetUsuarios()
        {
            try
            {
                var usuarios = await _usuarioService.GetAllUsuariosComEnderecosAsync();
                var response = usuarios.Select(u => new UsuarioResponse(u)).ToList();
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erro interno: {ex.Message}" });
            }
        }

        // GET: api/usuarios/5
        [HttpGet("{id}")]
        public async Task<ActionResult<UsuarioResponse>> GetUsuario(int id)
        {
            try
            {
                var usuario = await _usuarioService.GetUsuarioComEnderecoAsync(id);

                if (usuario == null)
                {
                    return NotFound(new { message = "Usuário não encontrado" });
                }
                return Ok(new UsuarioResponse(usuario));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erro interno: {ex.Message}" });
            }
        }

        // POST: api/usuarios
        [HttpPost]
        [AllowAnonymous]
        public async Task<ActionResult<UsuarioResponse>> PostUsuario([FromBody] CreateUsuarioRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Converter request para model
                var usuario = new Usuario
                {
                    Email = request.Email,
                    SenhaHash = request.Senha, // Será hashado no service
                    Nome_Completo = request.NomeCompleto,
                    CPF = request.CPF,
                    Telefone = request.Telefone,
                    Data_Nascimento = request.DataNascimento,
                    Tipo = request.Tipo ?? 1 // 1 = Aluno por padrão
                };

                var usuarioCriado = await _usuarioService.CreateUsuarioAsync(usuario);

                return CreatedAtAction(nameof(GetUsuario),
                    new { id = usuarioCriado.Id },
                    new UsuarioResponse(usuarioCriado));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erro ao criar usuário: {ex.Message}" });
            }
        }

        // PUT: api/usuarios/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutUsuario(int id, [FromBody] UpdateUsuarioRequest request)
        {
            try
            {
                // Verifica permissão
                var userId = int.Parse(User.FindFirst("id")?.Value ?? "0");
                var userRole = User.FindFirst("role")?.Value;

                if (userId != id && userRole != "admin")
                {
                    return Forbid();
                }

                var usuarioAtualizado = new Usuario
                {
                    Nome_Completo = request.NomeCompleto,
                    CPF = request.CPF,
                    Telefone = request.Telefone,
                    Data_Nascimento = request.DataNascimento,
                    Ativo = request.Ativo ?? true
                };

                await _usuarioService.UpdateUsuarioAsync(id, usuarioAtualizado);

                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erro ao atualizar usuário: {ex.Message}" });
            }
        }

        // DELETE: api/usuarios/5
        [HttpDelete("{id}")]
        [Authorize] 
        public async Task<IActionResult> DeleteUsuario(int id)
        {
            try
            {
                await _usuarioService.DeleteUsuarioAsync(id);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erro ao deletar usuário: {ex.Message}" });
            }
        }

        // GET: api/usuarios/email/teste@email.com
        [HttpGet("email/{email}")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<UsuarioResponse>> GetUsuarioByEmail(string email)
        {
            try
            {
                var usuario = await _usuarioService.GetUsuarioByEmailAsync(email);

                if (usuario == null)
                {
                    return NotFound(new { message = "Usuário não encontrado" });
                }

                return Ok(new UsuarioResponse(usuario));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erro interno: {ex.Message}" });
            }
        }

        // ========== ENDEREÇOS ==========

        // POST: api/usuarios/5/enderecos
        [HttpPost("{usuarioId}/enderecos")]
        public async Task<ActionResult<UsuarioResponse>> AddEndereco(int usuarioId, [FromBody] CreateEnderecoRequest request)
        {
            try
            {
                // Verifica permissão
                var userId = int.Parse(User.FindFirst("id")?.Value ?? "0");
                var userRole = User.FindFirst("role")?.Value;

                if (userId != usuarioId && userRole != "admin")
                {
                    return Forbid();
                }

                var endereco = new Endereco
                {
                    CEP = request.CEP,
                    Logradouro = request.Logradouro,
                    Numero = request.Numero,
                    Complemento = request.Complemento,
                    Bairro = request.Bairro,
                    Cidade = request.Cidade,
                    Estado = request.Estado,
                    Tipo = request.Tipo ?? "principal"
                };

                var usuario = await _usuarioService.AddEnderecoToUsuarioAsync(usuarioId, endereco);

                return Ok(new UsuarioResponse(usuario));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erro ao adicionar endereço: {ex.Message}" });
            }
        }

        // PUT: api/usuarios/5/enderecos/10
        [HttpPut("{usuarioId}/enderecos/{enderecoId}")]
        public async Task<ActionResult<UsuarioResponse>> UpdateEndereco(int usuarioId, int enderecoId, [FromBody] UpdateEnderecoRequest request)
        {
            try
            {
                // Verifica permissão
                var userId = int.Parse(User.FindFirst("id")?.Value ?? "0");
                var userRole = User.FindFirst("role")?.Value;

                if (userId != usuarioId && userRole != "admin")
                {
                    return Forbid();
                }

                var enderecoAtualizado = new Endereco
                {
                    CEP = request.CEP,
                    Logradouro = request.Logradouro,
                    Numero = request.Numero,
                    Complemento = request.Complemento,
                    Bairro = request.Bairro,
                    Cidade = request.Cidade,
                    Estado = request.Estado,
                    Tipo = request.Tipo,
                    Ativo = request.Ativo ?? true
                };

                var endereco = await _usuarioService.UpdateEnderecoAsync(usuarioId, enderecoId, enderecoAtualizado);

                var usuario = await _usuarioService.GetUsuarioComEnderecoAsync(usuarioId);
                return Ok(new UsuarioResponse(usuario));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erro ao atualizar endereço: {ex.Message}" });
            }
        }

        // DELETE: api/usuarios/5/enderecos/10
        [HttpDelete("{usuarioId}/enderecos/{enderecoId}")]
        public async Task<IActionResult> RemoveEndereco(int usuarioId, int enderecoId)
        {
            try
            {
                // Verifica permissão
                var userId = int.Parse(User.FindFirst("id")?.Value ?? "0");
                var userRole = User.FindFirst("role")?.Value;

                if (userId != usuarioId && userRole != "admin")
                {
                    return Forbid();
                }

                await _usuarioService.RemoveEnderecoAsync(usuarioId, enderecoId);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erro ao remover endereço: {ex.Message}" });
            }
        }
    }
}