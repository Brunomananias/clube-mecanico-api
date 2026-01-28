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
using System.Net.Mail;
using System.Net;

[ApiController]
[Route("api/[controller]")]
public class PagamentoController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _context;
    private readonly ILogger<PagamentoController> _logger;
    private readonly IServiceProvider _serviceProvider;
    // Injeta o IConfiguration no construtor
    public PagamentoController(
       IConfiguration configuration,
       AppDbContext context,
       ILogger<PagamentoController> logger,
       IServiceProvider serviceProvider)
    {
        _configuration = configuration;
        _context = context;
        _logger = logger;
        _serviceProvider = serviceProvider;
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
                    await MatricularAlunoNosCursos(pedido.Id, pedido.AlunoId);
                    await EnviarEmailPagamentoAprovado(pedido, pagamento);
                    break;

                case "pending":
                    pedido.Status = "pendente";
                    break;

                case "in_process":
                case "in_mediation":
                    pedido.Status = "em_processamento";
                    break;

                case "cancelled":
                case "rejected":
                    pedido.Status = "cancelado";
                    break;

                case "refunded":
                case "charged_back":
                    pedido.Status = "reembolsado";
                    break;

                default:
                    pedido.Status = "desconhecido";
                    break;
            }

            pedido.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return Ok();
        }
        catch (Exception ex)
        {
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
                    // **ATUALIZAR VAGAS DISPONÍVEIS DA TURMA**
                    if (item.Turma != null && item.TurmaId.HasValue)
                    {
                        // Buscar a turma para atualizar
                        var turma = await _context.Turmas.FindAsync(item.TurmaId.Value);
                        if (turma != null && turma.VagasDisponiveis > 0)
                        {
                            turma.VagasDisponiveis -= 1;
                            _context.Turmas.Update(turma);
                            _logger.LogInformation($"Vaga subtraída da turma {item.TurmaId}. Vagas disponíveis: {turma.VagasDisponiveis}");
                        }
                        else if (turma != null && turma.VagasDisponiveis <= 0)
                        {
                            _logger.LogWarning($"Turma {item.TurmaId} não tem vagas disponíveis");
                            // Aqui você pode lançar uma exceção ou tratar de outra forma
                        }
                    }

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

                        // **ATUALIZAR VAGAS DISPONÍVEIS DA TURMA SE A MATRÍCULA ESTAVA INATIVA**
                        if (item.Turma != null && item.TurmaId.HasValue)
                        {
                            var turma = await _context.Turmas.FindAsync(item.TurmaId.Value);
                            if (turma != null && turma.VagasDisponiveis > 0)
                            {
                                turma.VagasDisponiveis -= 1;
                                _context.Turmas.Update(turma);
                                _logger.LogInformation($"Vaga subtraída da turma {item.TurmaId} ao reativar matrícula. Vagas disponíveis: {turma.VagasDisponiveis}");
                            }
                        }

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
                    Itens = p.Pedido.ItensPedido.Select(i => new
                    {
                        i.Id,
                        i.CursoId,
                        i.NomeCurso,
                        i.Preco,
                        i.TurmaId,
                        i.Turma.DataInicio,
                        i.Turma.DataFim,
                        Horario = i.Turma != null ? i.Turma.Horario : null,
                        StatusTurma = i.Turma != null ? i.Turma.Status : null
                    }).ToList(),
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

    private async Task EnviarEmailPagamentoAprovado(Pedido pedido, Pagamento pagamento)
    {
        try
        {
            // 🔥 PEGAR CONFIGURAÇÕES DO AMBIENTE (Render) ou APPSETTINGS
            var emailConfig = ObterConfiguracaoEmail();

            if (string.IsNullOrEmpty(emailConfig.SenderEmail) || string.IsNullOrEmpty(emailConfig.SenderPassword))
            {
                _logger.LogWarning("⚠️ Configurações de email não encontradas");
                return;
            }

            var subject = $"💰 PAGAMENTO APROVADO - Pedido #{pedido.NumeroPedido}";

            // Buscar itens do pedido para detalhar
            var itensPedido = await _context.ItensPedido
                .Where(ip => ip.PedidoId == pedido.Id)
                .Include(ip => ip.Curso)
                .ToListAsync();

            var cursosLista = string.Join("<br>", itensPedido.Select(ip =>
                $"- {ip.NomeCurso} (R$ {ip.Preco:F2})"));

            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #28a745; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ background-color: #f8f9fa; padding: 20px; border: 1px solid #dee2e6; }}
        .details {{ background-color: white; border: 1px solid #ddd; padding: 15px; margin: 15px 0; border-radius: 5px; }}
        .footer {{ text-align: center; padding: 20px; color: #6c757d; font-size: 12px; border-top: 1px solid #dee2e6; }}
        .value {{ color: #28a745; font-weight: bold; font-size: 18px; }}
        .badge {{ display: inline-block; padding: 5px 10px; border-radius: 20px; font-size: 12px; font-weight: bold; }}
        .badge-approved {{ background-color: #d4edda; color: #155724; }}
        .curso-item {{ padding: 8px 0; border-bottom: 1px solid #eee; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>💰 PAGAMENTO APROVADO</h1>
            <h3>Clube do Mecânico</h3>
        </div>
        
        <div class='content'>
            <h3>📋 DETALHES DO PEDIDO</h3>
            
            <div class='details'>
                <p><strong>Número do Pedido:</strong> {pedido.NumeroPedido}</p>
                <p><strong>ID do Pedido:</strong> {pedido.Id}</p>
                <p><strong>ID do Aluno:</strong> {pedido.AlunoId}</p>
                <p><strong>Data do Pedido:</strong> {pedido.DataPedido:dd/MM/yyyy HH:mm}</p>
                <p><strong>Data de Aprovação:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</p>
                
                <p><strong>Status:</strong> 
                    <span class='badge badge-approved'>APROVADO ✅</span>
                </p>
                
                <p><strong>Valor Total:</strong> 
                    <span class='value'>R$ {pedido.Subtotal:F2}</span>
                </p>
                
                <p><strong>Método de Pagamento:</strong> {pagamento.MetodoPagamento?.ToUpper()}</p>
                
                {(string.IsNullOrEmpty(pagamento.Bandeira) ? "" :
                    $"<p><strong>Bandeira/Cartão:</strong> {pagamento.Bandeira}</p>")}
                
                {(string.IsNullOrEmpty(pagamento.UltimosDigitos) ? "" :
                    $"<p><strong>Últimos 4 dígitos:</strong> **** {pagamento.UltimosDigitos}</p>")}
                
                {(pagamento.Parcelas > 0 ?
                    $"<p><strong>Parcelas:</strong> {pagamento.Parcelas}x</p>" : "")}
                
                <p><strong>ID Mercado Pago:</strong> {pagamento.MpPaymentId}</p>
            </div>
            
            <h3>📚 CURSOS COMPRADOS:</h3>
            <div class='details'>
                {cursosLista}
            </div>
            
            <h3>📊 RESUMO:</h3>
            <div class='details'>
                <p>• Valor Total: R$ {pedido.Subtotal:F2}</p>
                <p>• Quantidade de Cursos: {itensPedido.Count}</p>
                <p>• Status: Aprovado ✅</p>
                <p>• Aluno já foi matriculado automaticamente</p>
            </div>
        </div>
        
        <div class='footer'>
            <p><strong>Clube do Mecânico</strong> &copy; {DateTime.Now.Year}</p>
            <p>📍 Sistema de Notificações Automáticas</p>
            <p>⏰ {DateTime.Now:dd/MM/yyyy HH:mm:ss}</p>
        </div>
    </div>
</body>
</html>";

            // 🔥 ENVIAR EMAIL COM CONFIGURAÇÃO OBTIDA
            await EnviarEmail(emailConfig, subject, body);

            _logger.LogInformation($"✅ Email enviado para {emailConfig.AdminEmail}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao enviar email de pagamento aprovado");
            // Não lançar para não afetar o fluxo principal
        }
    }

    // 🔥 MÉTODO AUXILIAR PARA OBTER CONFIGURAÇÃO DE EMAIL
    private EmailConfig ObterConfiguracaoEmail()
    {
        // PRIMEIRO: Tenta pegar do serviço injetado (se configurou no Program)
        try
        {
            var emailService = _serviceProvider.GetService<EmailConfig>();
            if (emailService != null &&
                !string.IsNullOrEmpty(emailService.SenderEmail) &&
                !string.IsNullOrEmpty(emailService.SenderPassword))
            {
                _logger.LogInformation("✅ Usando configuração de email do serviço injetado");
                return emailService;
            }
        }
        catch
        {
            // Se falhar, continua para outras formas
        }

        // SEGUNDO: Tenta pegar do ambiente (Render) com dois underlines
        var config = new EmailConfig();

        config.SmtpServer = Environment.GetEnvironmentVariable("EmailSettings__SmtpServer")
                           ?? "smtp.gmail.com";

        if (int.TryParse(Environment.GetEnvironmentVariable("EmailSettings__SmtpPort"), out var port))
        {
            config.SmtpPort = port;
        }
        else
        {
            config.SmtpPort = 587;
        }

        config.SenderEmail = Environment.GetEnvironmentVariable("EmailSettings__SenderEmail");
        config.SenderPassword = Environment.GetEnvironmentVariable("EmailSettings__SenderPassword");
        config.AdminEmail = Environment.GetEnvironmentVariable("EmailSettings__AdminEmail")
                           ?? "clubemecanico2026@gmail.com";

        // TERCEIRO: Se ainda não encontrou, tenta do appsettings.json
        if (string.IsNullOrEmpty(config.SenderEmail) || string.IsNullOrEmpty(config.SenderPassword))
        {
            config.SmtpServer = _configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
            config.SmtpPort = _configuration.GetValue<int>("EmailSettings:SmtpPort", 587);
            config.SenderEmail = _configuration["EmailSettings:SenderEmail"];
            config.SenderPassword = _configuration["EmailSettings:SenderPassword"];
            config.AdminEmail = _configuration["EmailSettings:AdminEmail"] ?? "clubemecanico2026@gmail.com";
        }

        // Log para debug
        _logger.LogDebug($"Email Config - Server: {config.SmtpServer}:{config.SmtpPort}");
        _logger.LogDebug($"Email Config - From: {config.SenderEmail}");
        _logger.LogDebug($"Email Config - To: {config.AdminEmail}");
        _logger.LogDebug($"Email Config - Password configurada: {!string.IsNullOrEmpty(config.SenderPassword)}");

        return config;
    }

    // 🔥 MÉTODO AUXILIAR PARA ENVIAR EMAIL
    private async Task EnviarEmail(EmailConfig config, string subject, string body)
    {
        using var smtpClient = new SmtpClient(config.SmtpServer, config.SmtpPort)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(config.SenderEmail, config.SenderPassword),
            Timeout = 10000 // 10 segundos
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress(config.SenderEmail, "Sistema Clube do Mecânico"),
            Subject = subject,
            Body = body,
            IsBodyHtml = true,
        };

        mailMessage.To.Add(config.AdminEmail);

        await smtpClient.SendMailAsync(mailMessage);
    }

    // 🔥 CLASSE PARA ARMAZENAR CONFIGURAÇÕES
    public class EmailConfig
    {
        public string SmtpServer { get; set; } = "smtp.gmail.com";
        public int SmtpPort { get; set; } = 587;
        public string SenderEmail { get; set; } = "";
        public string SenderPassword { get; set; } = "";
        public string AdminEmail { get; set; } = "clubemecanico2026@gmail.com";
    }

    [HttpGet("debug-email")]
    [AllowAnonymous]
    public IActionResult DebugEmail()
    {
        var resultado = new
        {
            // Do Environment (Render)
            envServer = Environment.GetEnvironmentVariable("EmailSettings__SmtpServer"),
            envEmail = Environment.GetEnvironmentVariable("EmailSettings__SenderEmail"),
            envPassword = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("EmailSettings__SenderPassword")),

            // Do appsettings.json
            configServer = _configuration["EmailSettings:SmtpServer"],
            configEmail = _configuration["EmailSettings:SenderEmail"],
            configPassword = !string.IsNullOrEmpty(_configuration["EmailSettings:SenderPassword"]),

            // Do ServiceProvider (se configurado)
            temServiceProvider = _serviceProvider != null
        };

        return Ok(resultado);
    }
}

