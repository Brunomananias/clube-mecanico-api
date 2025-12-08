using ClubeMecanico_API.Domain.Exceptions;

namespace ClubeMecanico_API.Domain.Entities
{
    public class Certificado : BaseEntity
    {
        public int Id { get; set; }
        public string CodigoCertificado { get; private set; }
        public int AlunoId { get; private set; }
        public int CursoId { get; private set; }
        public DateTime DataConclusao { get; private set; }
        public int CargaHoraria { get; private set; }
        public string? UrlCertificado { get; private set; }
        public DateTime DataEmissao { get; private set; }

        // Navegação
        public virtual Usuario Aluno { get; private set; }
        public virtual Curso Curso { get; private set; }

        private Certificado() { }

        public Certificado(string codigoCertificado, int alunoId, int cursoId, DateTime dataConclusao, int cargaHoraria)
        {
            CodigoCertificado = codigoCertificado;
            AlunoId = alunoId;
            CursoId = cursoId;
            DataConclusao = dataConclusao;
            CargaHoraria = cargaHoraria;
            DataEmissao = DateTime.UtcNow;

            Validar();
        }

        public void AtualizarUrl(string url)
        {
            UrlCertificado = url;
        }

        private void Validar()
        {
            if (string.IsNullOrWhiteSpace(CodigoCertificado))
                throw new DomainException("Código do certificado é obrigatório");

            if (CargaHoraria <= 0)
                throw new DomainException("Carga horária deve ser maior que zero");
        }
    }
}
