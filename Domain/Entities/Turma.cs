using ClubeMecanico_API.Domain.Exceptions;

namespace ClubeMecanico_API.Domain.Entities
{
    public class Turma : BaseEntity
    {
        public int Id { get; set; } 
        public int CursoId { get; set; }
        public DateTime DataInicio { get; set; }
        public DateTime DataFim { get; set; }
        public string? Horario { get; set; }
        public string? Professor { get; set; }
        public int VagasTotal { get; set; }
        public int VagasDisponiveis { get; set; }
        public string Status { get; set; }

        // Navegação
        public virtual Curso Curso { get; private set; }
        public virtual ICollection<ItemPedido> ItensPedido { get; private set; }
        public virtual ICollection<CursoAluno> CursosAlunos { get; private set; }

        public Turma() { }

        public Turma(int cursoId, DateTime dataInicio, DateTime dataFim, int vagasTotal, string? professor = null, string? horario = null, string? status = null)
        {
            CursoId = cursoId;
            DataInicio = dataInicio;
            DataFim = dataFim;
            VagasTotal = vagasTotal;
            VagasDisponiveis = vagasTotal;
            Professor = professor;
            Horario = horario;
            Status = status;

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

            if (Status != "ABERTO")
                throw new DomainException("Turma não está aberta para matrículas");

            VagasDisponiveis--;

            if (VagasDisponiveis == 0)
                Status = "LOTADA";
        }

        public void LiberarVaga()
        {
            VagasDisponiveis++;

            if (Status == "LOTADA" && VagasDisponiveis > 0)
                Status = "ABERTA";
        }

        public void IniciarTurma()
        {
            if (DataInicio > DateTime.UtcNow.Date)
                throw new DomainException("A turma ainda não começou");

            Status = "EMANDAMENTO";
        }

        public void ConcluirTurma()
        {
            Status = "CONCLUIDA";
        }

        public void CancelarTurma()
        {
            Status = "CANCELADA";
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
