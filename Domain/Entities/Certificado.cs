using ClubeMecanico_API.Domain.Exceptions;

namespace ClubeMecanico_API.Domain.Entities
{
    public class Certificado : BaseEntity
    {
        public int Id { get; set; }
        public DateTime DataConclusao { get; set; }
        public int CargaHoraria { get; set; }
        public string? UrlCertificado { get; set; }
        public DateTime DataEmissao { get; set; }
        public int CursoAlunoId { get; set; }
        public Certificado() { }

        public Certificado(int cursoAlunoId, DateTime dataConclusao, int cargaHoraria)
        {
            CursoAlunoId = cursoAlunoId;
            DataConclusao = dataConclusao;
            CargaHoraria = cargaHoraria;
            DataEmissao = DateTime.UtcNow;
        }

        public void AtualizarUrl(string url)
        {
            UrlCertificado = url;
        }
    }
}
