namespace ClubeMecanico_API.API.DTOs.Requests
{
    public class AtualizarTurmaDTO
    {
        public string? Nome { get; set; }
        public DateTime? DataInicio { get; set; }
        public DateTime? DataFim { get; set; }
        public string? Horario { get; set; }
        public string? Professor { get; set; }
        public int? VagasTotal { get; set; }
        public int? VagasDisponiveis { get; set; }
        public string? Status { get; set; } // ABERTO, FECHADO, CANCELADA, CONCLUÍDA

        // Validação para quando VagasDisponiveis for alterado
        public bool? ForcarAtualizacaoVagas { get; set; } = false;
    }
}
