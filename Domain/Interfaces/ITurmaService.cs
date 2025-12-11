using ClubeMecanico_API.API.DTOs.Requests;
using ClubeMecanico_API.Domain.Entities;

namespace ClubeMecanico_API.Domain.Interfaces
{
    public interface ITurmaService
    {
        Task<Turma> CriarTurmaAsync(CriarTurmaRequest turmaDto);
        Task<IEnumerable<Turma>> GetTurmasByCursoIdAsync(int cursoId);
        Task<Turma?> GetTurmaByIdAsync(int id);
        Task<bool> VerificarVagasDisponiveisAsync(int turmaId);
        Task<Turma> AtualizarTurmaAsync(int id, AtualizarTurmaDTO turmaDto);
        Task<bool> DeletarTurmaAsync(int id);
    }
}
