namespace ClubeMecanico_API.API.DTOs.Requests
{
    public class PagamentoRequest
    {
        public decimal Total { get; set; }
        public string MetodoPagamento { get; set; } // "pix", "credit_card", "boleto"
    }
}
