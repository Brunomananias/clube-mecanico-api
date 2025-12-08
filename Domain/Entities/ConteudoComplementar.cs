using ClubeMecanico_API.Domain.Exceptions;

namespace ClubeMecanico_API.Domain.Entities
{
    public class ConteudoComplementar : BaseEntity
    {
        public int Id { get; set; }
        public int CursoId { get; private set; }
        public string Titulo { get; private set; }
        public TipoConteudo Tipo { get; private set; }
        public string Url { get; private set; }
        public string? Descricao { get; private set; }
        public DateTime DataCriacao { get; private set; }

        // Navegação
        public virtual Curso Curso { get; private set; }

        private ConteudoComplementar() { }

        public ConteudoComplementar(int cursoId, string titulo, TipoConteudo tipo, string url)
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
