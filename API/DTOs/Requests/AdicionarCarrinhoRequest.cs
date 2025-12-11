using System.ComponentModel.DataAnnotations;

namespace ClubeMecanico_API.API.DTOs.Requests
{
    public class AdicionarCarrinhoRequest
    {
        [Required]
        public int CursoId { get; set; }
        public int UsuarioId { get; set; }
        public int? TurmaId { get; set; }
    }

    public class CarrinhoItemResponse
    {
        public int Id { get; set; }
        public int CursoId { get; set; }
        public string Titulo { get; set; }
        public string Descricao { get; set; }
        public decimal Valor { get; set; }
        public string Imagem { get; set; }
        public int VagasDisponiveis { get; set; }
        public DateTime DataAdicao { get; set; }
    }

    public class ApiResponse
    {
        public bool Success { get; set; }
        public string Mensagem { get; set; }
        public object Dados { get; set; }
    }
}
