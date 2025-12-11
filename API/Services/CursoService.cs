// Application/Services/CursoService.cs
using ClubeMecanico.Application.Interfaces;
using ClubeMecanico_API.API.DTOs;
using ClubeMecanico.Domain.Interfaces;
using ClubeMecanico_API.Domain.Entities;

namespace ClubeMecanico.Application.Services
{
    public class CursoService : ICursoService
    {
        private readonly ICursoRepository _cursoRepository;

        public CursoService(ICursoRepository cursoRepository)
        {
            _cursoRepository = cursoRepository;
        }

        public async Task<IEnumerable<Curso>> GetAllCursosAsync()
        {
            try
            {
                return await _cursoRepository.GetAllAsync();
            }
            catch (Exception ex)
            {
                // Log de erro (implementar depois)
                throw new ApplicationException("Erro ao buscar cursos", ex);
            }
        }

        public async Task<Curso?> GetCursoByIdAsync(int id)
        {
            try
            {
                if (id <= 0)
                    throw new ArgumentException("ID do curso inválido");

                return await _cursoRepository.GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                // Log de erro
                throw new ApplicationException($"Erro ao buscar curso com ID {id}", ex);
            }
        }

        public async Task<Curso> CriarCursoAsync(CriarCursoDTO cursoDto, int adminId)
        {
            try
            {
                // Validações básicas
                if (string.IsNullOrWhiteSpace(cursoDto.Codigo))
                    throw new ArgumentException("Código do curso é obrigatório");

                if (string.IsNullOrWhiteSpace(cursoDto.Nome))
                    throw new ArgumentException("Nome do curso é obrigatório");

                if (cursoDto.Valor < 0)
                    throw new ArgumentException("Valor do curso não pode ser negativo");

                if (cursoDto.DuracaoHoras <= 0)
                    throw new ArgumentException("Duração do curso deve ser maior que zero");

                if (cursoDto.MaxAlunos <= 0)
                    throw new ArgumentException("Número máximo de alunos deve ser maior que zero");

                // Verificar se código já existe
                if (await _cursoRepository.CodigoExistsAsync(cursoDto.Codigo))
                    throw new InvalidOperationException($"Já existe um curso com o código: {cursoDto.Codigo}");

                // Criar entidade
                var curso = new Curso
                {
                    Codigo = cursoDto.Codigo.Trim(),
                    Nome = cursoDto.Nome.Trim(),
                    Descricao = cursoDto.Descricao?.Trim(),
                    FotoUrl = cursoDto.FotoUrl?.Trim(),
                    Valor = cursoDto.Valor,
                    DuracaoHoras = cursoDto.DuracaoHoras,
                    Nivel = cursoDto.Nivel?.Trim(),
                    MaxAlunos = cursoDto.MaxAlunos,
                    ConteudoProgramatico = cursoDto.ConteudoProgramatico?.Trim(),
                    CertificadoDisponivel = cursoDto.CertificadoDisponivel,
                    Ativo = true,
                    DataCriacao = DateTime.UtcNow,
                    AdminCriador = adminId
                };

                // Inicializar collections para evitar null reference
                curso.Turmas = new List<Turma>();
                curso.ConteudosComplementares = new List<ConteudoComplementar>();
                curso.CursosAlunos = new List<CursoAluno>();
                curso.ItensPedido = new List<ItemPedido>();

                // Salvar no banco
                await _cursoRepository.AddAsync(curso);

                return curso;
            }
            catch (ArgumentException ex)
            {
                // Relançar validações de argumento
                throw;
            }
            catch (InvalidOperationException ex)
            {
                // Relançar operações inválidas
                throw;
            }
            catch (Exception ex)
            {
                // Log de erro
                throw new ApplicationException("Erro ao criar curso", ex);
            }
        }

        public async Task<IEnumerable<Turma>> GetTurmasByCursoIdAsync(int cursoId)
        {
            return await _cursoRepository.GetTurmasByCursoIdAsync(cursoId);
        }
    }
}