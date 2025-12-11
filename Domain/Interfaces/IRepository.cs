using ClubeMecanico_API.Domain.Entities;

namespace ClubeMecanico.Domain.Interfaces
{
    public interface IRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(int id);
        Task<IEnumerable<T>> GetAllAsync();
        Task AddAsync(T entity);
        Task UpdateAsync(T entity);
        Task DeleteAsync(T entity);
    }
}

namespace ClubeMecanico.Domain.Interfaces
{
    public interface IUsuarioRepository : IRepository<Usuario>
    {
        Task<Usuario?> GetByEmailAsync(string email);
        Task<bool> EmailExistsAsync(string email);
        Task<IEnumerable<Usuario>> GetAlunosAsync();
        Task<IEnumerable<Usuario>> GetAdministradoresAsync();
    }

    public interface ICursoRepository : IRepository<Curso>
    {
        Task<bool> CodigoExistsAsync(string codigo);
        Task<IEnumerable<Curso>> GetCursosDestaqueAsync();
        Task<IEnumerable<Turma>> GetTurmasByCursoIdAsync(int cursoId);
    }

    public interface IPedidoRepository : IRepository<Pedido>
    {
        Task<Pedido?> GetByNumeroAsync(string numeroPedido);
        Task<IEnumerable<Pedido>> GetByAlunoIdAsync(int alunoId);
        Task<string> GerarNumeroPedidoAsync();
    }
}