using ClubeMecanico_API.Domain.Exceptions;

namespace ClubeMecanico_API.Domain.Entities
{
    public class CarrinhoTemporario : BaseEntity
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public int CursoId { get; set; }
        public int? TurmaId { get; set; }
        public DateTime DataAdicao { get; set; }

        // Navegação
        public virtual Curso Curso { get; set; }
        public virtual Turma? Turma { get; set; }

        public CarrinhoTemporario() { }

        public CarrinhoTemporario(int usuarioId, int cursoId, int? turmaId = null)
        {
            UsuarioId = usuarioId;
            CursoId = cursoId;
            TurmaId = turmaId;
            DataAdicao = DateTime.UtcNow;

        }
    }
}
