using ClubeMecanico_API.Domain.Exceptions;

namespace ClubeMecanico_API.Domain.Entities
{
    public class CarrinhoTemporario : BaseEntity
    {
        public string SessaoId { get; private set; }
        public int CursoId { get; private set; }
        public int? TurmaId { get; private set; }
        public DateTime DataAdicao { get; private set; }

        // Navegação
        public virtual Curso Curso { get; private set; }
        public virtual Turma? Turma { get; private set; }

        private CarrinhoTemporario() { }

        public CarrinhoTemporario(string sessaoId, int cursoId, int? turmaId = null)
        {
            SessaoId = sessaoId;
            CursoId = cursoId;
            TurmaId = turmaId;
            DataAdicao = DateTime.UtcNow;

            Validar();
        }

        private void Validar()
        {
            if (string.IsNullOrWhiteSpace(SessaoId))
                throw new DomainException("ID da sessão é obrigatório");
        }
    }
}
