// Infrastructure/Repositories/TurmaRepository.cs
using Microsoft.EntityFrameworkCore;
using ClubeMecanico.Domain.Interfaces;
using ClubeMecanico_API.Domain.Entities;
using ClubeMecanico_API.Infrastructure.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ClubeMecanico_API.Domain.Interfaces;

namespace ClubeMecanico_API.Infrastructure.Repositories
{
    public class TurmaRepository : ITurmaRepository
    {
        private readonly AppDbContext _context;

        public TurmaRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Turma?> GetByIdAsync(int id)
        {
            return await _context.Turmas
                .Include(t => t.Curso)
                .Include(t => t.ItensPedido)
                .Include(t => t.CursosAlunos)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<IEnumerable<Turma>> GetAllAsync()
        {
            return await _context.Turmas
                .Include(t => t.Curso)
                .Where(t => t.Status != "CANCELADA")
                .OrderBy(t => t.DataInicio)
                .ToListAsync();
        }

        public async Task<IEnumerable<Turma>> GetByCursoIdAsync(int cursoId)
        {
            return await _context.Turmas
                .Where(t => t.CursoId == cursoId && t.Status != "CANCELADA")
                .OrderBy(t => t.DataInicio)
                .ToListAsync();
        }

        public async Task<IEnumerable<Turma>> GetAtivasByCursoIdAsync(int cursoId)
        {
            return await _context.Turmas
                .Where(t => t.CursoId == cursoId &&
                           t.Status == "ABERTO" &&
                           t.VagasDisponiveis > 0 &&
                           t.DataInicio > DateTime.UtcNow)
                .OrderBy(t => t.DataInicio)
                .ToListAsync();
        }

        public async Task<IEnumerable<Turma>> GetTurmasAtivasAsync()
        {
            return await _context.Turmas
                .Where(t => t.Status == "ABERTO" &&
                           t.VagasDisponiveis > 0 &&
                           t.DataInicio > DateTime.UtcNow)
                .Include(t => t.Curso)
                .OrderBy(t => t.DataInicio)
                .ToListAsync();
        }

        public async Task AddAsync(Turma turma)
        {
            await _context.Turmas.AddAsync(turma);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Turma turma)
        {
            _context.Turmas.Update(turma);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Turma turma)
        {
            // Soft delete - apenas marca como cancelada
            turma.Status = "CANCELADA";
            await UpdateAsync(turma);

            // Ou para hard delete:
            // _context.Turmas.Remove(turma);
            // await _context.SaveChangesAsync();
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.Turmas.AnyAsync(t => t.Id == id);
        }

        public async Task<bool> HasAlunosMatriculadosAsync(int turmaId)
        {
            return await _context.CursosAlunos
                .AnyAsync(ca => ca.TurmaId == turmaId &&
                               ca.Status != "CANCELADO");
        }

        public async Task<bool> HasPedidosAssociadosAsync(int turmaId)
        {
            return await _context.ItensPedido
                .AnyAsync(ip => ip.TurmaId == turmaId);
        }
    }
}