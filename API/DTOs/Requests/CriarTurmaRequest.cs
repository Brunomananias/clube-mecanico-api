namespace ClubeMecanico_API.API.DTOs.Requests
{
    public class CriarTurmaRequest
    {
        public int CursoId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public DateTime DataInicio { get; set; }
        public DateTime DataFim { get; set; }
        public string Horario { get; set; } = string.Empty;
        public string Professor { get; set; } = string.Empty;
        public int VagasTotal { get; set; }
        public int VagasDisponiveis { get; set; }
        public string Status { get; set; } = "ABERTO";
    }
}
