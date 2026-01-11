using ClubeMecanico_API.API.DTOs;
using ClubeMecanico_API.API.DTOs.Requests;
using ClubeMecanico_API.Domain.Entities;
using ClubeMecanico_API.Domain.Enums;

namespace ClubeMecanico.Application.Interfaces
{
    public interface IAuthService
    {
        // Métodos síncronos (para compatibilidade)
        Usuario? AuthenticateUser(string email, string password);
        string GenerateJwtToken(long userId, string email);
        string HashPassword(string password);

        // Métodos assíncronos
        Task<Usuario?> AutenticarAsync(string email, string senha);
        Task<Usuario?> RegistrarAsync(
         string email,
         string senha,
         string nomeCompleto,
         string? cpf,
         string? telefone,
         DateTime? dataNascimento,
         int tipo,
         string? cep = null,
         string? logradouro = null,
         string? numero = null,
         string? complemento = null,
         string? bairro = null,
         string? cidade = null,
         string? estado = null,
         string tipoEndereco = "principal");
    }

    public interface ICursoService
    {
        Task<IEnumerable<Curso>> GetAllCursosAsync();
        Task<Curso?> GetCursoByIdAsync(int id);
        Task<Curso> CriarCursoAsync(CriarCursoDTO cursoDto);
        Task<Curso> AtualizarCursoAsync(int id, AtualizarCursoDTO cursoDto);
        Task<IEnumerable<Turma>> GetTurmasByCursoIdAsync(int cursoId);
        Task<CursoAluno> MatricularAlunoAsync(MatricularAlunoCursoDTO matriculaDto, int usuarioId);
        Task<IEnumerable<CursoAluno>> BuscarCursosAlunos(int idAluno);
        Task<bool> DeletarCurso(int id);
    }

    public class ResultadoPagamento
    {
        public bool Sucesso { get; set; }
        public string Mensagem { get; set; }
    }

    public class AuthResponseDTO
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiraEm { get; set; }
        public UsuarioResponseDTO Usuario { get; set; } = null!;
    }

    public class UsuarioResponseDTO
    {
        public int Id { get; set; }
        public string NomeCompleto { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? CPF { get; set; }
        public int TipoUsuario { get; set; }
        public bool Ativo { get; set; }
    }

}