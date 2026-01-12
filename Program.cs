using Microsoft.IdentityModel.Tokens;
using System.Text;
using ClubeMecanico.Application.Interfaces;
using ClubeMecanico_API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using ClubeMecanico_API.Infrastructure.Repositories;
using ClubeMecanico.Domain.Interfaces;
using ClubeMecanico.Application.Services;
using System.Text.Json.Serialization;
using ClubeMecanico_API.Domain.Interfaces;
using MercadoPago.Config;
using YourProject.Repositories;
using ClubeMecanico_API.API.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Cors;

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine("=".PadRight(60, '='));
Console.WriteLine("?? DEBUG DAS VARIÁVEIS DE AMBIENTE NO RENDER");
Console.WriteLine("=".PadRight(60, '='));

// 1. Mostra todas as variáveis
var allVars = Environment.GetEnvironmentVariables();
Console.WriteLine($"Total de variáveis: {allVars.Count}");

// Lista todas as variáveis disponíveis
foreach (System.Collections.DictionaryEntry env in allVars)
{
    var key = env.Key.ToString();
    var value = env.Value?.ToString();

    // Mostra todas (você vai ver o que está realmente disponível)
    if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
    {
        Console.WriteLine($"  {key.PadRight(30)} = {value}");
    }
}
// ========== LEITURA DE TODAS AS VARIÁVEIS DE AMBIENTE DO RENDER ==========
Console.WriteLine("?? Carregando variáveis de ambiente...");

// 1. Banco de dados
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
var connectionStringFromEnv = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnections");

// 2. JWT Settings
var jwtKey = Environment.GetEnvironmentVariable("JwtSettings__Key") ??
             builder.Configuration["JwtSettings:Key"];
var jwtIssuer = Environment.GetEnvironmentVariable("JwtSettings__Issuer") ??
                builder.Configuration["JwtSettings:Issuer"];
var jwtAudience = Environment.GetEnvironmentVariable("JwtSettings__Audience") ??
                  builder.Configuration["JwtSettings:Audience"];
var jwtExpirationDays = Environment.GetEnvironmentVariable("JwtSettings__ExpirationDays") ??
                        builder.Configuration["JwtSettings:ExpirationDays"];

// 3. Mercado Pago
var mercadoPagoToken = Environment.GetEnvironmentVariable("MercadoPago__AccessToken") ??
                       builder.Configuration["MercadoPago:AccessToken"];

// 4. Cloudinary
var cloudinaryCloudName = Environment.GetEnvironmentVariable("Cloudinary__CloudName") ??
                          builder.Configuration["Cloudinary:CloudName"];
var cloudinaryApiKey = Environment.GetEnvironmentVariable("Cloudinary__ApiKey") ??
                       builder.Configuration["Cloudinary:ApiKey"];
var cloudinaryApiSecret = Environment.GetEnvironmentVariable("Cloudinary__ApiSecret") ??
                          builder.Configuration["Cloudinary:ApiSecret"];

// 5. PIX
var pixChavePix = Environment.GetEnvironmentVariable("Pix__ChavePix") ??
                  builder.Configuration["Pix:ChavePix"];

// ========== LOG DAS CONFIGURAÇÕES (sem expor dados sensíveis) ==========
Console.WriteLine($"? JWT Configurado: {!string.IsNullOrEmpty(jwtKey)}");
Console.WriteLine($"? Mercado Pago Configurado: {!string.IsNullOrEmpty(mercadoPagoToken)}");
Console.WriteLine($"? Cloudinary Configurado: {!string.IsNullOrEmpty(cloudinaryCloudName)}");
Console.WriteLine($"? PIX Configurado: {!string.IsNullOrEmpty(pixChavePix)}");

// ========== CONFIGURAÇÃO DO BANCO DE DADOS ==========
string connectionString;

if (!string.IsNullOrEmpty(connectionStringFromEnv))
{
    // Usa ConnectionStrings__DefaultConnection do Render
    connectionString = connectionStringFromEnv;
    Console.WriteLine("? Usando ConnectionStrings__DefaultConnection do ambiente");
}
else if (!string.IsNullOrEmpty(databaseUrl))
{
    // Converte DATABASE_URL do Render para string de conexão
    try
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':');

        var host = uri.Host;
        var port = uri.Port;
        var database = uri.AbsolutePath.Trim('/');
        var user = userInfo[0];
        var password = userInfo[1];

        connectionString = $"Host={host};" +
                          $"Port={port};" +
                          $"Database={database};" +
                          $"Username={user};" +
                          $"Password={password};" +
                          $"SSL Mode=Require;" +
                          $"Trust Server Certificate=true;" +
                          $"Pooling=true;" +
                          $"Timeout=30;";

        Console.WriteLine($"? Convertendo DATABASE_URL: Host={host}, Database={database}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"? Erro ao converter DATABASE_URL: {ex.Message}");
        throw;
    }
}
else
{
    // Fallback para appsettings.json (desenvolvimento)
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    Console.WriteLine("?? Usando appsettings.json (modo desenvolvimento)");
}

// Log seguro da string de conexão
var safeLog = connectionString?.Replace("Password=", "Password=***");
Console.WriteLine($"?? String de conexão: {safeLog}");

// ========== VALIDAÇÕES ==========
if (string.IsNullOrEmpty(jwtKey))
{
    throw new ArgumentNullException(nameof(jwtKey),
        "? JwtSettings__Key não configurada no Render. Configure esta variável de ambiente.");
}

if (jwtKey.Length < 32)
{
    Console.WriteLine($"?? AVISO: Chave JWT tem apenas {jwtKey.Length} caracteres. Recomendado: mínimo 32.");
}

// ========== CONFIGURAÇÃO DOS SERVIÇOS ==========
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
// 1. Banco de dados
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// 2. Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// 3. Mercado Pago
if (!string.IsNullOrEmpty(mercadoPagoToken))
{
    MercadoPagoConfig.AccessToken = mercadoPagoToken;
    Console.WriteLine("? Mercado Pago configurado com sucesso");
}
else
{
    Console.WriteLine("?? ATENÇÃO: MercadoPago__AccessToken não configurado!");
}

// 4. Serviços da aplicação
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICursoRepository, CursoRepository>();
builder.Services.AddScoped<ICursoService, CursoService>();
builder.Services.AddScoped<ITurmaService, TurmaService>();
builder.Services.AddScoped<ITurmaRepository, TurmaRepository>();
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<IUsuarioService, UsuarioService>();

// 5. Cloudinary Service (configura via construtor)
builder.Services.AddScoped<CloudinaryService>();

// 6. Upload de arquivos
builder.Services.Configure<FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = 10485760; // 10MB
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

// 7. Autenticação JWT
var tokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuerSigningKey = true,
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
    ValidateIssuer = !string.IsNullOrEmpty(jwtIssuer),
    ValidateAudience = !string.IsNullOrEmpty(jwtAudience),
    ValidateLifetime = true,
    ClockSkew = TimeSpan.Zero
};

if (!string.IsNullOrEmpty(jwtIssuer))
    tokenValidationParameters.ValidIssuer = jwtIssuer;

if (!string.IsNullOrEmpty(jwtAudience))
    tokenValidationParameters.ValidAudience = jwtAudience;

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = tokenValidationParameters;
});

// 8. Configuração do Token Service (se você tiver)
// Adicione este serviço se precisar gerar tokens
builder.Services.AddSingleton(new TokenServiceConfig
{
    Key = jwtKey,
    Issuer = jwtIssuer ?? "clube-mecanico-api",
    Audience = jwtAudience ?? "clube-mecanico-app",
    ExpirationDays = int.TryParse(jwtExpirationDays, out var days) ? days : 7
});

// 9. Configuração do PIX Service (se você tiver)
if (!string.IsNullOrEmpty(pixChavePix))
{
    builder.Services.AddSingleton(new PixConfig
    {
        ChavePix = pixChavePix
    });
    Console.WriteLine("? PIX configurado com sucesso");
}

// 10. Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// 11. Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Clube Mecânico API",
        Version = "v1",
        Description = "API para gerenciamento do Clube Mecânico"
    });

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

// 12. HttpClient
builder.Services.AddHttpClient();

// 13. CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ========== CONSTRUIR APLICAÇÃO ==========
var app = builder.Build();

// ========== MIDDLEWARE DE HEALTH CHECK ==========
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/health")
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var canConnect = await dbContext.Database.CanConnectAsync();

            var healthStatus = new
            {
                status = canConnect ? "healthy" : "unhealthy",
                database = canConnect ? "connected" : "disconnected",
                timestamp = DateTime.UtcNow,
                environment = app.Environment.EnvironmentName,
                services = new
                {
                    jwt = !string.IsNullOrEmpty(jwtKey),
                    mercadoPago = !string.IsNullOrEmpty(mercadoPagoToken),
                    cloudinary = !string.IsNullOrEmpty(cloudinaryCloudName),
                    pix = !string.IsNullOrEmpty(pixChavePix)
                }
            };

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(healthStatus);
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new
            {
                status = "error",
                error = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
        return;
    }

    await next();
});

// ========== MIDDLEWARE DE CONFIGURAÇÃO ==========
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Clube Mecânico API v1");
    c.RoutePrefix = "swagger";
    c.DisplayRequestDuration();
    c.EnableTryItOutByDefault();
    c.DefaultModelExpandDepth(2);
    c.EnableFilter();
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseSession();
app.UseRouting();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger"));

// ========== LOG INICIAL ==========
Console.WriteLine("\n?? Clube Mecânico API INICIADA");
Console.WriteLine("=".PadRight(50, '='));
Console.WriteLine($"?? Ambiente: {app.Environment.EnvironmentName}");
Console.WriteLine($"?? JWT: {(string.IsNullOrEmpty(jwtKey) ? "? NÃO CONFIGURADO" : "? CONFIGURADO")}");
Console.WriteLine($"?? Mercado Pago: {(string.IsNullOrEmpty(mercadoPagoToken) ? "? NÃO CONFIGURADO" : "? CONFIGURADO")}");
Console.WriteLine($"?? Cloudinary: {(string.IsNullOrEmpty(cloudinaryCloudName) ? "? NÃO CONFIGURADO" : "? CONFIGURADO")}");
Console.WriteLine($"?? PIX: {(string.IsNullOrEmpty(pixChavePix) ? "? NÃO CONFIGURADO" : "? CONFIGURADO")}");
Console.WriteLine("=".PadRight(50, '='));
Console.WriteLine($"?? URL: https://{Environment.GetEnvironmentVariable("RENDER_SERVICE_NAME")}.onrender.com");
Console.WriteLine($"?? Health Check: /health");
Console.WriteLine($"?? Swagger: /swagger\n");

app.Run();

// ========== CLASSES AUXILIARES ==========
public class TokenServiceConfig
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpirationDays { get; set; } = 7;
}

public class PixConfig
{
    public string ChavePix { get; set; } = string.Empty;
}