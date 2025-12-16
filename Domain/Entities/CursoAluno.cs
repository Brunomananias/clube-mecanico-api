using ClubeMecanico_API.Domain.Exceptions;

namespace ClubeMecanico_API.Domain.Entities
{
    public class CursoAluno : BaseEntity
    {
        public int Id { get; set; }
        public int AlunoId { get; set; }
        public int CursoId { get; set; }
        public int? TurmaId { get; set; }
        public string Status { get; set; }
        public int Progresso { get; set; }
        public DateTime DataMatricula { get; set; }
        public DateTime? DataConclusao { get; set; }

        // Navegação
        public virtual Usuario Aluno { get; set; }
        public virtual Curso Curso { get; set; }
        public virtual Turma? Turma { get; set; }
        public virtual ICollection<Certificado> Certificados { get;  set; }

        public CursoAluno() { }

        public CursoAluno(int alunoId, int cursoId, int? turmaId = null)
        {
            AlunoId = alunoId;
            CursoId = cursoId;
            TurmaId = turmaId;
            Status = "ATIVO";
            Progresso = 0;
            DataMatricula = DateTime.UtcNow;
        }

        public void AtualizarProgresso(int progresso)
        {
            if (progresso < 0 || progresso > 100)
                throw new DomainException("Progresso deve estar entre 0 e 100");

            Progresso = progresso;

            if (progresso == 100)
                Concluir();
        }

        public void Concluir()
        {
            Status ="CONCLUIDO";
            DataConclusao = DateTime.UtcNow;
            Progresso = 100;
        }

        public void Cancelar()
        {
            Status ="CANCELADO";
        }
    }

    public enum StatusMatricula
    {
        Ativo,
        Concluido,
        Cancelado,
        Trancado
    }
}
