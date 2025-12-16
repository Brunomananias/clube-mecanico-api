// Models/ItemPedido.cs
using ClubeMecanico_API.Domain.Entities;
using ClubeMecanico_API.Models;

public class ItemPedido
{
    public int Id { get; set; }
    public int PedidoId { get; set; }
    public int CursoId { get; set; }
    public int? TurmaId { get; set; }
    public decimal Preco { get; set; }
    public DateTime DataCompra { get; set; } = DateTime.UtcNow;
    public int Quantidade { get; set; } = 1;
    public string? NomeCurso { get; set; }
    public int? Duracao { get; set; }

    // Navegações
    public Pedido Pedido { get; set; }
    public Curso Curso { get; set; }
    public Turma? Turma { get; set; }
}

// Models/CriarPedidoRequest.cs
public class CriarPedidoRequest
{
    public List<ItemPedido> Itens { get; set; }
    public string? Cupom { get; set; }
}

// Models/PagamentoRequest.cs
public class PagamentoRequest
{
    public int PedidoId { get; set; }
    public int UserId { get; set; }
    public string MetodoPagamento { get; set; } // "credit_card", "pix", "boleto"
    public string? EmailCliente { get; set; }
    public decimal ValorCurso { get; set; }

}

// Models/PedidoResponse.cs
public class PedidoResponse
{
    public bool Success { get; set; }
    public string? Mensagem { get; set; }
    public PedidoData? Dados { get; set; }
}

public class PedidoData
{
    public int PedidoId { get; set; }
    public decimal Total { get; set; }
    public DateTime DataCriacao { get; set; }
}