// Models/Pedido.cs
using ClubeMecanico_API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClubeMecanico_API.Models
{
    public class Pedido
    {
        public int Id { get; set; }
        public string NumeroPedido { get; set; }
        public int AlunoId { get; set; }
        public decimal ValorTotal { get; set; }
        public string Status { get; set; } = "pendente";
        public DateTime DataPedido { get; set; } = DateTime.Now;
        public string? CupomCodigo { get; set; }
        public decimal Desconto { get; set; }
        public decimal Subtotal { get; set; }
        public string? MpPreferenceId { get; set; }
        public string? MpPaymentId { get; set; }
        public string? LinkPagamento { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navegações
        public Usuario Aluno { get; set; }
        public ICollection<ItemPedido> ItensPedido { get; set; } = new List<ItemPedido>();
        public Pagamento Pagamento { get; set; }
    }
}