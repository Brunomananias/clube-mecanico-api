using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ClubeMecanico_API.Domain.Entities;
using ClubeMecanico_API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using QRCoder;
using ClubeMecanico_API.Models;
using System.Security.Claims;
using System.Globalization;
using System.Text;

namespace ClubeMecanico_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]    
    public class PixController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PixController> _logger;

        public PixController(AppDbContext context, IConfiguration configuration, ILogger<PixController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("criar-pedido")]
        public async Task<IActionResult> CriarPedido([FromBody] CriarPedidoRequest request)
        {
            try
            {
                var aluno = await _context.Usuarios
                    .FirstOrDefaultAsync(u => u.Id == request.usuarioId);

                if (aluno == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Aluno não encontrado no sistema"
                    });
                }

                // 3. BUSCAR ITENS DO CARRINHO DO ALUNO
                var carrinhoItens = await _context.CarrinhoTemporario
                    .Include(ct => ct.Curso)
                    .Where(c => c.UsuarioId == request.usuarioId)
                    .ToListAsync();

                if (!carrinhoItens.Any())
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Seu carrinho está vazio. Adicione cursos antes de finalizar."
                    });
                }

                _logger.LogInformation("Encontrados {Quantidade} itens no carrinho", carrinhoItens.Count);

                // 4. CALCULAR VALORES
                var subtotal = carrinhoItens.Sum(c => c.Curso?.Valor ?? 0);

                // Aplicar desconto do cupom se for válido
                var descontoCupom = 0m;
                if (!string.IsNullOrEmpty(request.Cupom) && request.Cupom.ToUpper() == "BEMVINDO10")
                {
                    descontoCupom = subtotal * 0.1m;
                    _logger.LogInformation("Cupom BEMVINDO10 aplicado: R$ {Desconto}", descontoCupom);
                }

                var total = subtotal - descontoCupom;

                // 5. GERAR NÚMERO DO PEDIDO ÚNICO
                var numeroPedido = $"PED-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}";
                _logger.LogInformation("Gerando pedido: {NumeroPedido}", numeroPedido);

                // 6. CRIAR PEDIDO NO BANCO
                var pedido = new Pedido
                {
                    NumeroPedido = numeroPedido,
                    AlunoId = request.usuarioId,
                    Subtotal = subtotal,
                    Desconto = descontoCupom,
                    ValorTotal = total,
                    Status = "pendente",
                    DataPedido = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                };

                _context.Pedidos.Add(pedido);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Pedido {PedidoId} criado com ID", pedido.Id);

                // 7. CRIAR ITENS DO PEDIDO
                foreach (var item in carrinhoItens)
                {
                    var itemPedido = new ItemPedido
                    {
                        PedidoId = pedido.Id,
                        CursoId = item.CursoId,
                        TurmaId = item.TurmaId,
                        Preco = item.Curso?.Valor ?? 0,
                        Quantidade = 1,
                        NomeCurso = item.Curso?.Nome ?? "Curso",
                        Duracao = item.Curso?.DuracaoHoras.ToString(),
                        DataCompra = DateTime.Now
                    };

                    _context.ItensPedido.Add(itemPedido);
                    _logger.LogDebug("Item pedido criado: Curso {CursoId}", item.CursoId);
                }

                // 8. CRIAR REGISTRO DE PAGAMENTO
                var pagamento = new Pagamento
                {
                    PedidoId = pedido.Id,
                    Status = "pendente",
                    Valor = total,
                    DataCriacao = DateTime.Now,
                    MetodoPagamento = "pix"
                };

                _context.Pagamentos.Add(pagamento);

                // 9. SALVAR TUDO
                await _context.SaveChangesAsync();

                // 10. LIMPAR CARRINHO
                _context.CarrinhoTemporario.RemoveRange(carrinhoItens);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Pedido {PedidoId} finalizado com sucesso. Total: R$ {Total}", pedido.Id, total);

                // 11. RETORNAR SUCESSO
                return Ok(new
                {
                    success = true,
                    pedidoId = pedido.Id,
                    numeroPedido = pedido.NumeroPedido,
                    total = pedido.ValorTotal.ToString("F2"),
                    subtotal = pedido.Subtotal.ToString("F2"),
                    desconto = pedido.Desconto.ToString("F2"),
                    status = pedido.Status,
                    dataPedido = pedido.DataPedido.ToString("yyyy-MM-dd HH:mm:ss"),
                    aluno = new
                    {
                        id = aluno.Id,
                        nome = aluno.Nome_Completo,
                        email = aluno.Email
                    },
                    message = "Pedido criado com sucesso! Agora você pode fazer o pagamento via PIX."
                });
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Erro de banco de dados ao criar pedido");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erro ao salvar pedido no banco de dados.",
                    detalhes = dbEx.InnerException?.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao criar pedido");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erro interno ao criar pedido. Tente novamente.",
                    detalhes = ex.Message
                });
            }
        }

        [HttpPost("gerar-cobranca")]
        public async Task<IActionResult> GerarCobrancaPix([FromBody] GerarCobrancaPixRequest request)
        {
            try
            {
                _logger.LogInformation("Gerando cobrança PIX para pedido {PedidoId}, aluno {AlunoId}",
                    request.PedidoId, request.AlunoId);

                // 1. Validar pedido
                var pedido = await _context.Pedidos
                    .Include(p => p.ItensPedido)
                    .FirstOrDefaultAsync(p => p.Id == request.PedidoId && p.AlunoId == request.AlunoId);

                if (pedido == null)
                {
                    _logger.LogWarning("Pedido {PedidoId} não encontrado para aluno {AlunoId}",
                        request.PedidoId, request.AlunoId);
                    return NotFound(new
                    {
                        success = false,
                        message = "Pedido não encontrado ou não pertence a este aluno"
                    });
                }

                if (pedido.Status == "pago" || pedido.Status == "aprovado")
                {
                    _logger.LogWarning("Pedido {PedidoId} já está pago", pedido.Id);
                    return BadRequest(new
                    {
                        success = false,
                        message = "Este pedido já foi pago"
                    });
                }

                // 2. Obter chave PIX da configuração
                var chavePix = _configuration["Pix:ChavePix"] ?? "14594495680"; // Default para teste

                // Validar chave PIX
                if (string.IsNullOrWhiteSpace(chavePix))
                {
                    _logger.LogError("Chave PIX não configurada");
                    return BadRequest(new
                    {
                        success = false,
                        message = "Configuração do PIX não encontrada"
                    });
                }

                _logger.LogInformation("Usando chave PIX: {ChavePix}", chavePix);

                // 3. Gerar TxId CURTO (máximo 25 caracteres)
                var txid = GerarTxIdCurto();
                _logger.LogInformation("TxId gerado: {TxId}", txid);

                // 4. Gerar código PIX COPIA E COLA CORRETO
                var qrCodeText = GerarPixCopiaECola(chavePix, pedido.ValorTotal, txid, "Ana", "Vespasiano");

                // 5. Verificar se o código PIX é válido
                if (string.IsNullOrWhiteSpace(qrCodeText) || !qrCodeText.StartsWith("000201"))
                {
                    _logger.LogError("Falha ao gerar código PIX: Texto inválido");
                    return BadRequest(new
                    {
                        success = false,
                        message = "Falha ao gerar código PIX"
                    });
                }

                _logger.LogInformation("QR Code PIX gerado com {Caracteres} caracteres", qrCodeText.Length);

                // 6. Gerar QR Code base64
                var qrCodeBase64 = await GerarQrCodeBase64CorrigidoAsync(qrCodeText);

                // 7. Verificar se já existe pagamento pendente
                var pagamentoExistente = await _context.Pagamentos
                    .FirstOrDefaultAsync(p => p.PedidoId == pedido.Id && p.Status == "pendente");

                if (pagamentoExistente != null && pagamentoExistente.DataExpiracaoBoleto > DateTime.Now)
                {
                    _logger.LogInformation("Reutilizando PIX pendente para pedido {PedidoId}", pedido.Id);

                    // Retornar pagamento existente
                    return Ok(new
                    {
                        success = true,
                        txid = pagamentoExistente.CodigoTransacao,
                        qrcode = qrCodeText,
                        imagemQrcode = qrCodeBase64,
                        valor = pagamentoExistente.Valor.ToString("F2"),
                        pedidoId = pedido.Id,
                        numeroPedido = pedido.NumeroPedido,                    
                        mensagem = "Use este PIX para pagamento",
                        chavePix = chavePix,
                        reutilizado = true
                    });
                }

                // 8. Criar novo pagamento
                var pagamento = new Pagamento
                {
                    PedidoId = pedido.Id,
                    MetodoPagamento = "pix",
                    Status = "pendente",
                    CodigoTransacao = txid,
                    DataCriacao = DateTime.Now,
                    DataExpiracaoBoleto = DateTime.Now.AddHours(1),
                    Valor = pedido.ValorTotal,                  
                };

                // Atualizar status do pedido
                pedido.Status = "pendente_pix";
                pedido.UpdatedAt = DateTime.Now;

                if (pagamentoExistente != null)
                {
                    // Atualizar pagamento existente expirado
                    pagamentoExistente.Status = "expirado";
                    _context.Pagamentos.Update(pagamentoExistente);
                }

                _context.Pagamentos.Add(pagamento);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Pagamento PIX criado para pedido {PedidoId} com TxId {TxId}",
                    pedido.Id, txid);

                // 9. Retornar resposta
                return Ok(new
                {
                    success = true,
                    txid = txid,
                    qrcode = qrCodeText,
                    imagemQrcode = qrCodeBase64,
                    valor = pedido.ValorTotal.ToString("F2"),
                    pedidoId = pedido.Id,
                    numeroPedido = pedido.NumeroPedido,                
                    mensagem = "PIX copia e cola gerado com sucesso. Válido por 1 hora.",
                    chavePix = chavePix,
                    reutilizado = false,
                    dadosPix = new
                    {
                        chave = chavePix,
                        valor = pedido.ValorTotal.ToString("F2"),
                        beneficiario = "ClubeMecanico",
                        cidade = "SaoPaulo"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar cobrança PIX para pedido {PedidoId}", request.PedidoId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erro interno ao gerar cobrança PIX",
                    detalhes = ex.Message
                });
            }
        }

        [HttpGet("status/{pedidoId}")]
        public async Task<IActionResult> VerificarStatusPix(int pedidoId)
        {
            try
            {
                var pagamento = await _context.Pagamentos
                    .Include(p => p.Pedido)
                    .FirstOrDefaultAsync(p => p.PedidoId == pedidoId && p.MetodoPagamento == "pix");

                if (pagamento == null)
                    return NotFound(new { success = false, message = "Pagamento não encontrado" });

                // Verificar se expirou
                if (pagamento.DataExpiracaoBoleto < DateTime.Now && pagamento.Status == "pendente")
                {
                    pagamento.Status = "expirado";
                    if (pagamento.Pedido != null)
                    {
                        pagamento.Pedido.Status = "expirado";
                    }
                    await _context.SaveChangesAsync();
                }

                return Ok(new
                {
                    success = true,
                    status = pagamento.Status,
                    pedidoId = pagamento.PedidoId,
                    pedidoStatus = pagamento.Pedido?.Status,
                    expirado = pagamento.DataExpiracaoBoleto < DateTime.Now,
                    pago = pagamento.Status == "aprovado",
                    valor = pagamento.Valor.ToString("F2"),
                    txid = pagamento.CodigoTransacao,
                    dataCriacao = pagamento.DataCriacao.ToString("yyyy-MM-ddTHH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar status PIX para pedido {PedidoId}", pedidoId);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> WebhookPix([FromBody] JsonElement notification)
        {
            try
            {
                _logger.LogInformation("Webhook PIX recebido: {Notification}", notification.ToString());

                string txid = "";
                string status = "";

                // Extrair valores do JSON
                if (notification.TryGetProperty("pix", out var pixElement))
                {
                    if (pixElement.TryGetProperty("txid", out var txidElement))
                    {
                        txid = txidElement.GetString() ?? "";
                    }
                }

                if (notification.TryGetProperty("status", out var statusElement))
                {
                    status = statusElement.GetString()?.ToUpper() ?? "";
                }

                // Alternativa: buscar txid direto
                if (string.IsNullOrEmpty(txid) && notification.TryGetProperty("txid", out var txidDirect))
                {
                    txid = txidDirect.GetString() ?? "";
                }

                _logger.LogInformation("Processando webhook: TxId={TxId}, Status={Status}", txid, status);

                if (!string.IsNullOrEmpty(txid))
                {
                    var pagamento = await _context.Pagamentos
                        .Include(p => p.Pedido)
                        .FirstOrDefaultAsync(p => p.CodigoTransacao == txid);

                    if (pagamento != null)
                    {
                        _logger.LogInformation("Pagamento encontrado: {PagamentoId}, Status atual: {StatusAtual}",
                            pagamento.Id, pagamento.Status);

                        // Atualizar status baseado na notificação
                        string novoStatus = pagamento.Status;
                        string pedidoStatus = pagamento.Pedido?.Status ?? "";

                        if (status == "CONCLUIDA" || status == "APROVADO" || status == "APPROVED")
                        {
                            novoStatus = "aprovado";
                            pedidoStatus = "aprovado";
                            pagamento.DataPagamento = DateTime.Now;

                            // Matricular aluno nos cursos
                            if (pagamento.Pedido != null)
                            {
                                await MatricularAluno(pagamento.Pedido.Id, pagamento.Pedido.AlunoId);
                            }
                        }
                        else if (status == "PENDENTE" || status == "PENDING")
                        {
                            novoStatus = "pendente";
                            pedidoStatus = "pendente_pix";
                        }
                        else if (status == "CANCELADA" || status == "REJEITADA" || status == "CANCELED" || status == "REJECTED")
                        {
                            novoStatus = "cancelado";
                            pedidoStatus = "cancelado";
                        }

                        // Atualizar apenas se mudou
                        if (pagamento.Status != novoStatus)
                        {
                            pagamento.Status = novoStatus;                            

                            if (pagamento.Pedido != null)
                            {
                                pagamento.Pedido.Status = pedidoStatus;
                                pagamento.Pedido.UpdatedAt = DateTime.Now;
                            }

                            await _context.SaveChangesAsync();
                            _logger.LogInformation("Pagamento {TxId} atualizado para: {NovoStatus}", txid, novoStatus);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Pagamento não encontrado para TxId: {TxId}", txid);
                    }
                }
                else
                {
                    _logger.LogWarning("Webhook recebido sem TxId válido");
                }

                return Ok(new { success = true, message = "Webhook processado" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no webhook PIX");
                return StatusCode(500, new { success = false, message = "Erro interno" });
            }
        }

        // ============= MÉTODOS AUXILIARES =============

        private string GerarTxIdCurto()
        {
            // TxId deve ter NO MÁXIMO 25 caracteres
            // Formato: PIX + timestamp + 3 dígitos randômicos
            return $"PIX{DateTime.Now:yyyyMMddHHmmss}{new Random().Next(100, 999)}";
        }

        private string GerarPixCopiaECola(
            string chavePix,
            decimal valor,
            string txId,
            string nomeRecebedor,
            string cidade)
        {
            nomeRecebedor = nomeRecebedor.Length > 25 ? nomeRecebedor[..25] : nomeRecebedor;
            cidade = cidade.Length > 15 ? cidade[..15] : cidade;

            string valorFormatado = valor.ToString("F2", CultureInfo.InvariantCulture);

            string payload =
                "000201" +
                "010212" +
                MontarCampo("26",
                    MontarCampo("00", "BR.GOV.BCB.PIX") +
                    MontarCampo("01", chavePix) +
                    MontarCampo("02", "Pagamento")) +
                "52040000" +
                "5303986" +
                MontarCampo("54", valorFormatado) +
                "5802BR" +
                MontarCampo("59", nomeRecebedor) +
                MontarCampo("60", cidade) +
                MontarCampo("62",
                    MontarCampo("05", txId)) +
                "6304";

            return payload + CalcularCRC16(payload);
        }

        private string MontarCampo(string id, string valor)
        {
            return $"{id}{valor.Length:D2}{valor}";
        }

        private string CalcularCRC16(string payload)
        {
            ushort crc = 0xFFFF;
            foreach (byte b in Encoding.ASCII.GetBytes(payload))
            {
                crc ^= (ushort)(b << 8);
                for (int i = 0; i < 8; i++)
                {
                    crc = (crc & 0x8000) != 0
                        ? (ushort)((crc << 1) ^ 0x1021)
                        : (ushort)(crc << 1);
                }
            }
            return crc.ToString("X4");
        }

        private string CalculateCRC16CCITTCorrigido(string data)
        {
            ushort crc = 0xFFFF;
            byte[] bytes = Encoding.UTF8.GetBytes(data);

            foreach (byte b in bytes)
            {
                crc ^= (ushort)(b << 8);

                for (int i = 0; i < 8; i++)
                {
                    bool xor = (crc & 0x8000) != 0;
                    crc = (ushort)(crc << 1);
                    if (xor) crc ^= 0x1021;
                }
            }

            crc ^= 0xFFFF;
            return crc.ToString("X4").ToUpper();
        }

        private async Task<string> GerarQrCodeBase64CorrigidoAsync(string qrCodeText)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(qrCodeText))
                {
                    return "";
                }

                // Certifique-se de ter o pacote QRCoder instalado
                using var qrGenerator = new QRCodeGenerator();
                using var qrCodeData = qrGenerator.CreateQrCode(qrCodeText, QRCodeGenerator.ECCLevel.Q);
                using var qrCode = new PngByteQRCode(qrCodeData);

                byte[] qrCodeBytes = qrCode.GetGraphic(20);
                string base64 = Convert.ToBase64String(qrCodeBytes);

                return $"data:image/png;base64,{base64}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar QR Code");
                // QR Code de fallback (pixel transparente)
                return "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=";
            }
        }

        private async Task MatricularAluno(int pedidoId, int alunoId)
        {
            try
            {
                _logger.LogInformation("Matriculando aluno {AlunoId} para pedido {PedidoId}", alunoId, pedidoId);

                // Buscar itens do pedido
                var itensPedido = await _context.ItensPedido
                    .Where(ip => ip.PedidoId == pedidoId)
                    .ToListAsync();

                foreach (var item in itensPedido)
                {
                    // Verificar se já existe matrícula
                    var matriculaExistente = await _context.CursosAlunos
                        .FirstOrDefaultAsync(ca =>
                            ca.AlunoId == alunoId &&
                            ca.CursoId == item.CursoId &&
                            ca.TurmaId == item.TurmaId);

                    if (matriculaExistente == null)
                    {
                        var novaMatricula = new CursoAluno
                        {
                            AlunoId = alunoId,
                            CursoId = item.CursoId,
                            TurmaId = item.TurmaId,
                            Status = "ativo",
                            Progresso = 0,
                            DataMatricula = DateTime.Now,
                        };

                        _context.CursosAlunos.Add(novaMatricula);
                        _logger.LogInformation("Aluno {AlunoId} matriculado no curso {CursoId}", alunoId, item.CursoId);
                    }
                    else
                    {
                        _logger.LogInformation("Aluno {AlunoId} já matriculado no curso {CursoId}", alunoId, item.CursoId);
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Matrícula concluída para pedido {PedidoId}", pedidoId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao matricular aluno {AlunoId} para pedido {PedidoId}", alunoId, pedidoId);
                throw;
            }
        }

        [HttpGet("teste-pix/{valor}")]
        [AllowAnonymous]
        public IActionResult TestePix(decimal valor)
        {
            try
            {
                var chavePix = _configuration["Pix:ChavePix"] ?? "14594495680";
                var txid = GerarTxIdCurto();
                var pixCode = GerarPixCopiaECola(chavePix, valor, txid, "Ana", "Vespasiano");

                return Ok(new
                {
                    success = true,
                    pixCode = pixCode,
                    txid = txid,
                    valor = valor.ToString("F2"),
                    chavePix = chavePix,
                    tamanho = pixCode.Length
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }
    }

    public class GerarCobrancaPixRequest
    {
        public int PedidoId { get; set; }
        public int AlunoId { get; set; }
        public string? Cpf { get; set; }
        public string? Nome { get; set; }
    }

    public class CriarPedidoRequest
    {
        public int usuarioId { get; set; }
        public string? Cupom { get; set; }
    }
}