using ClubeMecanico.Domain.Interfaces;
using ClubeMecanico_API.Domain.Entities;
using ClubeMecanico_API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ClubeMecanico_API.Infrastructure.Repositories
{
    public class CursoRepository : ICursoRepository
    {
        private readonly AppDbContext _context;

        public CursoRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Curso?> GetByIdAsync(int id)
        {
            return await _context.Cursos
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<IEnumerable<Curso>> GetAllAsync()
        {
            return await _context.Cursos
                .Where(c => c.Ativo)
                .ToListAsync();
        }

        

        public async Task AddAsync(Curso curso)
        {
            await _context.Cursos.AddAsync(curso);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Curso curso)
        {
            _context.Cursos.Update(curso);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Curso curso)
        {
            curso.Desativar();
            await UpdateAsync(curso);
        }

        public async Task<bool> CodigoExistsAsync(string codigo)
        {
            return await _context.Cursos
                .AnyAsync(c => c.Codigo == codigo);
        }

        public async Task<IEnumerable<Curso>> GetCursosDestaqueAsync()
        {
            return await _context.Cursos
                .Where(c => c.Ativo)
                .OrderByDescending(c => c.DataCriacao)
                .Take(10)
                .ToListAsync();
        }

        public async Task<IEnumerable<Turma>> GetTurmasByCursoIdAsync(int cursoId)
        {
            return await _context.Turmas
                .Where(t => t.CursoId == cursoId && t.Status == "ABERTO")
                .OrderBy(t => t.DataInicio)
                .ToListAsync();
        }
    }
}
