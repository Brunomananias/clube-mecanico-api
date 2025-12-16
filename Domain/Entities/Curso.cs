using ClubeMecanico_API.Domain.Exceptions;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClubeMecanico_API.Domain.Entities
{
    public class Curso : BaseEntity
    {
        public int Id { get; set; }
        public string Codigo { get; set; }
        public string Nome { get;  set; }
        public string Descricao { get;  set; }
        public string? FotoUrl { get;  set; }
        public decimal Valor { get;  set; }
        public int DuracaoHoras { get;  set; }
        public string? Nivel { get;  set; }
        public int MaxAlunos { get;  set; }
        public string? ConteudoProgramatico { get;  set; }
        public bool CertificadoDisponivel { get;  set; }
        public bool Ativo { get;  set; }
        public DateTime DataCriacao { get;  set; }
        public int? AdminCriador { get;  set; }

        public virtual ICollection<Turma> Turmas { get;  set; }
        public virtual ICollection<ConteudoComplementar> ConteudosComplementares { get;  set; }
        public virtual ICollection<CursoAluno> CursosAlunos { get;  set; }

        public Curso() { }

        public Curso(string codigo, string nome, string descricao, decimal valor, int duracaoHoras, int maxAlunos, int? adminCriadorId = null)
        {
            Codigo = codigo;
            Nome = nome;
            Descricao = descricao;
            Valor = valor;
            DuracaoHoras = duracaoHoras;
            MaxAlunos = maxAlunos;
            CertificadoDisponivel = true;
            Ativo = true;
            DataCriacao = DateTime.UtcNow;

            Validar();
        }

        public void Atualizar(string nome, string descricao, decimal valor, int duracaoHoras, string? nivel, int maxAlunos)
        {
            Nome = nome;
            Descricao = descricao;
            Valor = valor;
            DuracaoHoras = duracaoHoras;
            Nivel = nivel;
            MaxAlunos = maxAlunos;
            Validar();
        }

      
        public void AtualizarConteudoProgramatico(string conteudoProgramatico)
        {
            ConteudoProgramatico = conteudoProgramatico;
        }

        public void AtualizarFoto(string fotoUrl)
        {
            FotoUrl = fotoUrl;
        }

        public void Ativar()
        {
            Ativo = true;
        }

        public void Desativar()
        {
            Ativo = false;
        }

        public void AtualizarCertificadoDisponivel(bool disponivel)
        {
            CertificadoDisponivel = disponivel;
        }

        private void Validar()
        {
            if (string.IsNullOrWhiteSpace(Codigo))
                throw new DomainException("Código do curso é obrigatório");

            if (string.IsNullOrWhiteSpace(Nome))
                throw new DomainException("Nome do curso é obrigatório");

            if (string.IsNullOrWhiteSpace(Descricao))
                throw new DomainException("Descrição do curso é obrigatória");

            if (Valor <= 0)
                throw new DomainException("Valor do curso deve ser maior que zero");

            if (DuracaoHoras <= 0)
                throw new DomainException("Duração do curso deve ser maior que zero");

            if (MaxAlunos <= 0)
                throw new DomainException("Número máximo de alunos deve ser maior que zero");
        }
    }
}
