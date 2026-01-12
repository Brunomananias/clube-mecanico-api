// Controllers/CarrinhoController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClubeMecanico_API.Infrastructure.Data; // Seu AppDbContext
using ClubeMecanico_API.Domain.Entities;
using System;
using System.Linq;
using System.Threading.Tasks;
using ClubeMecanico.Application.Interfaces;
using ClubeMecanico_API.API.DTOs.Requests;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace ClubeMecanico_API.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CarrinhoController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ICursoService _cursoService; // Se você tiver este serviço

        private int GetUserId()
        {
            // Tenta várias formas de pegar o userId do token
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                             ?? User.FindFirst("id")?.Value
                             ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                             ?? User.FindFirst("userId")?.Value
                             ?? User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                throw new UnauthorizedAccessException("Token não contém ID do usuário");
            }

            if (!int.TryParse(userIdClaim, out int userId))
            {
                throw new UnauthorizedAccessException("ID do usuário inválido no token");
            }

            return userId;
        }

        public CarrinhoController(AppDbContext context, ICursoService cursoService)
        {
            _context = context;
            _cursoService = cursoService;
        }
        // POST: api/carrinho/adicionar
        [HttpPost("adicionar")]
        public async Task<IActionResult> AdicionarAoCarrinho([FromBody] AdicionarCarrinhoRequest request)
        {
            try
            {               
                var curso = await _context.Cursos
                    .FirstOrDefaultAsync(c => c.Id == request.CursoId);

                if (curso == null)
                {
                    return BadRequest(new ApiResponse
                    {
                        Success = false,
                        Mensagem = "Curso não encontrado"
                    });
                }

                // 3. Verificar se já está no carrinho
                var itemExistente = await _context.CarrinhoTemporario
               .FirstOrDefaultAsync(ct =>
                   ct.UsuarioId == request.UsuarioId &&
                   ct.CursoId == request.CursoId &&
                   ct.TurmaId == request.TurmaId);

                if (itemExistente != null)
                {
                    return Ok(new ApiResponse
                    {
                        Success = true,
                        Mensagem = "Curso já está no carrinho",
                        Dados = new { carrinhoId = itemExistente.Id }
                    });
                }

                // 4. Adicionar novo item ao carrinho
                var novoItem = new CarrinhoTemporario
                {
                    UsuarioId = request.UsuarioId,
                    CursoId = request.CursoId,
                    TurmaId = request.TurmaId,
                    DataAdicao = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified)
                };

                _context.CarrinhoTemporario.Add(novoItem);
                await _context.SaveChangesAsync();

                // 5. Obter contagem atual do carrinho
                var itensCarrinho = await _context.CarrinhoTemporario
                    .Where(ct => ct.UsuarioId == request.UsuarioId)
                    .CountAsync();

                return Ok(new ApiResponse
                {
                    Success = true,
                    Mensagem = "Curso adicionado ao carrinho com sucesso",
                    Dados = new
                    {
                        carrinhoId = novoItem.Id,
                        totalItens = itensCarrinho,
                        curso = new
                        {
                            id = curso.Id,
                            titulo = curso.Nome,
                            valor = curso.Valor,
                            imagem = curso.FotoUrl
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                // Log do erro
                Console.WriteLine($"Erro ao adicionar ao carrinho: {ex.Message}");

                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Mensagem = "Erro interno ao adicionar ao carrinho"
                });
            }
        }

        [HttpGet("itens")]
        public async Task<IActionResult> ObterItensCarrinho(long usuarioId)
        {
            try
            {
                var itens = await _context.CarrinhoTemporario
                    .Where(ct => ct.UsuarioId == usuarioId)
                    .Include(ct => ct.Curso)
                    .Select(ct => new CarrinhoItemResponse
                    {
                        Id = ct.Id,
                        CursoId = ct.CursoId,
                        Titulo = ct.Curso.Nome, // Note: Nome, não Titulo
                        Descricao = ct.Curso.Descricao,
                        Valor = ct.Curso.Valor,
                        Imagem = ct.Curso.FotoUrl, // Note: FotoUrl, não Imagem
                        DataAdicao = ct.DataAdicao
                    })
                    .ToListAsync();

                var total = itens.Sum(item => item.Valor);

                return Ok(new ApiResponse
                {
                    Success = true,
                    Dados = new
                    {
                        itens = itens,
                        totalItens = itens?.Count,
                        valorTotal = total
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao obter itens do carrinho: {ex.Message}");
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Mensagem = "Erro interno ao carregar carrinho"
                });
            }
        }

            // DELETE: api/carrinho/remover/{id}
            [HttpDelete("remover/{id}")]
        public async Task<IActionResult> RemoverDoCarrinho(int id)
        {
            try
            {
                var item = await _context.CarrinhoTemporario
                    .FirstOrDefaultAsync(ct => ct.Id == id);

                if (item == null)
                {
                    return NotFound(new ApiResponse
                    {
                        Success = false,
                        Mensagem = "Item não encontrado no carrinho"
                    });
                }

                _context.CarrinhoTemporario.Remove(item);
                await _context.SaveChangesAsync();

                return Ok(new ApiResponse
                {
                    Success = true,
                    Mensagem = "Item removido do carrinho"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao remover do carrinho: {ex.Message}");
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Mensagem = "Erro interno ao remover item"
                });
            }
        }
    }
}