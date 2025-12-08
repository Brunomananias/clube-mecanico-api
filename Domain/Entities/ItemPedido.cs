using ClubeMecanico_API.Domain.Exceptions;

namespace ClubeMecanico_API.Domain.Entities
{
    public class ItemPedido : BaseEntity
    {
        public int Id { get; set; }
        public int PedidoId { get; private set; }
        public int CursoId { get; private set; }
        public int? TurmaId { get; private set; }
        public decimal Preco { get; private set; }
        public DateTime DataCompra { get; private set; }

        // Navegação
        public virtual Pedido Pedido { get; private set; }
        public virtual Curso Curso { get; private set; }
        public virtual Turma? Turma { get; private set; }

        private ItemPedido() { }

        public ItemPedido(int pedidoId, int cursoId, decimal preco, int? turmaId = null)
        {
            PedidoId = pedidoId;
            CursoId = cursoId;
            Preco = preco;
            TurmaId = turmaId;
            DataCompra = DateTime.UtcNow;

            Validar();
        }

        private void Validar()
        {
            if (Preco <= 0)
                throw new DomainException("Preço do item deve ser maior que zero");
        }
    }
}
