using ClubeMecanico.Domain.Interfaces;
using ClubeMecanico_API.API.DTOs.Requests;
using ClubeMecanico_API.Domain.Entities;

namespace ClubeMecanico.Domain.Interfaces
{
    public interface IRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(int id);
        Task<IEnumerable<T>> GetAllAsync();
        Task AddAsync(T entity);
        Task AdicionarMatricula(CursoAluno cursoAluno);
        Task UpdateAsync(T entity);
        Task DeleteAsync(T entity);
    }
}

namespace ClubeMecanico.Domain.Interfaces
{
    public interface ICursoRepository : IRepository<Curso>
    {
        Task<bool> CodigoExistsAsync(string codigo);
        Task<IEnumerable<Curso>> GetCursosDestaqueAsync();
        Task<IEnumerable<Turma>> GetTurmasByCursoIdAsync(int cursoId);
        Task<IEnumerable<CursoAluno>> GetCursosComTurmasPorAluno(int idAluno);
        Task<bool> DeletarCurso(int id);
        Task<bool> AdicionarCertificado(AdicionarCertificadoRequest certificado);
        Task<IEnumerable<Certificado>> BuscarCertificado(int cursoAlunoId);
    }
}

