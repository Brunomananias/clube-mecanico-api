//using ClubeMecanico.Domain.Interfaces;
//using ClubeMecanico_API.Domain.Entities;
//using ClubeMecanico_API.Infrastructure.Data;

//namespace ClubeMecanico_API.Infrastructure.Repositories
//{
//    public class PedidoRepository : IPedidoRepository
//    {
//        private readonly AppDbContext _context;

//        public PedidoRepository(AppDbContext context)
//        {
//            _context = context;
//        }

//        public async Task<Pedido?> GetByIdAsync(int id)
//        {
//            return await _context.Pedidos
//                .Include(p => p.Aluno)
//                .Include(p => p.Itens)
//                    .ThenInclude(i => i.Curso)
//                .Include(p => p.Itens)
//                    .ThenInclude(i => i.Turma)
//                .Include(p => p.Pagamento)
//                .FirstOrDefaultAsync(p => p.Id == id);
//        }

//        public async Task<Pedido?> GetByNumeroAsync(string numeroPedido)
//        {
//            return await _context.Pedidos
//                .Include(p => p.Aluno)
//                .Include(p => p.Itens)
//                .Include(p => p.Pagamento)
//                .FirstOrDefaultAsync(p => p.NumeroPedido == numeroPedido);
//        }

//        public async Task<IEnumerable<Pedido>> GetByAlunoIdAsync(int alunoId)
//        {
//            return await _context.Pedidos
//                .Where(p => p.AlunoId == alunoId)
//                .Include(p => p.Itens)
//                    .ThenInclude(i => i.Curso)
//                .Include(p => p.Pagamento)
//                .OrderByDescending(p => p.DataPedido)
//                .ToListAsync();
//        }

//        public async Task AddAsync(Pedido pedido)
//        {
//            await _context.Pedidos.AddAsync(pedido);
//            await _context.SaveChangesAsync();
//        }

//        public async Task UpdateAsync(Pedido pedido)
//        {
//            _context.Pedidos.Update(pedido);
//            await _context.SaveChangesAsync();
//        }

//        public async Task<string> GerarNumeroPedidoAsync()
//        {
//            var hoje = DateTime.UtcNow;
//            var quantidadeHoje = await _context.Pedidos
//                .CountAsync(p => p.DataPedido.Date == hoje.Date);

//            return $"PED{hoje:yyyyMMdd}-{(quantidadeHoje + 1):D4}";
//        }
//    }
//}
