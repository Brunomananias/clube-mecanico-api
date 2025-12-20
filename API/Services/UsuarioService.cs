using ClubeMecanico.Domain.Interfaces;
using ClubeMecanico_API.Domain.Entities;
using ClubeMecanico_API.Domain.Interfaces;

namespace ClubeMecanico_API.API.Services
{
    public class UsuarioService : IUsuarioService
    {
        private readonly IUsuarioRepository _usuarioRepository;

        public UsuarioService(IUsuarioRepository usuarioRepository)
        {
            _usuarioRepository = usuarioRepository;
        }

        public async Task<Usuario> GetUsuarioComEnderecoAsync(int id)
        {
            return await _usuarioRepository.GetByIdWithEnderecosAsync(id);
        }

        public async Task<List<Usuario>> GetAllUsuariosComEnderecosAsync()
        {
            return await _usuarioRepository.GetAllWithEnderecosAsync();
        }

        public async Task<Usuario> CreateUsuarioAsync(Usuario usuario)
        {
            // Validações básicas
            if (string.IsNullOrWhiteSpace(usuario.Email))
                throw new ArgumentException("Email é obrigatório");

            if (string.IsNullOrWhiteSpace(usuario.SenhaHash))
                throw new ArgumentException("Senha é obrigatória");

            // Hash da senha (implemente sua lógica de hash)
            // usuario.SenhaHash = HashPassword(usuario.SenhaHash);

            usuario.Data_Cadastro = DateTime.UtcNow;
            usuario.UltimoLogin = null;
            usuario.Ativo = true;

            return await _usuarioRepository.AddAsync(usuario);
        }

        public async Task<Usuario> UpdateUsuarioAsync(int id, Usuario usuarioAtualizado)
        {
            var usuarioExistente = await _usuarioRepository.GetByIdAsync(id);
            if (usuarioExistente == null)
                throw new KeyNotFoundException($"Usuário com ID {id} não encontrado");

            // Atualiza apenas os campos permitidos
            usuarioExistente.Nome_Completo = usuarioAtualizado.Nome_Completo ?? usuarioExistente.Nome_Completo;
            usuarioExistente.CPF = usuarioAtualizado.CPF ?? usuarioExistente.CPF;
            usuarioExistente.Telefone = usuarioAtualizado.Telefone ?? usuarioExistente.Telefone;
            usuarioExistente.Data_Nascimento = usuarioAtualizado.Data_Nascimento ?? usuarioExistente.Data_Nascimento;
            usuarioExistente.Ativo = usuarioAtualizado.Ativo;

            return await _usuarioRepository.UpdateAsync(usuarioExistente);
        }

        public async Task<bool> DeleteUsuarioAsync(int id)
        {
            var usuario = await _usuarioRepository.GetByIdAsync(id);
            if (usuario == null)
                throw new KeyNotFoundException($"Usuário com ID {id} não encontrado");

            // Soft delete (desativar)
            usuario.Ativo = false;
            await _usuarioRepository.UpdateAsync(usuario);

            return true;
        }

        public async Task<Usuario> GetUsuarioByEmailAsync(string email)
        {
            return await _usuarioRepository.GetByEmailAsync(email);
        }

        public async Task<Usuario> AddEnderecoToUsuarioAsync(int usuarioId, Endereco endereco)
        {
            var usuario = await _usuarioRepository.GetByIdWithEnderecosAsync(usuarioId);
            if (usuario == null)
                throw new KeyNotFoundException($"Usuário com ID {usuarioId} não encontrado");

            // Se for o primeiro endereço ou o tipo for "principal", 
            // desativa outros endereços principais
            if (endereco.Tipo == "principal")
            {
                foreach (var addr in usuario.Enderecos.Where(e => e.Tipo == "principal"))
                {
                    addr.Tipo = "secundario";
                }
            }

            endereco.UsuarioId = usuarioId;
            endereco.DataCadastro = DateTime.UtcNow;
            endereco.Ativo = true;

            return await _usuarioRepository.AddEnderecoAsync(usuarioId, endereco);
        }

        public async Task<Endereco> UpdateEnderecoAsync(int usuarioId, int enderecoId, Endereco enderecoAtualizado)
        {
            var usuario = await _usuarioRepository.GetByIdWithEnderecosAsync(usuarioId);
            if (usuario == null)
                throw new KeyNotFoundException($"Usuário com ID {usuarioId} não encontrado");

            var enderecoExistente = usuario.Enderecos.FirstOrDefault(e => e.Id == enderecoId);
            if (enderecoExistente == null)
                throw new KeyNotFoundException($"Endereço com ID {enderecoId} não encontrado");

            // Se estiver mudando para principal, desativa outros principais
            if (enderecoAtualizado.Tipo == "principal" && enderecoExistente.Tipo != "principal")
            {
                foreach (var addr in usuario.Enderecos.Where(e => e.Id != enderecoId && e.Tipo == "principal"))
                {
                    addr.Tipo = "secundario";
                }
            }

            // Atualiza campos
            enderecoExistente.CEP = enderecoAtualizado.CEP ?? enderecoExistente.CEP;
            enderecoExistente.Logradouro = enderecoAtualizado.Logradouro ?? enderecoExistente.Logradouro;
            enderecoExistente.Numero = enderecoAtualizado.Numero ?? enderecoExistente.Numero;
            enderecoExistente.Complemento = enderecoAtualizado.Complemento ?? enderecoExistente.Complemento;
            enderecoExistente.Bairro = enderecoAtualizado.Bairro ?? enderecoExistente.Bairro;
            enderecoExistente.Cidade = enderecoAtualizado.Cidade ?? enderecoExistente.Cidade;
            enderecoExistente.Estado = enderecoAtualizado.Estado ?? enderecoExistente.Estado;
            enderecoExistente.Tipo = enderecoAtualizado.Tipo ?? enderecoExistente.Tipo;
            enderecoExistente.Ativo = enderecoAtualizado.Ativo;
            enderecoExistente.DataAtualizacao = DateTime.UtcNow;

            return await _usuarioRepository.UpdateEnderecoAsync(enderecoExistente);
        }

        public async Task<bool> RemoveEnderecoAsync(int usuarioId, int enderecoId)
        {
            var usuario = await _usuarioRepository.GetByIdWithEnderecosAsync(usuarioId);
            if (usuario == null)
                throw new KeyNotFoundException($"Usuário com ID {usuarioId} não encontrado");

            var endereco = usuario.Enderecos.FirstOrDefault(e => e.Id == enderecoId);
            if (endereco == null)
                throw new KeyNotFoundException($"Endereço com ID {enderecoId} não encontrado");

            // Soft delete no endereço
            endereco.Ativo = false;
            endereco.DataAtualizacao = DateTime.UtcNow;

            await _usuarioRepository.UpdateEnderecoAsync(endereco);
            return true;
        }
    }
}
