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

var builder = WebApplication.CreateBuilder(args);

// 1. Configuração do banco de dados
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Serviços de aplicação
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICursoRepository, CursoRepository>();
builder.Services.AddScoped<ICursoService, CursoService>();
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

// 4. Controllers
builder.Services.AddControllers();

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
app.UseRouting(); // Adicione esta linha
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Rota padrão redirecionando para o Swagger
app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();