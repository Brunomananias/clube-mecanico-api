using ClubeMecanico_API.Domain.Enums;
using ClubeMecanico_API.Domain.Exceptions;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClubeMecanico_API.Domain.Entities
{
    public class Usuario : BaseEntity
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public string SenhaHash { get; set; }
        public int Tipo { get; set; }
        public string Nome_Completo { get; set; }
        public string? CPF { get; set; }
        public string? Telefone { get; set; }
        public DateTime? Data_Nascimento { get; set; }
        public bool Ativo { get; set; }
        [Column(TypeName = "timestamp without time zone")]
        public DateTime Data_Cadastro { get; set; }

        [Column(TypeName = "timestamp without time zone")]
        public DateTime? UltimoLogin { get; set; }

        // Navegação
        public virtual ICollection<Pedido> Pedidos { get; set; }
        public virtual ICollection<CursoAluno> CursosAlunos { get; set; }
        public virtual ICollection<Certificado> Certificados { get; set; }

        private Usuario() { }

        public Usuario(string email, string senhaHash, string nomeCompleto)
        {
            Email = email;
            SenhaHash = senhaHash;
            
            Nome_Completo = nomeCompleto;
            Ativo = true;
            Data_Cadastro = DateTime.UtcNow;

            Validar();
        }

        public void AtualizarInformacoes(string nomeCompleto, string? telefone, DateTime? dataNascimento)
        {
            Nome_Completo = nomeCompleto;
            Telefone = telefone;
            Data_Nascimento = dataNascimento;
            Validar();
        }

        public void AtualizarSenha(string novaSenhaHash)
        {
            SenhaHash = novaSenhaHash;
        }

        public void RegistrarLogin()
        {
            UltimoLogin = DateTime.UtcNow;
        }

        public void Desativar()
        {
            Ativo = false;
        }

        public void Ativar()
        {
            Ativo = true;
        }

        private void Validar()
        {
            if (string.IsNullOrWhiteSpace(Email))
                throw new DomainException("Email é obrigatório");

            if (string.IsNullOrWhiteSpace(Nome_Completo))
                throw new DomainException("Nome completo é obrigatório");

            if (!Email.Contains("@"))
                throw new DomainException("Email inválido");
        }
    }
}
