// API/Controllers/CursosController.cs
using ClubeMecanico.Application.Interfaces;
using ClubeMecanico_API.API.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ClubeMecanico_API.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CursosController : ControllerBase
    {
        private readonly ICursoService _cursoService;

        public CursosController(ICursoService cursoService)
        {
            _cursoService = cursoService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var cursos = await _cursoService.GetAllCursosAsync();
                return Ok(cursos);
            }
            catch (ApplicationException ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erro interno no servidor" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                if (id <= 0)
                    return BadRequest(new { message = "ID inválido" });

                var curso = await _cursoService.GetCursoByIdAsync(id);

                if (curso == null)
                    return NotFound(new { message = $"Curso com ID {id} não encontrado" });

                return Ok(curso);
            }
            catch (ApplicationException ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erro interno no servidor" });
            }
        }

        [HttpPost]
        [Authorize] // Apenas usuários autenticados
        public async Task<IActionResult> Create([FromBody] CriarCursoDTO cursoDto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                // Obter ID do usuário autenticado
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int adminId))
                    return Unauthorized(new { message = "Usuário não autenticado" });

                var curso = await _cursoService.CriarCursoAsync(cursoDto, adminId);

                return CreatedAtAction(nameof(GetById), new { id = curso.Id }, curso);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (ApplicationException ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erro interno no servidor" });
            }
        }

        // Comente os outros métodos por enquanto
        /*
        [HttpPut("{id}")]
        [Authorize(Roles = "1")]
        public async Task<IActionResult> Update(int id, [FromBody] AtualizarCursoDTO cursoDto)
        {
            // Implementar depois
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> Delete(int id)
        {
            // Implementar depois
        }

        [HttpGet("{id}/turmas")]
        public async Task<IActionResult> GetTurmas(int id)
        {
            // Implementar depois
        }

        [HttpGet("{id}/conteudos")]
        public async Task<IActionResult> GetConteudos(int id)
        {
            // Implementar depois
        }
        */
    }
}