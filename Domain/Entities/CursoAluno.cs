using ClubeMecanico_API.Domain.Exceptions;

namespace ClubeMecanico_API.Domain.Entities
{
    public class CursoAluno : BaseEntity
    {
        public int Id { get; set; }
        public int AlunoId { get; private set; }
        public int CursoId { get; private set; }
        public int? TurmaId { get; private set; }
        public string Status { get; private set; }
        public int Progresso { get; private set; }
        public DateTime DataMatricula { get; private set; }
        public DateTime? DataConclusao { get; private set; }

        // Navegação
        public virtual Usuario Aluno { get; private set; }
        public virtual Curso Curso { get; private set; }
        public virtual Turma? Turma { get; private set; }
        public virtual ICollection<Certificado> Certificados { get; private set; }

        private CursoAluno() { }

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
