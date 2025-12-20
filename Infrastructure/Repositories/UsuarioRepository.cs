using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ClubeMecanico.Domain.Interfaces;
using ClubeMecanico_API.Domain.Entities;
using ClubeMecanico_API.Domain.Interfaces;
using ClubeMecanico_API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace YourProject.Repositories
{

    public class UsuarioRepository : IUsuarioRepository
    {
        private readonly AppDbContext _context;

        public UsuarioRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Usuario> GetByIdAsync(int id)
        {
            return await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Id == id && u.Ativo);
        }

        public async Task<Usuario> GetByIdWithEnderecosAsync(int id)
        {
            return await _context.Usuarios
                .Include(u => u.Enderecos.Where(e => e.Ativo))
                .FirstOrDefaultAsync(u => u.Id == id && u.Ativo);
        }

        public async Task<List<Usuario>> GetAllAsync()
        {
            return await _context.Usuarios
                .Where(u => u.Ativo)
                .OrderBy(u => u.Nome_Completo)
                .ToListAsync();
        }

        public async Task<List<Usuario>> GetAllWithEnderecosAsync()
        {
            return await _context.Usuarios
                .Include(u => u.Enderecos.Where(e => e.Ativo))
                .Where(u => u.Ativo)
                .OrderBy(u => u.Nome_Completo)
                .ToListAsync();
        }

        public async Task<Usuario> GetByEmailAsync(string email)
        {
            return await _context.Usuarios
                .Include(u => u.Enderecos.Where(e => e.Ativo))
                .FirstOrDefaultAsync(u => u.Email == email && u.Ativo);
        }

        public async Task<Usuario> AddAsync(Usuario usuario)
        {
            await _context.Usuarios.AddAsync(usuario);
            await _context.SaveChangesAsync();
            return usuario;
        }

        public async Task<Usuario> UpdateAsync(Usuario usuario)
        {
            _context.Entry(usuario).State = EntityState.Modified;

            // Marca propriedades que não devem ser atualizadas
            _context.Entry(usuario).Property(x => x.Email).IsModified = false;
            _context.Entry(usuario).Property(x => x.SenhaHash).IsModified = false;
            _context.Entry(usuario).Property(x => x.Tipo).IsModified = false;
            _context.Entry(usuario).Property(x => x.Data_Cadastro).IsModified = false;

            await _context.SaveChangesAsync();
            return usuario;
        }

        public async Task<Usuario> AddEnderecoAsync(int usuarioId, Endereco endereco)
        {
            var usuario = await GetByIdWithEnderecosAsync(usuarioId);
            if (usuario == null)
                return null;

            usuario.Enderecos.Add(endereco);
            await _context.SaveChangesAsync();

            return await GetByIdWithEnderecosAsync(usuarioId);
        }

        public async Task<Endereco> UpdateEnderecoAsync(Endereco endereco)
        {
            _context.Entry(endereco).State = EntityState.Modified;

            // Marca propriedades que não devem ser atualizadas
            _context.Entry(endereco).Property(x => x.Id).IsModified = false;
            _context.Entry(endereco).Property(x => x.UsuarioId).IsModified = false;
            _context.Entry(endereco).Property(x => x.DataCadastro).IsModified = false;

            await _context.SaveChangesAsync();
            return endereco;
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            return await _context.Usuarios
                .AnyAsync(u => u.Email == email && u.Ativo);
        }
    }
}
