namespace ClubeMecanico_API.Domain.Entities
{
    public class Endereco
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public string? CEP { get; set; }
        public string? Logradouro { get; set; }
        public string? Numero { get; set; }
        public string? Complemento { get; set; }
        public string? Bairro { get; set; }
        public string? Cidade { get; set; }
        public string? Estado { get; set; }
        public string Tipo { get; set; } = "principal";
        public bool Ativo { get; set; } = true;
        public DateTime DataCadastro { get; set; } = DateTime.UtcNow;
        public DateTime? DataAtualizacao { get; set; }

        // Navegação
        public virtual Usuario Usuario { get; set; }
    }
}
