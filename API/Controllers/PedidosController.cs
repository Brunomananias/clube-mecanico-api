using ClubeMecanico.Application.Interfaces;
using ClubeMecanico_API.API.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ClubeMecanico_API.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PedidosController : ControllerBase
    {
        private readonly IPedidoService _pedidoService;

        public PedidosController(IPedidoService pedidoService)
        {
            _pedidoService = pedidoService;
        }

        [HttpGet]
        public async Task<IActionResult> GetPedidosUsuario()
        {
            var alunoId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            var pedidos = await _pedidoService.GetPedidosByAlunoIdAsync(alunoId);
            return Ok(pedidos);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var alunoId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            var pedido = await _pedidoService.GetPedidoByIdAsync(id);

            if (pedido == null || pedido.AlunoId != alunoId)
                return NotFound();

            return Ok(pedido);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CriarPedidoDTO pedidoDto)
        {
            var alunoId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            var pedido = await _pedidoService.CriarPedidoAsync(pedidoDto, alunoId);

            return CreatedAtAction(nameof(GetById), new { id = pedido.Id }, pedido);
        }

        [HttpPost("{id}/pagamento")]
        public async Task<IActionResult> ProcessarPagamento(int id, [FromBody] ProcessarPagamentoDTO pagamentoDto)
        {
            var alunoId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            var resultado = await _pedidoService.ProcessarPagamentoAsync(id, pagamentoDto, alunoId);

            if (!resultado.Sucesso)
                return BadRequest(new { message = resultado.Mensagem });

            return Ok(resultado);
        }
    }
}
