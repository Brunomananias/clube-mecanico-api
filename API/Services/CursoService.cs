// Application/Services/CursoService.cs
using ClubeMecanico.Application.Interfaces;
using ClubeMecanico_API.API.DTOs;
using ClubeMecanico.Domain.Interfaces;
using ClubeMecanico_API.Domain.Entities;
using ClubeMecanico_API.API.DTOs.Requests;
using ClubeMecanico_API.Domain.Interfaces;

namespace ClubeMecanico.Application.Services
{
    public class CursoService : ICursoService
    {
        private readonly ICursoRepository _cursoRepository;
        private readonly ITurmaRepository _turmaRepository;
        private readonly IUsuarioRepository _usuarioRepository;

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

        public async Task<Curso> CriarCursoAsync(CriarCursoDTO cursoDto)
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
                    DescricaoDetalhada = cursoDto.DescricaoDetalhada?.Trim(),
                    FotoUrl = cursoDto.FotoUrl?.Trim(),
                    Valor = cursoDto.Valor,
                    DuracaoHoras = cursoDto.DuracaoHoras,
                    Nivel = cursoDto.Nivel?.Trim(),
                    MaxAlunos = cursoDto.MaxAlunos,
                    ConteudoProgramatico = cursoDto.ConteudoProgramatico?.Trim(),
                    CertificadoDisponivel = cursoDto.CertificadoDisponivel,
                    Ativo = true,
                    DataCriacao = DateTime.UtcNow,
                    AdminCriador = cursoDto.IdUsuario
                };

                // Inicializar collections para evitar null reference
                curso.Turmas = new List<Turma>();
                curso.ConteudosComplementares = new List<ConteudoComplementar>();
                curso.CursosAlunos = new List<CursoAluno>();

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

        public async Task<IEnumerable<Certificado>> BuscarCertificado(int cursoAlunoId)
        {
            return await _cursoRepository.BuscarCertificado(cursoAlunoId);
        }


        public async Task<bool> AdicionarCertificado(AdicionarCertificadoRequest certificado)
        {
            return await _cursoRepository.AdicionarCertificado(certificado);
        }

        public async Task<CursoAluno> MatricularAlunoAsync(MatricularAlunoCursoDTO matriculaDto, int usuarioId)
        {
            try
            {
                // Validar dados
                if (matriculaDto == null)
                    throw new ArgumentException("Dados de matrícula inválidos");

                if (matriculaDto.CursoId <= 0)
                    throw new ArgumentException("Curso inválido");

                if (matriculaDto.TurmaId <= 0)
                    throw new ArgumentException("Turma inválida");

                if (matriculaDto.AlunoId <= 0)
                    throw new ArgumentException("Aluno inválido");

                // Verificar se o curso existe
                var curso = await _cursoRepository.GetByIdAsync(matriculaDto.CursoId);
                if (curso == null)
                    throw new InvalidOperationException($"Curso com ID {matriculaDto.CursoId} não encontrado");

                var cursoAluno = new CursoAluno
                {
                    AlunoId = matriculaDto.AlunoId,
                    CursoId = matriculaDto.CursoId,
                    TurmaId = matriculaDto.TurmaId,
                    Status = matriculaDto.Status ?? "Ativo",
                    DataMatricula = DateTime.UtcNow,
                };

                //// Salvar no banco de dados
                await _cursoRepository.AdicionarMatricula(cursoAluno);
                //await _unitOfWork.CommitAsync();

                // Retornar matrícula criada
                return cursoAluno;
            }
            catch (Exception ex)
            {
                // Log do erro
                Console.WriteLine($"Erro ao matricular aluno: {ex.Message}");
                throw;
            }
        }

        public async Task<IEnumerable<CursoAluno>> BuscarCursosAlunos(int idAluno)
        {
            var cursos = await _cursoRepository.GetCursosComTurmasPorAluno(idAluno);
            return cursos;
        }

        // Application/Services/CursoService.cs - Adicione este método
        public async Task<Curso> AtualizarCursoAsync(int id, AtualizarCursoDTO cursoDto)
        {
            try
            {
                // 1. Validações básicas
                if (id <= 0)
                    throw new ArgumentException("ID do curso inválido");

                if (string.IsNullOrWhiteSpace(cursoDto.Nome))
                    throw new ArgumentException("Nome do curso é obrigatório");

                if (cursoDto.Valor < 0)
                    throw new ArgumentException("Valor do curso não pode ser negativo");

                if (cursoDto.DuracaoHoras <= 0)
                    throw new ArgumentException("Duração do curso deve ser maior que zero");

                if (cursoDto.MaxAlunos <= 0)
                    throw new ArgumentException("Número máximo de alunos deve ser maior que zero");

                // 2. Buscar curso existente
                var cursoExistente = await _cursoRepository.GetByIdAsync(id);
                if (cursoExistente == null)
                    throw new KeyNotFoundException($"Curso com ID {id} não encontrado");

                // 3. Atualizar propriedades do curso
                cursoExistente.Atualizar(
                    cursoDto.Codigo,
                    cursoDto.Nome.Trim(),
                    cursoDto.Descricao?.Trim(),
                    cursoDto.DescricaoDetalhada?.Trim(),
                    cursoDto.Valor,
                    cursoDto.DuracaoHoras,
                    cursoDto.Nivel?.Trim(),
                    cursoDto.MaxAlunos,
                    cursoDto.FotoUrl
                );

                // 4. Atualizar propriedades adicionais
                if (!string.IsNullOrEmpty(cursoDto.ConteudoProgramatico))
                {
                    cursoExistente.AtualizarConteudoProgramatico(cursoDto.ConteudoProgramatico.Trim());
                }

                if (!string.IsNullOrEmpty(cursoDto.FotoUrl))
                {
                    cursoExistente.AtualizarFoto(cursoDto.FotoUrl.Trim());
                }

                cursoExistente.AtualizarCertificadoDisponivel(cursoDto.CertificadoDisponivel);

                // 5. Chamar repositório para salvar
                await _cursoRepository.UpdateAsync(cursoExistente);

                return cursoExistente;
            }
            catch (KeyNotFoundException)
            {
                throw; // Relançar exceção de não encontrado
            }
            catch (ArgumentException ex)
            {
                throw; // Relançar validações de argumento
            }
            catch (Exception ex)
            {
                // Log de erro
                throw new ApplicationException($"Erro ao atualizar curso com ID {id}", ex);
            }
        }

        public async Task<bool> DeletarCurso(int id)
        {
            try
            {
                var curso = await GetCursoByIdAsync(id);
                var deleted = await _cursoRepository.DeletarCurso(id);
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao deletar curso");
            }
        }
    }
}