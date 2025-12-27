// API/DTOs/Requests/MatricularAlunoCursoDTO.cs
namespace ClubeMecanico_API.API.DTOs.Requests
{
    public class MatricularAlunoCursoDTO
    {
        public int CursoId { get; set; }
        public int TurmaId { get; set; }
        public int AlunoId { get; set; }
        public string Status { get; set; } = "Ativo"; // Valor padrão
    }
}