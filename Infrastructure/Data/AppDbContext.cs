using Microsoft.EntityFrameworkCore;
using ClubeMecanico_API.Domain.Entities;

namespace ClubeMecanico_API.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Curso> Cursos { get; set; }
        public DbSet<Turma> Turmas { get; set; }
        public DbSet<Pedido> Pedidos { get; set; }
        public DbSet<ItemPedido> ItensPedido { get; set; }
        public DbSet<Pagamento> Pagamentos { get; set; }
        public DbSet<CursoAluno> CursosAlunos { get; set; }
        public DbSet<Certificado> Certificados { get; set; }
        public DbSet<ConteudoComplementar> ConteudosComplementares { get; set; }
        public DbSet<Endereco> Enderecos { get; set; }
        public DbSet<CarrinhoTemporario> CarrinhoTemporario { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Usuario>().ToTable("usuarios");
            modelBuilder.Entity<Curso>().ToTable("cursos");
            modelBuilder.Entity<Turma>().ToTable("turmas");
            modelBuilder.Entity<Pedido>().ToTable("pedidos");
            modelBuilder.Entity<ItemPedido>().ToTable("itenspedido");
            modelBuilder.Entity<Pagamento>().ToTable("pagamentos");
            modelBuilder.Entity<CursoAluno>().ToTable("cursosalunos");
            modelBuilder.Entity<Certificado>().ToTable("certificados");
            modelBuilder.Entity<ConteudoComplementar>().ToTable("conteudoscomplementares");
            modelBuilder.Entity<CarrinhoTemporario>().ToTable("carrinho_temporario");
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                // Tabela
                entityType.SetTableName(entityType.GetTableName().ToLowerInvariant());

                // Colunas
                foreach (var property in entityType.GetProperties())
                {
                    property.SetColumnName(property.GetColumnName().ToLowerInvariant());
                }

                // Chaves primárias
                foreach (var key in entityType.GetKeys())
                {
                    key.SetName(key.GetName().ToLowerInvariant());
                }

                // Chaves estrangeiras
                foreach (var foreignKey in entityType.GetForeignKeys())
                {
                    foreignKey.SetConstraintName(foreignKey.GetConstraintName().ToLowerInvariant());
                }

                // Índices
                foreach (var index in entityType.GetIndexes())
                {
                    index.SetDatabaseName(index.GetDatabaseName().ToLowerInvariant());
                }
            }

            // Configuração Usuario
            modelBuilder.Entity<Usuario>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Nome_Completo).IsRequired().HasMaxLength(200);
                entity.Property(e => e.CPF).HasMaxLength(14);
                entity.Property(e => e.Telefone).HasMaxLength(20);
                entity.Property(e => e.Tipo);
                entity.Property(e => e.Data_Cadastro)
             .HasColumnType("timestamp without time zone")
             .HasDefaultValueSql("CURRENT_TIMESTAMP")
             .HasColumnName("data_cadastro");

                entity.Property(e => e.UltimoLogin)
                      .HasColumnType("timestamp without time zone")
                      .HasColumnName("ultimologin");

                entity.Property(e => e.Data_Nascimento)
                      .HasColumnType("timestamp without time zone")
                      .HasColumnName("data_nascimento");
                // Navegações
                entity.HasMany(u => u.Pedidos)
                      .WithOne(p => p.Aluno)
                      .HasForeignKey(p => p.AlunoId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(u => u.CursosAlunos)
                      .WithOne(ca => ca.Aluno)
                      .HasForeignKey(ca => ca.AlunoId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(u => u.Certificados)
                      .WithOne(c => c.Aluno)
                      .HasForeignKey(c => c.AlunoId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(u => u.Enderecos)
                      .WithOne(e => e.Usuario)
                      .HasForeignKey(e => e.UsuarioId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configuração CarrinhoTemporario - ADICIONE ISSO:
            modelBuilder.Entity<CarrinhoTemporario>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.UsuarioId)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("usuario_id");

                entity.Property(e => e.CursoId)
                    .IsRequired()
                    .HasColumnName("curso_id");

                entity.Property(e => e.TurmaId)
                    .HasColumnName("turma_id");

                entity.Property(e => e.DataAdicao)
                    .HasColumnType("timestamp without time zone")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP")
                    .HasColumnName("data_adicao");

                // Relacionamento com Curso
                entity.HasOne(ct => ct.Curso)
                    .WithMany()
                    .HasForeignKey(ct => ct.CursoId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Relacionamento com Turma
                entity.HasOne(ct => ct.Turma)
                    .WithMany()
                    .HasForeignKey(ct => ct.TurmaId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Índice para performance
                entity.HasIndex(e => e.UsuarioId);

                // Índice composto para evitar duplicatas
                entity.HasIndex(e => new { e.UsuarioId, e.CursoId, e.TurmaId })
                      .IsUnique()
                      .HasFilter("[turma_id] IS NOT NULL");
            });

            modelBuilder.Entity<Endereco>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UsuarioId)
                 .HasColumnName("usuario_id")
                 .IsRequired();
                entity.Property(e => e.CEP).HasMaxLength(10);
                entity.Property(e => e.Logradouro).HasMaxLength(200);
                entity.Property(e => e.Numero).HasMaxLength(20);
                entity.Property(e => e.Complemento).HasMaxLength(100);
                entity.Property(e => e.Bairro).HasMaxLength(100);
                entity.Property(e => e.Cidade).HasMaxLength(100);
                entity.Property(e => e.Estado).HasMaxLength(2);
                entity.Property(e => e.Tipo).HasMaxLength(20).HasDefaultValue("principal");
                entity.Property(e => e.Ativo).IsRequired().HasDefaultValue(true);
                entity.Property(e => e.DataCadastro)
          .HasColumnType("timestamp without time zone")
          .HasDefaultValueSql("CURRENT_TIMESTAMP")
          .HasColumnName("data_cadastro");

                entity.Property(e => e.DataAtualizacao)
                      .HasColumnType("timestamp without time zone")
                      .HasColumnName("data_atualizacao");

                // Índices para melhor performance
                entity.HasIndex(e => e.UsuarioId);
                entity.HasIndex(e => new { e.UsuarioId, e.Tipo });
                entity.HasIndex(e => e.CEP);

                // Relacionamento com Usuario
                entity.HasOne(e => e.Usuario)
                      .WithMany(u => u.Enderecos)
                      .HasForeignKey(e => e.UsuarioId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configuração Curso
            modelBuilder.Entity<Curso>(entity =>
            {
                entity.ToTable("cursos");
                entity.HasKey(e => e.Id);

                // Configurar cada propriedade com snake_case
                entity.Property(e => e.Id)
                    .HasColumnName("id");

                entity.Property(e => e.Codigo)
                    .IsRequired()
                    .HasMaxLength(20)
                    .HasColumnName("codigo");

                entity.Property(e => e.Nome)
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnName("nome");

                entity.Property(e => e.Descricao)
                    .IsRequired()
                    .HasMaxLength(500)
                    .HasColumnName("descricao");

                entity.Property(e => e.FotoUrl)
                    .HasMaxLength(500)
                    .HasColumnName("foto_url");

                entity.Property(e => e.Valor)
                    .IsRequired()
                    .HasPrecision(18, 2)
                    .HasColumnName("valor");

                entity.Property(e => e.DuracaoHoras)
                    .IsRequired()
                    .HasColumnName("duracao_horas");

                entity.Property(e => e.Nivel)
                    .HasMaxLength(50)
                    .HasColumnName("nivel");

                entity.Property(e => e.MaxAlunos)
                    .IsRequired()
                    .HasColumnName("max_alunos");

                entity.Property(e => e.ConteudoProgramatico)
                    .HasMaxLength(5000)
                    .HasColumnName("conteudo_programatico");

                entity.Property(e => e.CertificadoDisponivel)
                    .HasDefaultValue(true)
                    .HasColumnName("certificado_disponivel");

                entity.Property(e => e.Ativo)
                    .HasDefaultValue(true)
                    .HasColumnName("ativo");

                entity.Property(e => e.DataCriacao)
                    .HasDefaultValueSql("NOW()")
                    .HasColumnName("data_criacao");

                entity.Property(e => e.AdminCriador)
                    .HasColumnName("admin_criador");

                // Relacionamentos (continuam iguais)
                entity.HasMany(c => c.Turmas)
                    .WithOne(t => t.Curso)
                    .HasForeignKey(t => t.CursoId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(c => c.ConteudosComplementares)
                    .WithOne(cc => cc.Curso)
                    .HasForeignKey(cc => cc.CursoId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(c => c.CursosAlunos)
                    .WithOne(ca => ca.Curso)
                    .HasForeignKey(ca => ca.CursoId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(c => c.ItensPedido)
                    .WithOne(ip => ip.Curso)
                    .HasForeignKey(ip => ip.CursoId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            // Configuração Turma
            modelBuilder.Entity<Turma>(entity =>
            {
                entity.Property(e => e.CursoId)
                 .HasColumnName("curso_id")
                 .IsRequired();
                entity.HasKey(e => e.Id);
                entity.Property(e => e.DataInicio).IsRequired().HasColumnName("data_inicio"); ;
                entity.Property(e => e.DataFim).IsRequired().HasColumnName("data_fim"); ;
                entity.Property(e => e.Horario).HasMaxLength(50);
                entity.Property(e => e.Professor).HasMaxLength(100);
                entity.Property(e => e.VagasTotal).IsRequired().HasColumnName("vagas_total"); ;
                entity.Property(e => e.VagasDisponiveis).IsRequired().HasColumnName("vagas_disponiveis"); ;
                entity.Property(e => e.Status).IsRequired().HasConversion<string>();

                // Relacionamento com Curso
                entity.HasOne(t => t.Curso)
                      .WithMany(c => c.Turmas)
                      .HasForeignKey(t => t.CursoId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Relacionamento com ItensPedido
                entity.HasMany(t => t.ItensPedido)
                      .WithOne(i => i.Turma)
                      .HasForeignKey(i => i.TurmaId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Relacionamento com CursosAlunos
                entity.HasMany(t => t.CursosAlunos)
                      .WithOne(ca => ca.Turma)
                      .HasForeignKey(ca => ca.TurmaId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Configuração Pedido
            modelBuilder.Entity<Pedido>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.NumeroPedido).IsRequired().HasMaxLength(50);
                entity.Property(e => e.ValorTotal).IsRequired().HasPrecision(18, 2);
                entity.Property(e => e.Status).IsRequired().HasConversion<string>();
                entity.Property(e => e.DataPedido).IsRequired();

                // Relacionamento com Usuario (Aluno)
                entity.HasOne(p => p.Aluno)
                      .WithMany(u => u.Pedidos)
                      .HasForeignKey(p => p.AlunoId)
                      .OnDelete(DeleteBehavior.Restrict);

                // RELACIONAMENTO 1-to-1 com Pagamento (RESOLVE O ERRO ANTERIOR)
                entity.HasOne(p => p.Pagamento)
                      .WithOne(p => p.Pedido)
                      .HasForeignKey<Pagamento>(p => p.PedidoId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Relacionamento com ItensPedido
                entity.HasMany(p => p.Itens)
                      .WithOne(i => i.Pedido)
                      .HasForeignKey(i => i.PedidoId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configuração Pagamento
            modelBuilder.Entity<Pagamento>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.PedidoId).IsRequired();
                entity.Property(e => e.Metodo).IsRequired().HasConversion<string>();
                entity.Property(e => e.Valor).IsRequired().HasPrecision(18, 2);
                entity.Property(e => e.Status).IsRequired().HasConversion<string>();
                entity.Property(e => e.CodigoTransacao).HasMaxLength(100);
                entity.Property(e => e.DataCriacao).IsRequired();
                entity.Property(e => e.DataPagamento).IsRequired(false);

                // ÍNDICE ÚNICO para garantir relacionamento 1-to-1
                entity.HasIndex(e => e.PedidoId).IsUnique();
            });

            // Configuração ItemPedido
            modelBuilder.Entity<ItemPedido>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Preco).IsRequired().HasPrecision(18, 2);
                entity.Property(e => e.DataCompra).IsRequired();

                // Relacionamento com Pedido
                entity.HasOne(i => i.Pedido)
                      .WithMany(p => p.Itens)
                      .HasForeignKey(i => i.PedidoId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Relacionamento com Curso
                entity.HasOne(i => i.Curso)
                      .WithMany()
                      .HasForeignKey(i => i.CursoId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Relacionamento com Turma (opcional)
                entity.HasOne(i => i.Turma)
                      .WithMany(t => t.ItensPedido)
                      .HasForeignKey(i => i.TurmaId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Configuração CursoAluno
            modelBuilder.Entity<CursoAluno>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.AlunoId).IsRequired();
                entity.Property(e => e.CursoId).IsRequired();
                entity.Property(e => e.DataMatricula).IsRequired();
                entity.Property(e => e.Status).IsRequired().HasConversion<string>();

                // Relacionamento com Usuario (Aluno)
                entity.HasOne(ca => ca.Aluno)
                      .WithMany(u => u.CursosAlunos)
                      .HasForeignKey(ca => ca.AlunoId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Relacionamento com Curso
                entity.HasOne(ca => ca.Curso)
                      .WithMany(c => c.CursosAlunos)
                      .HasForeignKey(ca => ca.CursoId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Relacionamento com Turma (opcional)
                entity.HasOne(ca => ca.Turma)
                      .WithMany(t => t.CursosAlunos)
                      .HasForeignKey(ca => ca.TurmaId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Índice composto único para evitar matrícula duplicada
                entity.HasIndex(e => new { e.AlunoId, e.CursoId, e.TurmaId })
                      .IsUnique()
                      .HasFilter("[TurmaId] IS NOT NULL");
            });

            // Configuração Certificado (RESOLVE O NOVO ERRO)
            modelBuilder.Entity<Certificado>(entity =>
            {
                entity.HasKey(e => e.Id); // CHAVE PRIMÁRIA DEFINIDA
                entity.Property(e => e.CodigoCertificado).IsRequired().HasMaxLength(100);
                entity.Property(e => e.AlunoId).IsRequired();
                entity.Property(e => e.CursoId).IsRequired();
                entity.Property(e => e.DataConclusao).IsRequired();
                entity.Property(e => e.CargaHoraria).IsRequired();
                entity.Property(e => e.UrlCertificado).HasMaxLength(500);
                entity.Property(e => e.DataEmissao).IsRequired();

                // Relacionamento com Usuario (Aluno)
                entity.HasOne(c => c.Aluno)
                      .WithMany(u => u.Certificados)
                      .HasForeignKey(c => c.AlunoId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Relacionamento com Curso
                entity.HasOne(c => c.Curso)
                      .WithMany()
                      .HasForeignKey(c => c.CursoId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Índice único para código do certificado
                entity.HasIndex(e => e.CodigoCertificado).IsUnique();
            });

            modelBuilder.Entity<ConteudoComplementar>(entity =>
            {
                entity.HasKey(e => e.Id); // CHAVE PRIMÁRIA OBRIGATÓRIA

                entity.Property(e => e.CursoId).IsRequired();
                entity.Property(e => e.Titulo).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Tipo).IsRequired().HasConversion<string>();
                entity.Property(e => e.Url).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Descricao).HasMaxLength(1000);
                entity.Property(e => e.DataCriacao).IsRequired();

                // Relacionamento com Curso
                entity.HasOne(cc => cc.Curso)
                      .WithMany() // Se Curso não tiver navegação para ConteudoComplementar
                      .HasForeignKey(cc => cc.CursoId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Índice para consultas por curso
                entity.HasIndex(e => e.CursoId);
            });
        }
    }
}