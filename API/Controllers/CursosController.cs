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

                var curso = await _cursoService.CriarCursoAsync(cursoDto);

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

        [HttpGet("certificado")]
        public async Task<IActionResult> BuscarCertificado(int cursoAlunoId)
        {
            try
            {
                var certificado = await _cursoService.BuscarCertificado(cursoAlunoId);
                return Ok(new ApiResponse
                {
                    Success = true,
                    Dados = certificado
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
                var matricula = await _cursoService.MatricularAlunoAsync(matriculaDto, 16);

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

        [HttpPost("cadastrar-certificado")]
        public async Task<IActionResult> CadastrarCertificado([FromBody] AdicionarCertificadoRequest request)
        {
            try
            {
                 await _cursoService.AdicionarCertificado(request);

                return Ok(new ApiResponse
                {
                    Success = true,
                    Mensagem = "Certificado cadastrado com sucesso!",
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
        [HttpPut("{id}")]
        public async Task<IActionResult> AtualizarCurso(int id, [FromBody] AtualizarCursoDTO cursoDto)
        {
            try
            {
                // Chamar o service
                var cursoAtualizado = await _cursoService.AtualizarCursoAsync(id, cursoDto);

                // Retornar resposta
                return Ok(new
                {
                    success = true,
                    message = "Curso atualizado com sucesso",
                    data = new
                    {
                        cursoAtualizado.Id,
                        cursoAtualizado.Nome,
                        cursoAtualizado.Descricao,
                        cursoAtualizado.DescricaoDetalhada,
                        cursoAtualizado.Valor,
                        cursoAtualizado.DuracaoHoras,
                        cursoAtualizado.Nivel,
                        cursoAtualizado.MaxAlunos,
                        cursoAtualizado.CertificadoDisponivel
                    }
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                // Log de erro (você pode usar ILogger aqui)
                Console.WriteLine($"Erro ao atualizar curso: {ex.Message}");

                return StatusCode(500, new
                {
                    success = false,
                    message = "Erro interno ao atualizar curso"
                });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletarCurso(int id)
        {
            await _cursoService.DeletarCurso(id);
            return Ok();
        }
       

    }
}