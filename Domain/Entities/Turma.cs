using ClubeMecanico_API.Domain.Exceptions;

namespace ClubeMecanico_API.Domain.Entities
{
    public class Turma : BaseEntity
    {
        public int Id { get; set; } 
        public int CursoId { get; private set; }
        public DateTime DataInicio { get; private set; }
        public DateTime DataFim { get; private set; }
        public string? Horario { get; private set; }
        public string? Professor { get; private set; }
        public int VagasTotal { get; private set; }
        public int VagasDisponiveis { get; private set; }
        public StatusTurma Status { get; private set; }

        // Navegação
        public virtual Curso Curso { get; private set; }
        public virtual ICollection<ItemPedido> ItensPedido { get; private set; }
        public virtual ICollection<CursoAluno> CursosAlunos { get; private set; }

        private Turma() { }

        public Turma(int cursoId, DateTime dataInicio, DateTime dataFim, int vagasTotal, string? professor = null, string? horario = null)
        {
            CursoId = cursoId;
            DataInicio = dataInicio;
            DataFim = dataFim;
            VagasTotal = vagasTotal;
            VagasDisponiveis = vagasTotal;
            Professor = professor;
            Horario = horario;
            Status = StatusTurma.Aberta;

            Validar();
        }

        public void Atualizar(DateTime dataInicio, DateTime dataFim, string? horario, string? professor, int vagasTotal)
        {
            DataInicio = dataInicio;
            DataFim = dataFim;
            Horario = horario;
            Professor = professor;
            VagasTotal = vagasTotal;
            Validar();
        }

        public void ReservarVaga()
        {
            if (VagasDisponiveis <= 0)
                throw new DomainException("Não há vagas disponíveis nesta turma");

            if (Status != StatusTurma.Aberta)
                throw new DomainException("Turma não está aberta para matrículas");

            VagasDisponiveis--;

            if (VagasDisponiveis == 0)
                Status = StatusTurma.Lotada;
        }

        public void LiberarVaga()
        {
            VagasDisponiveis++;

            if (Status == StatusTurma.Lotada && VagasDisponiveis > 0)
                Status = StatusTurma.Aberta;
        }

        public void IniciarTurma()
        {
            if (DataInicio > DateTime.UtcNow.Date)
                throw new DomainException("A turma ainda não começou");

            Status = StatusTurma.EmAndamento;
        }

        public void ConcluirTurma()
        {
            Status = StatusTurma.Concluida;
        }

        public void CancelarTurma()
        {
            Status = StatusTurma.Cancelada;
        }

        private void Validar()
        {
            if (DataFim <= DataInicio)
                throw new DomainException("Data de término deve ser posterior à data de início");

            if (VagasTotal <= 0)
                throw new DomainException("Número de vagas deve ser maior que zero");

            if (VagasDisponiveis > VagasTotal)
                throw new DomainException("Vagas disponíveis não podem ser maiores que vagas totais");
        }
    }

    public enum StatusTurma
    {
        Aberta,
        Lotada,
        EmAndamento,
        Concluida,
        Cancelada
    }
}
