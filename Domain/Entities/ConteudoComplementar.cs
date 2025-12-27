using ClubeMecanico_API.Domain.Exceptions;

namespace ClubeMecanico_API.Domain.Entities
{
    public class ConteudoComplementar : BaseEntity
    {
        public int Id { get; set; }
        public int CursoId { get; set; }
        public string Titulo { get; set; }
        public string Tipo { get; set; }
        public string Url { get; set; }
        public string? Descricao { get; set; }
        public DateTime DataCriacao { get; set; }

        public ConteudoComplementar() { }

        public ConteudoComplementar(int cursoId, string titulo, string tipo, string url)
        {
            CursoId = cursoId;
            Titulo = titulo;
            Tipo = tipo;
            Url = url;
            DataCriacao = DateTime.UtcNow;

            Validar();
        }

        private void Validar()
        {
            if (string.IsNullOrWhiteSpace(Titulo))
                throw new DomainException("Título do conteúdo é obrigatório");

            if (string.IsNullOrWhiteSpace(Url))
                throw new DomainException("URL do conteúdo é obrigatória");
        }
    }

    public enum TipoConteudo
    {
        Video,
        PDF,
        Link,
        Imagem,
        Arquivo
    }
}
