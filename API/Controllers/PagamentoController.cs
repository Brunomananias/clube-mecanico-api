using Microsoft.AspNetCore.Mvc;
using MercadoPago.Client.Preference;
using MercadoPago.Config;
using MercadoPago.Resource.Preference;
using ClubeMecanico_API.API.DTOs;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using ClubeMecanico_API.Infrastructure.Data;
using ClubeMecanico_API.Domain.Entities;
using ClubeMecanico_API.Models;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class PagamentoController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _context;
    private readonly ILogger<PagamentoController> _logger;
    // Injeta o IConfiguration no construtor
    public PagamentoController(
       IConfiguration configuration,
       AppDbContext context,
       ILogger<PagamentoController> logger)
    {
        _configuration = configuration;
        _context = context;
        _logger = logger;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromQuery] string topic, [FromQuery] string id)
    {
        try
        {
            // Configurar token
            var accessToken = _configuration["MercadoPago:AccessToken"];
            MercadoPagoConfig.AccessToken = accessToken;

            // Buscar o pagamento no Mercado Pago
            var client = new MercadoPago.Client.Payment.PaymentClient();
            var payment = await client.GetAsync(long.Parse(id));

            if (payment == null)
                return NotFound();

            // Buscar pedido no seu banco usando o ExternalReference
            var pedidoId = payment.ExternalReference;

            var pedido = await _context.Pedidos
                .FirstOrDefaultAsync(p => p.Id == int.Parse(pedidoId));

            if (pedido == null)
                return NotFound("Pedido não encontrado");

            // Carregar itens do pedido
            var itensPedido = await _context.ItensPedido
                .Where(ip => ip.PedidoId == pedido.Id)
                .ToListAsync();

            // Atualizar status do pagamento
            var pagamento = await _context.Pagamentos
                .FirstOrDefaultAsync(p => p.PedidoId == pedido.Id);

            if (pagamento != null)
            {
                pagamento.Status = payment.Status.ToString();
                pagamento.MpPaymentId = payment.Id?.ToString();
                pagamento.DataCriacao = DateTime.UtcNow;

                // CORREÇÃO: Verificar status como string
                var status = payment.Status.ToString().ToLower();

                if (status == "approved")
                {
                    pagamento.DataPagamento = DateTime.UtcNow;

                    // Atualizar status do pedido
                    pedido.Status = "Aprovado";

                    // Matricular o aluno nos cursos
                    await MatricularAluno(pedido.Id, pedido.AlunoId, itensPedido);
                }
                else if (status == "cancelled" || status == "rejected")
                {
                    pedido.Status = "Cancelado";
                }
                else if (status == "pending")
                {
                    pedido.Status = "Pendente";
                }
                else if (status == "in_process" || status == "in_mediation")
                {
                    pedido.Status = "EmProcessamento";
                }
                else if (status == "refunded" || status == "charged_back")
                {
                    pedido.Status = "Reembolsado";
                }

                await _context.SaveChangesAsync();
            }

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no webhook do Mercado Pago");
            return StatusCode(500);
        }
    }

    private async Task MatricularAluno(int pedidoId, int alunoId, List<ItemPedido> itensPedido)
    {
        if (itensPedido == null || !itensPedido.Any())
            return;

        // Para cada item no pedido, matricular o aluno
        foreach (var item in itensPedido)
        {
            var matricula = new CursoAluno
            {
                AlunoId = alunoId,
                CursoId = item.CursoId,
                DataMatricula = DateTime.UtcNow,
                Status = "Ativo"
            };

            _context.CursosAlunos.Add(matricula);
        }

        // Limpar carrinho temporário do aluno
        var carrinhoItens = await _context.CarrinhoTemporario
            .Where(c => c.UsuarioId == alunoId)
            .ToListAsync();

        if (carrinhoItens.Any())
        {
            _context.CarrinhoTemporario.RemoveRange(carrinhoItens);
        }

        await _context.SaveChangesAsync();
    }


    [HttpPost("criar-pagamento")]
    [Authorize]
    public async Task<IActionResult> CriarPagamento([FromBody] PagamentoRequest request)
    {
        try
        {
            // 1. OBTER ALUNO LOGADO
            var alunoIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (alunoIdClaim == null)
            {
                return Unauthorized(new { success = false, message = "Usuário não autenticado" });
            }
            var alunoId = alunoIdClaim.Value;

            // 2. BUSCAR ITENS DO CARRINHO
            var carrinhoItens = await _context.CarrinhoTemporario
                .Include(ct => ct.Curso)
                .Where(c => c.UsuarioId.ToString() == alunoId)
                .ToListAsync();

            if (!carrinhoItens.Any())
            {
                return BadRequest(new { success = false, message = "Carrinho vazio" });
            }

            // 3. CALCULAR VALORES
            var subtotal = carrinhoItens.Sum(c => c.Curso?.Valor ?? 0);

            // 4. GERAR NÚMERO DO PEDIDO
            var numeroPedido = $"PED-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";

            // 5. CRIAR PEDIDO NO BANCO
            var pedido = new Pedido
            {
                NumeroPedido = numeroPedido,
                AlunoId = Convert.ToInt16(alunoId),
                Subtotal = subtotal,
                Status = "pendente",
                DataPedido = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.Pedidos.Add(pedido);
            await _context.SaveChangesAsync(); // SALVA PARA TER O ID

            // 6. CRIAR ITENS DO PEDIDO
            foreach (var item in carrinhoItens)
            {
                var itemPedido = new ItemPedido
                {
                    PedidoId = pedido.Id,
                    CursoId = item.CursoId,
                    TurmaId = item.TurmaId,
                    Preco = item.Curso?.Valor ?? 0,
                    Quantidade = 1,
                    NomeCurso = item.Curso?.Nome,
                    Duracao = item.Curso?.DuracaoHoras,
                    DataCompra = DateTime.Now
                };

                _context.ItensPedido.Add(itemPedido);
            }

            // 7. CRIAR REGISTRO DE PAGAMENTO
            var pagamento = new Pagamento
            {
                PedidoId = pedido.Id,
                MetodoPagamento = request.MetodoPagamento ?? "MercadoPago",
                Status = "pendente",
                DataCriacao = DateTime.Now,
            };

            _context.Pagamentos.Add(pagamento);
            await _context.SaveChangesAsync(); // SALVA PARA TER O ID

            // 8. CONFIGURAR MERCADO PAGO
            var accessToken = _configuration["MercadoPago:AccessToken"];
            MercadoPagoConfig.AccessToken = accessToken;

            // 9. CRIAR ITENS PARA O MERCADO PAGO
            var items = new List<PreferenceItemRequest>();
            foreach (var item in carrinhoItens)
            {
                items.Add(new PreferenceItemRequest
                {
                    Title = item.Curso?.Nome ?? "Curso Clube do Mecânico",
                    Description = $"Curso: {item.Curso?.Nome}",
                    Quantity = 1,
                    CurrencyId = "BRL",
                    UnitPrice = item.Curso?.Valor ?? 0
                });
            }

            // 10. CRIAR PREFERÊNCIA NO MERCADO PAGO
            var preferenceRequest = new PreferenceRequest
            {
                Items = items,
                BackUrls = new PreferenceBackUrlsRequest
                {
                    Success = "https://8a315ad669e0.ngrok-free.app/pagamento/sucesso?id=" + pedido.Id,
                    Failure = "https://8a315ad669e0.ngrok-free.app/pagamento/falha?id=" + pedido.Id,
                    Pending = "https://8a315ad669e0.ngrok-free.app/pagamento/pendente?id=" + pedido.Id
                },
                AutoReturn = "approved",
                ExternalReference = pedido.Id.ToString(), // LINK COM SEU PEDIDO
                NotificationUrl = "https://9a528a07b1b2.ngrok-free.app/api/pagamento/webhook", // WEBHOOK
                StatementDescriptor = "CLUBE MECANICO"
            };

            // 11. CRIAR NO MERCADO PAGO
            var client = new PreferenceClient();
            Preference preference = await client.CreateAsync(preferenceRequest);

            // 12. ATUALIZAR PEDIDO COM DADOS DO MP
            pedido.MpPreferenceId = preference.Id;
            pedido.LinkPagamento = preference.InitPoint;
            pagamento.CodigoTransacao = preference.Id;

            await _context.SaveChangesAsync();

            // 13. LOG PARA DEBUG
            _logger.LogInformation($"Pedido {pedido.Id} criado com Preference ID: {preference.Id}");
            _logger.LogInformation($"URL de pagamento: {preference.InitPoint}");
            _logger.LogInformation($"Webhook configurado: {preferenceRequest.NotificationUrl}");

            // 14. RETORNAR PARA O FRONTEND
            return Ok(new
            {
                success = true,
                url = preference.InitPoint,
                pedidoId = pedido.Id,
                preferenceId = preference.Id,
                numeroPedido = pedido.NumeroPedido,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar pagamento");
            return BadRequest(new
            {
                success = false,
                message = ex.Message
            });
        }
    }
}

