using ClubeMecanico_API.Models;

public class Pagamento
{
    public int Id { get; set; }
    public int PedidoId { get; set; }
    public string MetodoPagamento { get; set; }
    public decimal Valor { get; set; }
    public string Status { get; set; } = "pendente";
    public string? CodigoTransacao { get; set; }
    public DateTime? DataPagamento { get; set; }
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public string? MpPaymentId { get; set; }
    public string? StatusDetail { get; set; }
    public string? TipoPagamento { get; set; }
    public int Parcelas { get; set; } = 1;
    public string? Bandeira { get; set; }
    public string? UltimosDigitos { get; set; }
    public string? PixQrCode { get; set; }
    public string? PixCopiaCola { get; set; }
    public string? BoletoUrl { get; set; }
    public string? BoletoLinhaDigitavel { get; set; }
    public DateTime? DataExpiracaoBoleto { get; set; }
    public bool NotificacaoRecebida { get; set; } = false;

    // Navegações
    public Pedido Pedido { get; set; }
}