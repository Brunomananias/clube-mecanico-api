using ClubeMecanico_API.Domain.Exceptions;

namespace ClubeMecanico_API.Domain.Entities
{
    public class Pedido : BaseEntity
    {
        public int Id { get; set; }
        public string NumeroPedido { get; private set; }
        public int AlunoId { get; private set; }
        public decimal ValorTotal { get; private set; }
        public StatusPedido Status { get; private set; }
        public DateTime DataPedido { get; private set; }

        // Navegação
        public virtual Usuario Aluno { get; private set; }
        public virtual ICollection<ItemPedido> Itens { get; private set; }
        public virtual Pagamento? Pagamento { get; private set; }

        private Pedido() { }

        public Pedido(int alunoId, string numeroPedido, decimal valorTotal)
        {
            AlunoId = alunoId;
            NumeroPedido = numeroPedido;
            ValorTotal = valorTotal;
            Status = StatusPedido.Pendente;
            DataPedido = DateTime.UtcNow;

            Validar();
        }

        public void AdicionarItem(ItemPedido item)
        {
            if (Itens == null)
                Itens = new List<ItemPedido>();

            Itens.Add(item);
            ValorTotal += item.Preco;
        }

        public void Aprovar()
        {
            Status = StatusPedido.Aprovado;
        }

        public void Cancelar()
        {
            Status = StatusPedido.Cancelado;

            // Liberar vagas nas turmas
            foreach (var item in Itens)
            {
                if (item.TurmaId.HasValue)
                {
                    // A liberação da vaga será feita pelo serviço que tem acesso ao repositório
                }
            }
        }

        public void Concluir()
        {
            Status = StatusPedido.Concluido;
        }

        private void Validar()
        {
            if (ValorTotal <= 0)
                throw new DomainException("Valor total do pedido deve ser maior que zero");

            if (string.IsNullOrWhiteSpace(NumeroPedido))
                throw new DomainException("Número do pedido é obrigatório");
        }
    }

    public enum StatusPedido
    {
        Pendente,
        Aprovado,
        Processando,
        Concluido,
        Cancelado,
        Reembolsado
    }
}
