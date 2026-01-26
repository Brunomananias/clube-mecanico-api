namespace ClubeMecanico_API.API.DTOs.Requests
{
    public class AdicionarCertificadoRequest
    {
        public DateTime DataConclusao { get; set; }
        public int CargaHoraria { get; set; }
        public string? UrlCertificado { get; set; }
        public DateTime DataEmissao { get; set; }
        public int CursoAlunoId { get; set; }
    }
}