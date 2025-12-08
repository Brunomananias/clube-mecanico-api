using ClubeMecanico_API.Domain.Exceptions;

namespace ClubeMecanico_API.Domain.Entities
{
    public class Pagamento : BaseEntity
    {
        public int Id { get; set; }
        public int PedidoId { get; private set; }
        public MetodoPagamento Metodo { get; private set; }
        public decimal Valor { get; private set; }
        public StatusPagamento Status { get; private set; }
        public string? CodigoTransacao { get; private set; }
        public DateTime? DataPagamento { get; private set; }
        public DateTime DataCriacao { get; private set; }

        // Navegação
        public virtual Pedido Pedido { get; private set; }

        private Pagamento() { }

        public Pagamento(int pedidoId, MetodoPagamento metodo, decimal valor)
        {
            PedidoId = pedidoId;
            Metodo = metodo;
            Valor = valor;
            Status = StatusPagamento.Pendente;
            DataCriacao = DateTime.UtcNow;

            Validar();
        }

        public void Aprovar(string codigoTransacao)
        {
            Status = StatusPagamento.Aprovado;
            CodigoTransacao = codigoTransacao;
            DataPagamento = DateTime.UtcNow;
        }

        public void Recusar()
        {
            Status = StatusPagamento.Recusado;
        }

        public void Estornar()
        {
            Status = StatusPagamento.Estornado;
        }

        private void Validar()
        {
            if (Valor <= 0)
                throw new DomainException("Valor do pagamento deve ser maior que zero");
        }
    }

    public enum MetodoPagamento
    {
        CartaoCredito,
        CartaoDebito,
        PIX,
        Boleto,
        Transferencia
    }

    public enum StatusPagamento
    {
        Pendente,
        Processando,
        Aprovado,
        Recusado,
        Cancelado,
        Estornado
    }
}
