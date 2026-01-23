using ClubeMecanico.Domain.Interfaces;
using ClubeMecanico_API.Domain.Entities;
using ClubeMecanico_API.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
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

        [Authorize]
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

        public async Task AdicionarMatricula(CursoAluno curso)
        {
            await _context.CursosAlunos.AddAsync(curso);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Curso curso)
        {
            var existe = await _context.Cursos.AnyAsync(c => c.Id == curso.Id);
            if (!existe)
            {
                throw new KeyNotFoundException($"Curso com ID {curso.Id} não encontrado");
            }

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
                .Where(t => t.CursoId == cursoId && t.Status == "ATIVA")
                .OrderBy(t => t.DataInicio)
                .ToListAsync();
        }

        public async Task<IEnumerable<CursoAluno>> GetCursosComTurmasPorAluno(int alunoId)
        {
            return await _context.CursosAlunos
                .Where(ca => ca.AlunoId == alunoId)
                .Include(ca => ca.Curso)  // Inclui o curso
                .Include(ca => ca.Turma)  // Inclui a turma
                .Select(ca => new CursoAluno
                {
                    Id = ca.Id,
                    AlunoId = ca.AlunoId,
                    Curso = new Curso
                    {
                        Id = ca.Curso.Id,
                        Nome = ca.Curso.Nome,
                        Descricao = ca.Curso.Descricao,
                        DuracaoHoras = ca.Curso.DuracaoHoras,
                        Valor = ca.Curso.Valor
                    },
                    Turma = new Turma
                    {
                        Id = ca.Turma.Id,
                        DataInicio = ca.Turma.DataInicio,
                        DataFim = ca.Turma.DataFim,                        
                    },
                    Status = ca.Status,
                    Progresso = ca.Progresso,
                    DataMatricula = ca.DataMatricula,
                })
                .ToListAsync();
        }

        public async Task<bool> DeletarCurso(int id)
        {
            var curso = await _context.Cursos.FindAsync(id);
            if (curso == null)
                return false;

            _context.Cursos.Remove(curso);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
