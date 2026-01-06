// API/Controllers/CursosController.cs
using ClubeMecanico.Application.Interfaces;
using ClubeMecanico_API.API.DTOs;
using ClubeMecanico_API.API.DTOs.Requests;
using ClubeMecanico_API.Domain.Entities;
using ClubeMecanico_API.Domain.Interfaces;
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

        [HttpGet("buscarCursosAlunos")]
        public async Task<IActionResult> BuscarCursosAlunos(int idAluno)
        {
            try
            {
                var cursos = await _cursoService.BuscarCursosAlunos(idAluno);
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

        [HttpGet("{id}/turmas")]
        public async Task<IActionResult> GetTurmasDoCurso(int id)
        {
            try
            {
                var turmas = await _cursoService.GetTurmasByCursoIdAsync(id);

                if (turmas == null || !turmas.Any())
                {
                    return Ok(new ApiResponse
                    {
                        Success = true,
                        Mensagem = "Nenhuma turma disponível para este curso",
                        Dados = new List<Turma>()
                    });
                }

                return Ok(new ApiResponse
                {
                    Success = true,
                    Dados = turmas
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao buscar turmas: {ex.Message}");
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Mensagem = "Erro interno ao buscar turmas"
                });
            }
        }

        // API/Controllers/CursosController.cs - Adicione este método
        [HttpPost("matricular-aluno")]
        public async Task<IActionResult> MatricularAluno([FromBody] MatricularAlunoCursoDTO matriculaDto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                // Obter ID do usuário autenticado (aluno ou admin)
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int usuarioId))
                    return Unauthorized(new { message = "Usuário não autenticado" });

                // Verificar se o usuário tem permissão (aluno ou administrador)
                var roleClaim = User.FindFirst(ClaimTypes.Role);
                var isAdmin = roleClaim?.Value == "1" || roleClaim?.Value == "Administrador";

                // Se não for admin, só pode se matricular ele mesmo
                if (!isAdmin && usuarioId != matriculaDto.AlunoId)
                    return Forbid();

                var matricula = await _cursoService.MatricularAlunoAsync(matriculaDto, usuarioId);

                return Ok(new ApiResponse
                {
                    Success = true,
                    Mensagem = "Aluno matriculado com sucesso!",
                    Dados = matricula
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Mensagem = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new ApiResponse
                {
                    Success = false,
                    Mensagem = ex.Message
                });
            }
            catch (ApplicationException ex)
            {
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Mensagem = ex.Message
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao matricular aluno: {ex.Message}");
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Mensagem = "Erro interno no servidor ao realizar matrícula"
                });
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