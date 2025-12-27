using Microsoft.IdentityModel.Tokens;
using System.Text;
using ClubeMecanico.Application.Interfaces;
using ClubeMecanico_API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Cors.Infrastructure;
using ClubeMecanico_API.Infrastructure.Repositories;
using ClubeMecanico.Domain.Interfaces;
using ClubeMecanico.Application.Services;
using System.Text.Json.Serialization;
using ClubeMecanico_API.Domain.Interfaces;
using MercadoPago.Config;
using YourProject.Repositories;
using ClubeMecanico_API.API.Services;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);
MercadoPagoConfig.AccessToken = builder.Configuration["MercadoPago:AccessToken"];

// 1. Configuração do banco de dados
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddDistributedMemoryCache(); // Necessário para sessão

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Tempo de expiração
    options.Cookie.HttpOnly = true; // Segurança
    options.Cookie.IsEssential = true; // GDPR
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Use sempre em produção
});
// 2. Serviços de aplicação
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICursoRepository, CursoRepository>();
builder.Services.AddScoped<ICursoService, CursoService>();
builder.Services.AddScoped<ITurmaService, TurmaService>();
builder.Services.AddScoped<ITurmaRepository, TurmaRepository>();
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<CloudinaryService>();

builder.Services.Configure<FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = 10485760; // 10MB
    options.MultipartHeadersLengthLimit = int.MaxValue;
});
// 3. Configuração da autenticação JWT
var jwtKey = builder.Configuration["JwtSettings:Key"];
if (string.IsNullOrEmpty(jwtKey))
{
    throw new ArgumentNullException("JwtSettings:Key", "Chave JWT não configurada no appsettings.json");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// Program.cs
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// 5. Configuração do Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Clube Mecânico API",
        Version = "v1",
        Description = "API para gerenciamento do Clube Mecânico"
    });

    // Configuração de segurança JWT
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Insira o token JWT no formato: Bearer {seu_token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// 6. HttpClient
builder.Services.AddHttpClient();

// 7. CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// CONSTRUIR APLICAÇÃO
var app = builder.Build();

// CONFIGURAR PIPELINE HTTP

// Habilitar Swagger sempre (tanto em desenvolvimento quanto produção)
app.UseSwagger();

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Clube Mecânico API v1");
    c.RoutePrefix = "swagger"; // Acesse via: https://localhost:7289/swagger
    c.DisplayRequestDuration();
    c.EnableTryItOutByDefault();
    c.DefaultModelExpandDepth(2);
    c.EnableFilter();
});

// Middlewares na ORDEM CORRETA
app.UseHttpsRedirection(); // Adicione esta linha
app.UseStaticFiles();
app.UseSession();
app.UseRouting(); // Adicione esta linha
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Rota padrão redirecionando para o Swagger
app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();