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
using MercadoPago.Client.Common;
using MercadoPago.Client.Payment;
using MercadoPago.Client;
using System.Text.Json;

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
    [AllowAnonymous]
    public async Task<IActionResult> Webhook()
    {
        try
        {
            // 1. LER O BODY COMPLETO
            using var reader = new StreamReader(Request.Body);
            var json = await reader.ReadToEndAsync();

            _logger.LogInformation($"=== WEBHOOK RECEBIDO ===");
            _logger.LogInformation($"Body: {json}");

            // 2. DESSERIALIZAR
            var webhookData = JsonSerializer.Deserialize<JsonDocument>(json);

            // 3. EXTRAIR DADOS
            var type = webhookData.RootElement.GetProperty("type").GetString();
            var action = webhookData.RootElement.GetProperty("action").GetString();
            var dataId = webhookData.RootElement.GetProperty("data").GetProperty("id").GetString();

            _logger.LogInformation($"Tipo: {type}, Ação: {action}, DataId: {dataId}");

            // 4. CONFIGURAR TOKEN
            var accessToken = _configuration["MercadoPago:AccessToken"];
            MercadoPagoConfig.AccessToken = accessToken;

            // 5. SÓ PROCESSAR SE FOR PAGAMENTO
            if (type != "payment")
            {
                _logger.LogInformation($"Webhook do tipo {type} ignorado.");
                return Ok();
            }

            // 6. BUSCAR PAGAMENTO NO MERCADO PAGO
            var client = new MercadoPago.Client.Payment.PaymentClient();

            if (!long.TryParse(dataId, out long paymentId))
            {
                _logger.LogError($"ID de pagamento inválido: {dataId}");
                return BadRequest("ID inválido");
            }

            MercadoPago.Resource.Payment.Payment payment;

            try
            {
                payment = await client.GetAsync(paymentId);
                _logger.LogInformation($"Pagamento encontrado: {payment.Id}, Status: {payment.Status}");
            }
            catch (MercadoPago.Error.MercadoPagoApiException ex) when (ex.StatusCode == 404)
            {
                _logger.LogWarning($"Pagamento {paymentId} não encontrado.");
                return Ok();
            }

            // 7. VERIFICAR EXTERNAL REFERENCE (seu PedidoId)
            if (string.IsNullOrEmpty(payment.ExternalReference))
            {
                _logger.LogError($"ExternalReference não encontrado no pagamento {paymentId}");
                return BadRequest("ExternalReference não encontrado");
            }

            if (!int.TryParse(payment.ExternalReference, out int pedidoId))
            {
                _logger.LogError($"ExternalReference inválido: {payment.ExternalReference}");
                return BadRequest("ExternalReference inválido");
            }

            // 8. BUSCAR PEDIDO NO BANCO
            var pedido = await _context.Pedidos
                .Include(p => p.Pagamento)
                .FirstOrDefaultAsync(p => p.Id == pedidoId);

            if (pedido == null)
            {
                _logger.LogError($"Pedido {pedidoId} não encontrado no banco");
                return NotFound("Pedido não encontrado");
            }

            _logger.LogInformation($"Pedido encontrado: {pedido.Id}, Status atual: {pedido.Status}");

            // 9. ATUALIZAR/CRIAR PAGAMENTO COM TODOS OS CAMPOS
            var pagamento = pedido.Pagamento ?? new Pagamento { PedidoId = pedido.Id };

            // Campos obrigatórios
            pagamento.Status = payment.Status.ToString();
            pagamento.MpPaymentId = payment.Id?.ToString();

            // Status detail (informações adicionais do status)
            pagamento.StatusDetail = payment.StatusDetail;

            // Tipo de pagamento e detalhes
            if (payment.PaymentTypeId != null)
            {
                pagamento.TipoPagamento = payment.PaymentTypeId;
            }

            // Método de pagamento
            if (payment.PaymentMethodId != null)
            {
                pagamento.MetodoPagamento = payment.PaymentMethodId;
            }

            // Bandeira do cartão (se for cartão)
            if (payment.Card?.Cardholder?.Name != null)
            {
                pagamento.Bandeira = payment.Card.Cardholder.Name;
            }
            else if (payment.PaymentMethodId != null)
            {
                // Para PIX, boleto, etc
                pagamento.Bandeira = payment.PaymentMethodId;
            }

            // Últimos dígitos do cartão
            if (payment.Card?.LastFourDigits != null)
            {
                pagamento.UltimosDigitos = payment.Card.LastFourDigits;
            }

            // Parcelas
            if (payment.Installments.HasValue)
            {
                pagamento.Parcelas = payment.Installments.Value;
            }

            // Valor do pagamento
            if (payment.TransactionAmount.HasValue)
            {
                pagamento.Valor = payment.TransactionAmount.Value;
            }
            else
            {
                // Se não tiver no pagamento, usar o subtotal do pedido
                pagamento.Valor = pedido.Subtotal;
            }

            // Código de transação (preference ID ou payment ID)
            if (string.IsNullOrEmpty(pagamento.CodigoTransacao))
            {
                pagamento.CodigoTransacao = payment.Id?.ToString() ?? paymentId.ToString();
            }

            // Data de pagamento (apenas se aprovado)
            if (payment.Status == MercadoPago.Resource.Payment.PaymentStatus.Approved &&
                payment.DateApproved.HasValue)
            {
                pagamento.DataPagamento = payment.DateApproved.Value;
            }
            else
            {
                pagamento.DataPagamento = DateTime.Now;
            }

            // Data de criação (se for novo pagamento)
            if (pagamento.Id == 0)
            {
                pagamento.DataCriacao = DateTime.Now;
                _context.Pagamentos.Add(pagamento);
            }
            else
            {
                pagamento.DataCriacao = DateTime.Now; // ou manter a original se já existir
            }

            // 10. ATUALIZAR STATUS DO PEDIDO
            var status = payment.Status.ToString().ToLower();

            switch (status)
            {
                case "approved":
                    pedido.Status = "aprovado";
                    pagamento.DataPagamento = DateTime.Now;

                    // Matricular aluno nos cursos
                    await MatricularAlunoNosCursos(pedido.Id, pedido.AlunoId);
                    _logger.LogInformation($"Pedido {pedidoId} APROVADO - Aluno matriculado");
                    break;

                case "pending":
                    pedido.Status = "pendente";
                    _logger.LogInformation($"Pedido {pedidoId} PENDENTE");
                    break;

                case "in_process":
                case "in_mediation":
                    pedido.Status = "em_processamento";
                    _logger.LogInformation($"Pedido {pedidoId} EM PROCESSAMENTO");
                    break;

                case "cancelled":
                case "rejected":
                    pedido.Status = "cancelado";
                    _logger.LogInformation($"Pedido {pedidoId} CANCELADO");
                    break;

                case "refunded":
                case "charged_back":
                    pedido.Status = "reembolsado";
                    _logger.LogInformation($"Pedido {pedidoId} REEMBOLSADO");
                    break;

                default:
                    pedido.Status = "desconhecido";
                    _logger.LogWarning($"Pedido {pedidoId} status desconhecido: {status}");
                    break;
            }

            pedido.UpdatedAt = DateTime.Now;

            // 11. SALVAR ALTERAÇÕES
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Pagamento salvo com sucesso:");
            _logger.LogInformation($"- ID: {pagamento.Id}");
            _logger.LogInformation($"- PedidoId: {pagamento.PedidoId}");
            _logger.LogInformation($"- Status: {pagamento.Status}");
            _logger.LogInformation($"- Valor: {pagamento.Valor}");
            _logger.LogInformation($"- Método: {pagamento.MetodoPagamento}");
            _logger.LogInformation($"- Tipo: {pagamento.TipoPagamento}");
            _logger.LogInformation($"- MP Payment ID: {pagamento.MpPaymentId}");
            _logger.LogInformation($"- Data Pagamento: {pagamento.DataPagamento}");

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no webhook do Mercado Pago");
            return Ok(); // Retornar OK mesmo em erro para o Mercado Pago não reenviar
        }
    }

    private async Task MatricularAlunoNosCursos(int pedidoId, int alunoId)
    {
        try
        {
            // Buscar itens do pedido (cursos comprados)
            var itensPedido = await _context.ItensPedido
                .Where(ip => ip.PedidoId == pedidoId)
                .Include(ip => ip.Curso)
                .Include(ip => ip.Turma)
                .ToListAsync();

            foreach (var item in itensPedido)
            {
                // Verificar se já existe registro na tabela cursos_alunos
                var cursoAlunoExistente = await _context.CursosAlunos
                    .FirstOrDefaultAsync(ca =>
                        ca.AlunoId == alunoId &&
                        ca.CursoId == item.CursoId &&
                        ca.TurmaId == item.TurmaId);

                if (cursoAlunoExistente == null)
                {
                    // Criar novo registro na tabela cursos_alunos
                    var cursoAluno = new CursoAluno
                    {
                        AlunoId = alunoId,
                        CursoId = item.CursoId,
                        TurmaId = item.TurmaId,
                        Status = "ativo", // ou "matriculado", depende da sua lógica
                        Progresso = 0, // Inicia com 0% de progresso
                        DataMatricula = DateTime.Now,
                    };

                    _context.CursosAlunos.Add(cursoAluno);
                    _logger.LogInformation($"Aluno {alunoId} matriculado no curso {item.CursoId}, turma {item.TurmaId}");
                }
                else
                {
                    // Se já existe, apenas atualizar status se necessário
                    if (cursoAlunoExistente.Status != "ativo")
                    {
                        cursoAlunoExistente.Status = "ativo";
                        cursoAlunoExistente.DataMatricula = DateTime.Now;
                        _logger.LogInformation($"Matrícula do aluno {alunoId} no curso {item.CursoId} reativada");
                    }
                    else
                    {
                        _logger.LogInformation($"Aluno {alunoId} já está matriculado no curso {item.CursoId}");
                    }
                }
            }

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Erro ao matricular aluno {alunoId} nos cursos do pedido {pedidoId}");
            throw;
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
    public async Task<IActionResult> CriarPagamento([FromBody] PagamentoRequest request)
    {
        try
        {
            var carrinhoItens = await _context.CarrinhoTemporario
                .Include(ct => ct.Curso)
                .Where(c => c.UsuarioId == request.UserId)
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
                AlunoId = Convert.ToInt16(request.UserId),
                Subtotal = subtotal,
                Status = "pendente",
                DataPedido = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.Pedidos.Add(pedido);
            await _context.SaveChangesAsync();

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
                    Duracao = item.Curso?.DuracaoHoras.ToString(),
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
            await _context.SaveChangesAsync();

            // 8. CONFIGURAR MERCADO PAGO
            var accessToken = _configuration["MercadoPago:AccessToken"];
            MercadoPagoConfig.AccessToken = accessToken;

            // 9. CRIAR ITENS PARA O MERCADO PAGO
            var items = new List<PreferenceItemRequest>();
            foreach (var item in carrinhoItens)
            {
                items.Add(new PreferenceItemRequest
                {
                    Id = item.CursoId.ToString(),
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
                    Success = "https://clubedomecanico.vercel.app/pagamento/sucesso",
                    Failure = "https://clubedomecanico.vercel.app/pagamento/falha",
                    Pending = "https://clubedomecanico.vercel.app/pagamento/pendente"
                },
                AutoReturn = "approved",
                ExternalReference = pedido.Id.ToString(),
                NotificationUrl = "https://clube-mecanico-api.onrender.com/api/pagamento/webhook",
                StatementDescriptor = "CLUBE MECANICO"
            };

            // 11. SE O CLIENTE ESCOLHEU PIX, CRIAR URL ESPECÍFICA PARA PIX
            string paymentUrl;
            if (request.MetodoPagamento?.ToLower() == "pix")
            {
                // Primeiro, criar a preferência normalmente
                var client = new PreferenceClient();
                Preference preference = await client.CreateAsync(preferenceRequest);

                // Atualizar pedido
                pedido.MpPreferenceId = preference.Id;
                pagamento.CodigoTransacao = preference.Id;
                pagamento.MetodoPagamento = "pix";

                // Criar URL específica para PIX
                paymentUrl = $"{preference.InitPoint}&payment_method=pix";

                _logger.LogInformation($"PIX selecionado - URL específica criada: {paymentUrl}");
            }
            else
            {
                // Para cartão/boleto, usar normalmente
                var client = new PreferenceClient();
                Preference preference = await client.CreateAsync(preferenceRequest);

                pedido.MpPreferenceId = preference.Id;
                pagamento.CodigoTransacao = preference.Id;
                paymentUrl = preference.InitPoint;
            }

            pedido.LinkPagamento = paymentUrl;
            await _context.SaveChangesAsync();

            // 12. LOG
            _logger.LogInformation($"Pedido {pedido.Id} criado");
            _logger.LogInformation($"URL de pagamento: {paymentUrl}");

            // 13. RETORNAR PARA O FRONTEND
            return Ok(new
            {
                success = true,
                url = paymentUrl,
                pedidoId = pedido.Id,
                numeroPedido = pedido.NumeroPedido,
                metodoPagamento = request.MetodoPagamento
            });
        }
        catch (MercadoPago.Error.MercadoPagoApiException ex)
        {
            _logger.LogError(ex, $"Erro na API do Mercado Pago: {ex.Message}");

            // Erro específico do PIX não habilitado
            if (ex.Message.Contains("invalid default_payment_method_id"))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "PIX não está habilitado na sua conta Mercado Pago. Ative o PIX nas configurações da sua conta primeiro.",
                    errorCode = "PIX_NOT_ENABLED"
                });
            }

            return BadRequest(new
            {
                success = false,
                message = ex.Message
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

    [HttpGet("listar-todos")]
    public async Task<IActionResult> ListarTodosPagamentos(
    [FromQuery] DateTime? dataInicio = null,
    [FromQuery] DateTime? dataFim = null,
    [FromQuery] string? status = null,
    [FromQuery] string? metodoPagamento = null,
    [FromQuery] int pagina = 1,
    [FromQuery] int itensPorPagina = 20)
    {
        try
        {
            // Query base
            var query = _context.Pagamentos
                .Include(p => p.Pedido)
                    .ThenInclude(ped => ped.Aluno)
                .AsQueryable();

            // Aplicar filtros
            if (dataInicio.HasValue)
            {
                query = query.Where(p => p.DataPagamento >= dataInicio.Value);
            }

            if (dataFim.HasValue)
            {
                query = query.Where(p => p.DataPagamento <= dataFim.Value);
            }

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(p => p.Status == status);
            }

            if (!string.IsNullOrEmpty(metodoPagamento))
            {
                query = query.Where(p => p.MetodoPagamento == metodoPagamento);
            }

            // Total de registros (para paginação)
            var totalRegistros = await query.CountAsync();
            var totalPaginas = (int)Math.Ceiling(totalRegistros / (double)itensPorPagina);

            // Aplicar ordenação e paginação
            var pagamentos = await query
                .OrderByDescending(p => p.DataPagamento)
                .Skip((pagina - 1) * itensPorPagina)
                .Take(itensPorPagina)
                .Select(p => new
                {
                    p.Id,
                    p.PedidoId,
                    NumeroPedido = p.Pedido != null ? p.Pedido.NumeroPedido : "N/A",
                    Aluno = p.Pedido != null && p.Pedido.Aluno != null
                        ? new { p.Pedido.Aluno.Id, p.Pedido.Aluno.Nome_Completo, p.Pedido.Aluno.Email }
                        : null,
                    p.MetodoPagamento,
                    p.Valor,
                    p.Status,
                    StatusDetail = p.StatusDetail,
                    p.CodigoTransacao,
                    p.DataPagamento,
                    p.DataCriacao,
                    p.MpPaymentId,
                    p.TipoPagamento,
                    p.Parcelas,
                    p.Bandeira,
                    p.UltimosDigitos
                })
                .ToListAsync();

            // Estatísticas
            var estatisticas = new
            {
                TotalAprovados = await _context.Pagamentos.CountAsync(p => p.Status == "approved"),
                TotalPendentes = await _context.Pagamentos.CountAsync(p => p.Status == "pending"),
                TotalCancelados = await _context.Pagamentos.CountAsync(p =>
                    p.Status == "cancelled" || p.Status == "rejected"),
                ValorTotalAprovado = await _context.Pagamentos
                    .Where(p => p.Status == "approved")
                    .SumAsync(p => p.Valor)
            };

            return Ok(new
            {
                PaginaAtual = pagina,
                TotalPaginas = totalPaginas,
                TotalRegistros = totalRegistros,
                ItensPorPagina = itensPorPagina,
                Pagamentos = pagamentos,
                Estatisticas = estatisticas
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar pagamentos");
            return StatusCode(500, new { message = "Erro interno ao listar pagamentos" });
        }
    }

    [HttpGet("estatisticas")]
    [Authorize]
    public async Task<IActionResult> ObterEstatisticas([FromQuery] int? mes = null, [FromQuery] int? ano = null)
    {
        try
        {
            var dataAtual = DateTime.Now;
            var mesAtual = mes ?? dataAtual.Month;
            var anoAtual = ano ?? dataAtual.Year;

            var inicioMes = new DateTime(anoAtual, mesAtual, 1);
            var fimMes = inicioMes.AddMonths(1).AddDays(-1);

            // Pagamentos do mês
            var pagamentosMes = await _context.Pagamentos
                .Where(p => p.DataPagamento >= inicioMes && p.DataPagamento <= fimMes)
                .ToListAsync();

            // Pagamentos aprovados do mês
            var aprovadosMes = pagamentosMes
                .Where(p => p.Status == "approved")
                .ToList();

            var porMetodoPagamento = aprovadosMes
                .GroupBy(p => p.MetodoPagamento)
                .Select(g => new
                {
                    Metodo = g.Key ?? "Não informado",
                    Valor = g.Sum(p => p.Valor),
                    Quantidade = g.Count(),
                    Porcentagem = aprovadosMes.Count > 0 ?
                        (g.Count() * 100.0 / aprovadosMes.Count) : 0
                })
                .ToList();

            return Ok(new
            {
                Mes = mesAtual,
                Ano = anoAtual,
                TotalRecebido = aprovadosMes.Sum(p => p.Valor),
                TotalPagamentos = pagamentosMes.Count,
                Aprovados = aprovadosMes.Count,
                Pendentes = pagamentosMes.Count(p => p.Status == "pending"),
                Cancelados = pagamentosMes.Count(p =>
                    p.Status == "cancelled" || p.Status == "rejected"),
                MediaDiaria = aprovadosMes.Count > 0 ?
                    aprovadosMes.Sum(p => p.Valor) / DateTime.DaysInMonth(anoAtual, mesAtual) : 0,
                PorMetodoPagamento = porMetodoPagamento
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter estatísticas");
            return StatusCode(500, new { message = "Erro interno ao obter estatísticas" });
        }
    }
}

