using System.ComponentModel.DataAnnotations;

namespace ClubeMecanico_API.API.DTOs
{
    public class LoginDTO
    {
        public string Email { get; set; }
        public string Senha { get; set; }
    }

    public class RegistrarUsuarioDTO
    {
        public string Email { get; set; }
        public string Senha { get; set; }
        public string Nome_Completo { get; set; }
        public string? CPF { get; set; }
        public string? Telefone { get; set; }
        public DateTime? Data_Nascimento { get; set; }
        public int Tipo { get; set; }
        public EnderecoDTO? Endereco { get; set; }
    }

    public class EnderecoDTO
    {
        public string? CEP { get; set; }
        public string? Logradouro { get; set; }
        public string? Numero { get; set; }
        public string? Complemento { get; set; }
        public string? Bairro { get; set; }
        public string? Cidade { get; set; }
        public string? Estado { get; set; }
        public string Tipo { get; set; } = "principal";
    }

    public class CriarCursoDTO
    {
        [Required(ErrorMessage = "Código é obrigatório")]
        [StringLength(20, MinimumLength = 3, ErrorMessage = "Código deve ter entre 3 e 20 caracteres")]
        public string Codigo { get; set; }

        [Required(ErrorMessage = "Nome é obrigatório")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Nome deve ter entre 3 e 100 caracteres")]
        public string Nome { get; set; }

        [StringLength(500, ErrorMessage = "Descrição não pode exceder 500 caracteres")]
        public string? Descricao { get; set; }

        [StringLength(2000, ErrorMessage = "Descrição detalhada não pode exceder 2000 caracteres")]
        public string? DescricaoDetalhada { get; set; }

        [Url(ErrorMessage = "URL da foto inválida")]
        public string? FotoUrl { get; set; }

        [Range(0, 999999.99, ErrorMessage = "Valor deve ser entre 0 e 999999.99")]
        public decimal Valor { get; set; }

        [Range(1, 1000, ErrorMessage = "Duração deve ser entre 1 e 1000 horas")]
        public int DuracaoHoras { get; set; }

        [StringLength(50, ErrorMessage = "Nível não pode exceder 50 caracteres")]
        public string? Nivel { get; set; }

        [Range(1, 1000, ErrorMessage = "Máximo de alunos deve ser entre 1 e 1000")]
        public int MaxAlunos { get; set; }

        [StringLength(5000, ErrorMessage = "Conteúdo programático não pode exceder 5000 caracteres")]
        public string? ConteudoProgramatico { get; set; }

        public bool CertificadoDisponivel { get; set; } = true;
    }

    public class AtualizarCursoDTO
    {
        public string Codigo { get; set; }
        public string Nome { get; set; }
        public string Descricao { get; set; }
        public decimal Valor { get; set; }
        public int DuracaoHoras { get; set; }
        public string? Nivel { get; set; }
        public int MaxAlunos { get; set; }
    }

    public class CriarPedidoDTO
    {
        public List<ItemPedidoDTO> Itens { get; set; }
    }

    public class ItemPedidoDTO
    {
        public int CursoId { get; set; }
        public int? TurmaId { get; set; }
    }

    public class ProcessarPagamentoDTO
    {
        public string MetodoPagamento { get; set; }
        public string? CodigoTransacao { get; set; }
    }
}
