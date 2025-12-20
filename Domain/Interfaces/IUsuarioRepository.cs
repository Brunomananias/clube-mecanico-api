using ClubeMecanico_API.Domain.Entities;

namespace ClubeMecanico_API.Domain.Interfaces
{
    public interface IUsuarioRepository
    {
        Task<Usuario> GetByIdAsync(int id);
        Task<Usuario> GetByIdWithEnderecosAsync(int id);
        Task<List<Usuario>> GetAllAsync();
        Task<List<Usuario>> GetAllWithEnderecosAsync();
        Task<Usuario> GetByEmailAsync(string email);
        Task<Usuario> AddAsync(Usuario usuario);
        Task<Usuario> UpdateAsync(Usuario usuario);
        Task<Usuario> AddEnderecoAsync(int usuarioId, Endereco endereco);
        Task<Endereco> UpdateEnderecoAsync(Endereco endereco);
        Task<bool> EmailExistsAsync(string email);
    }
}
