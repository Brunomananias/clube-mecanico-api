using ClubeMecanico_API.Domain.Entities;

namespace ClubeMecanico_API.Domain.Interfaces
{
    public interface ITurmaRepository
    {
        Task<Turma?> GetByIdAsync(int id);
        Task<IEnumerable<Turma>> GetAllAsync();
        Task<IEnumerable<Turma>> GetByCursoIdAsync(int cursoId);
        Task<IEnumerable<Turma>> GetAtivasByCursoIdAsync(int cursoId);
        Task<IEnumerable<Turma>> GetTurmasAtivasAsync();
        Task AddAsync(Turma turma);
        Task UpdateAsync(Turma turma);
        Task DeleteAsync(Turma turma);
        Task AtualizarStatusTurma(Turma turma);
        Task<bool> ExistsAsync(int id);
        Task<bool> HasAlunosMatriculadosAsync(int turmaId);
        Task<List<Turma>> BuscarTurmas();
    }
}
