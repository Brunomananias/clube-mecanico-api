// Application/Services/TurmaService.cs
using ClubeMecanico.Application.Interfaces;
using ClubeMecanico.Domain.Interfaces;
using ClubeMecanico_API.Domain.Entities;
using ClubeMecanico_API.Domain.Interfaces;
using ClubeMecanico_API.API.DTOs.Requests;

namespace ClubeMecanico.Application.Services
{
    public class TurmaService : ITurmaService
    {
        private readonly ITurmaRepository _turmaRepository;
        private readonly ICursoRepository _cursoRepository;

        public TurmaService(ITurmaRepository turmaRepository, ICursoRepository cursoRepository)
        {
            _turmaRepository = turmaRepository;
            _cursoRepository = cursoRepository;
        }

        public async Task<Turma> CriarTurmaAsync(CriarTurmaRequest turmaDto)
        {
            try
            {
                // Validações
                if (turmaDto.VagasTotal <= 0)
                    throw new ArgumentException("Total de vagas deve ser maior que zero");

                if (turmaDto.VagasDisponiveis > turmaDto.VagasTotal)
                    throw new ArgumentException("Vagas disponíveis não podem ser maiores que o total");

                // Verificar se curso existe
                var curso = await _cursoRepository.GetByIdAsync(turmaDto.CursoId);
                if (curso == null)
                    throw new ArgumentException($"Curso com ID {turmaDto.CursoId} não encontrado");

                var dataInicioUtc = turmaDto.DataInicio;
                var dataFimUtc = turmaDto.DataFim;

                // Se as datas não forem UTC, converta
                if (dataInicioUtc.Kind != DateTimeKind.Utc)
                {
                    dataInicioUtc = DateTime.SpecifyKind(dataInicioUtc, DateTimeKind.Utc);
                }

                if (dataFimUtc.Kind != DateTimeKind.Utc)
                {
                    dataFimUtc = DateTime.SpecifyKind(dataFimUtc, DateTimeKind.Utc);
                }

                // Criar turma
                var turma = new Turma
                {
                    CursoId = turmaDto.CursoId,
                    DataInicio = dataInicioUtc,
                    DataFim = dataFimUtc,
                    Horario = turmaDto.Horario.Trim(),
                    Professor = turmaDto.Professor.Trim(),
                    VagasTotal = turmaDto.VagasTotal,
                    VagasDisponiveis = turmaDto.VagasDisponiveis,
                    Status = turmaDto.Status?.ToUpper() ?? "ABERTO"
                };

                await _turmaRepository.AddAsync(turma);
                return turma;
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Erro ao criar turma: {ex.Message}", ex);
            }
        }

        public async Task<IEnumerable<Turma>> GetTurmasByCursoIdAsync(int cursoId)
        {
            return await _turmaRepository.GetByCursoIdAsync(cursoId);
        }

        public async Task<Turma?> GetTurmaByIdAsync(int id)
        {
            return await _turmaRepository.GetByIdAsync(id);
        }

        public async Task<List<Turma>> BuscarTurmas()
        {
            return await _turmaRepository.BuscarTurmas();
        }

        public async Task<bool> VerificarVagasDisponiveisAsync(int turmaId)
        {
            var turma = await _turmaRepository.GetByIdAsync(turmaId);
            return turma != null && turma.VagasDisponiveis > 0 && turma.Status == "ABERTO";
        }

        // No TurmaService.cs - complete o método AtualizarTurmaAsync
        public async Task<Turma> AtualizarTurmaAsync(int id, AtualizarTurmaDTO turmaDto)
        {
            try
            {
                var turma = await _turmaRepository.GetByIdAsync(id);
                if (turma == null)
                    throw new KeyNotFoundException($"Turma com ID {id} não encontrada");

                if (turmaDto.DataInicio.HasValue)
                {
                    if (turmaDto.DataFim.HasValue && turmaDto.DataInicio.Value >= turmaDto.DataFim.Value)
                        throw new ArgumentException("Data de início deve ser anterior à data de fim");

                    turma.DataInicio = turmaDto.DataInicio.Value;
                }

                if (turmaDto.DataFim.HasValue)
                {
                    if (turmaDto.DataInicio.HasValue && turmaDto.DataInicio.Value >= turmaDto.DataFim.Value)
                        throw new ArgumentException("Data de fim deve ser posterior à data de início");

                    turma.DataFim = turmaDto.DataFim.Value;
                }

                if (!string.IsNullOrWhiteSpace(turmaDto.Horario))
                    turma.Horario = turmaDto.Horario.Trim();

                if (!string.IsNullOrWhiteSpace(turmaDto.Professor))
                    turma.Professor = turmaDto.Professor.Trim();

                if (turmaDto.VagasTotal.HasValue)
                {
                    if (turmaDto.VagasTotal.Value <= 0)
                        throw new ArgumentException("Total de vagas deve ser maior que zero");

                    if (turmaDto.VagasDisponiveis.HasValue &&
                        turmaDto.VagasDisponiveis.Value > turmaDto.VagasTotal.Value)
                        throw new ArgumentException("Vagas disponíveis não podem ser maiores que o total");

                    turma.VagasTotal = turmaDto.VagasTotal.Value;
                }

                if (turmaDto.VagasDisponiveis.HasValue)
                {
                    // Validação para evitar que vagas disponíveis sejam maiores que o total
                    int vagasTotalAtual = turmaDto.VagasTotal.HasValue ?
                        turmaDto.VagasTotal.Value : turma.VagasTotal;

                    if (turmaDto.VagasDisponiveis.Value > vagasTotalAtual)
                        throw new ArgumentException($"Vagas disponíveis não podem ser maiores que o total ({vagasTotalAtual})");

                    // Se não forçar, não permite reduzir vagas disponíveis abaixo do que já está ocupado
                    int vagasOcupadas = turma.VagasTotal - turma.VagasDisponiveis;
                    int novoMinimoVagas = vagasTotalAtual - vagasOcupadas;

                    if (!turmaDto.ForcarAtualizacaoVagas.HasValue || !turmaDto.ForcarAtualizacaoVagas.Value)
                    {
                        if (turmaDto.VagasDisponiveis.Value < novoMinimoVagas)
                            throw new ArgumentException($"Não é possível reduzir para menos de {novoMinimoVagas} vagas disponíveis. Já existem {vagasOcupadas} matrículas.");
                    }

                    turma.VagasDisponiveis = turmaDto.VagasDisponiveis.Value;
                }

                if (!string.IsNullOrWhiteSpace(turmaDto.Status))
                {
                    var statusValido = new[] { "ABERTO", "FECHADO", "CANCELADA", "CONCLUÍDA" };
                    var statusUpper = turmaDto.Status.ToUpper();

                    if (!statusValido.Contains(statusUpper))
                        throw new ArgumentException($"Status inválido. Use: {string.Join(", ", statusValido)}");

                    turma.Status = statusUpper;
                }

                await _turmaRepository.UpdateAsync(turma);
                return turma;
            }
            catch (KeyNotFoundException ex)
            {
                throw;
            }
            catch (ArgumentException ex)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Erro ao atualizar turma: {ex.Message}", ex);
            }
        }

        public async Task<bool> DeletarTurmaAsync(int id)
        {
            try
            {
                var turma = await _turmaRepository.GetByIdAsync(id);
                if (turma == null)
                    return false;

                if (turma.VagasDisponiveis < turma.VagasTotal)
                    await _turmaRepository.AtualizarStatusTurma(turma);

                await _turmaRepository.DeleteAsync(turma);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Erro ao deletar turma: {ex.Message}", ex);
            }
        }
    }
}