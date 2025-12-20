using ClubeMecanico_API.Domain.Entities;

namespace ClubeMecanico_API.Domain.Interfaces
{
    public interface IUsuarioService
    {
        Task<Usuario> GetUsuarioComEnderecoAsync(int id);
        Task<List<Usuario>> GetAllUsuariosComEnderecosAsync();
        Task<Usuario> CreateUsuarioAsync(Usuario usuario);
        Task<Usuario> UpdateUsuarioAsync(int id, Usuario usuarioAtualizado);
        Task<bool> DeleteUsuarioAsync(int id);
        Task<Usuario> GetUsuarioByEmailAsync(string email);
        Task<Usuario> AddEnderecoToUsuarioAsync(int usuarioId, Endereco endereco);
        Task<Endereco> UpdateEnderecoAsync(int usuarioId, int enderecoId, Endereco enderecoAtualizado);
        Task<bool> RemoveEnderecoAsync(int usuarioId, int enderecoId);
    }
}
