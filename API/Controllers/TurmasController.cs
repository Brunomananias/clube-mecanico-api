// API/Controllers/TurmasController.cs
using Microsoft.AspNetCore.Mvc;
using ClubeMecanico.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using ClubeMecanico_API.API.DTOs.Requests;
using ClubeMecanico_API.Domain.Interfaces;

namespace ClubeMecanico_API.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TurmasController : ControllerBase
    {
        private readonly ITurmaService _turmaService;

        public TurmasController(ITurmaService turmaService)
        {
            _turmaService = turmaService;
        }

        // POST: api/turmas
        [HttpPost]
        public async Task<ActionResult<ApiResponse>> CriarTurma([FromBody] CriarTurmaRequest turmaDto)
        {
            try
            {
                var turma = await _turmaService.CriarTurmaAsync(turmaDto);

                return CreatedAtAction(nameof(GetTurmaById), new { id = turma.Id }, new ApiResponse
                {
                    Success = true,
                    Mensagem = "Turma criada com sucesso",
                    Dados = new
                    {
                        turma.Id,
                        turma.CursoId,
                        turma.DataInicio,
                        turma.DataFim,
                        turma.Horario,
                        turma.Professor,
                        turma.VagasTotal,
                        turma.VagasDisponiveis,
                        turma.Status
                    }
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
            catch (ApplicationException ex)
            {
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Mensagem = ex.Message
                });
            }
        }

        // GET: api/turmas/curso/{cursoId}
        [HttpGet("curso/{cursoId}")]
        public async Task<ActionResult<ApiResponse>> GetTurmasPorCurso(int cursoId)
        {
            try
            {
                var turmas = await _turmaService.GetTurmasByCursoIdAsync(cursoId);

                return Ok(new ApiResponse
                {
                    Success = true,
                    Dados = turmas.Select(t => new
                    {
                        t.Id,
                        t.CursoId,
                        t.DataInicio,
                        t.DataFim,
                        t.Horario,
                        t.Professor,
                        t.VagasTotal,
                        t.VagasDisponiveis,
                        t.Status
                    })
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Mensagem = "Erro ao buscar turmas"
                });
            }
        }

        // GET: api/turmas/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse>> GetTurmaById(int id)
        {
            try
            {
                var turma = await _turmaService.GetTurmaByIdAsync(id);

                if (turma == null)
                {
                    return NotFound(new ApiResponse
                    {
                        Success = false,
                        Mensagem = "Turma não encontrada"
                    });
                }

                return Ok(new ApiResponse
                {
                    Success = true,
                    Dados = new
                    {
                        turma.Id,
                        turma.CursoId,
                        turma.DataInicio,
                        turma.DataFim,
                        turma.Horario,
                        turma.Professor,
                        turma.VagasTotal,
                        turma.VagasDisponiveis,
                        turma.Status
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Mensagem = "Erro ao buscar turma"
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> BuscarTurmas()
        {
            try
            {
                var turmas = await _turmaService.BuscarTurmas();

                if (turmas == null || !turmas.Any())
                {
                    return NotFound(new ApiResponse
                    {
                        Success = false,
                        Mensagem = "Nenhuma turma encontrada"
                    });
                }

                return Ok(turmas); // ← Adicione Ok() aqui
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Mensagem = "Erro ao buscar turmas"
                });
            }
        }

        // POST: api/turmas/{turmaId}/verificar-vagas
        [HttpPost("{turmaId}/verificar-vagas")]
        public async Task<ActionResult<ApiResponse>> VerificarVagas(int turmaId)
        {
            try
            {
                var temVagas = await _turmaService.VerificarVagasDisponiveisAsync(turmaId);

                return Ok(new ApiResponse
                {
                    Success = true,
                    Dados = new { temVagas }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Mensagem = "Erro ao verificar vagas"
                });
            }
        }
    }
}