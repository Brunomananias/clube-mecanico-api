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
Console.WriteLine($"?? AMBIENTE: {builder.Environment.EnvironmentName}");
Console.WriteLine("=".PadRight(60, '='));

// ========== CONFIGURAÇÃO DE AMBIENTE ==========
var isDevelopment = builder.Environment.IsDevelopment();
var isRender = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RENDER"));

Console.WriteLine($"?? Modo Desenvolvimento: {isDevelopment}");
Console.WriteLine($"?? Executando no Render: {isRender}");

// ========== CONFIGURAÇÃO DO CONFIGURATION BUILDER ==========
// Isso garante que as variáveis de ambiente sejam carregadas corretamente
builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables() // Variáveis de ambiente têm prioridade máxima
    .AddUserSecrets<Program>(optional: true); // Para desenvolvimento local com segredos

// ========== LEITURA DAS CONFIGURAÇÕES ==========
// O Configuration já prioriza: Variáveis de Ambiente > appsettings.{Environment}.json > appsettings.json

// 1. Banco de Dados
var connectionString = GetConnectionString(builder.Configuration, isRender);

// 2. JWT Settings
var jwtSettings = GetJwtSettings(builder.Configuration);

// 3. Cloudinary Settings
var cloudinarySettings = GetCloudinarySettings(builder.Configuration);

// 4. Outras configurações
var mercadoPagoToken = builder.Configuration["MercadoPago:AccessToken"];
var pixChavePix = builder.Configuration["Pix:ChavePix"];

// ========== LOG DAS CONFIGURAÇÕES (seguro) ==========
LogConfigurationSafe(connectionString, jwtSettings, cloudinarySettings,
    mercadoPagoToken, pixChavePix, isRender);

// ========== VALIDAÇÕES ==========
ValidateRequiredSettings(jwtSettings, connectionString);

// ========== CONFIGURAÇÃO DOS SERVIÇOS ==========

// Adicione isso antes de AddDbContext
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// Em seguida, configure seu DbContext normalmente
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = isDevelopment ? SameSiteMode.Lax : SameSiteMode.Strict;
    options.Cookie.SecurePolicy = isDevelopment ? CookieSecurePolicy.None : CookieSecurePolicy.Always;
});

// 3. Mercado Pago
if (!string.IsNullOrEmpty(mercadoPagoToken))
{
    MercadoPagoConfig.AccessToken = mercadoPagoToken;
    Console.WriteLine("? Mercado Pago configurado com sucesso");
}
else
{
    Console.WriteLine("?? AVISO: Mercado Pago não configurado (modo sandbox)");
}

// 4. Serviços da aplicação
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICursoRepository, CursoRepository>();
builder.Services.AddScoped<ICursoService, CursoService>();
builder.Services.AddScoped<ITurmaService, TurmaService>();
builder.Services.AddScoped<ITurmaRepository, TurmaRepository>();
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<IUsuarioService, UsuarioService>();

// 5. Cloudinary Service - AGORA COM IConfiguration NO CONSTRUTOR
// O serviço será instanciado com IConfiguration, que contém todas as configurações
builder.Services.AddScoped<CloudinaryService>();

// 6. Upload de arquivos
var maxFileSizeMB = int.TryParse(builder.Configuration["FileUpload:MaxFileSizeMB"], out var size) ? size : 10;
builder.Services.Configure<FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = maxFileSizeMB * 1024 * 1024; // Converte MB para bytes
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

// 7. Autenticação JWT
ConfigureJwtAuthentication(builder.Services, jwtSettings);

// 8. Configuração do Token Service
builder.Services.AddSingleton(new TokenServiceConfig
{
    Key = jwtSettings.Key,
    Issuer = jwtSettings.Issuer,
    Audience = jwtSettings.Audience,
    ExpirationDays = jwtSettings.ExpirationDays
});

// 9. Configuração do PIX Service
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
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// 11. Swagger
ConfigureSwagger(builder.Services);

// 12. HttpClient
builder.Services.AddHttpClient();

// 13. CORS
ConfigureCors(builder.Services, isDevelopment);

// ========== CONSTRUIR APLICAÇÃO ==========
var app = builder.Build();

// ========== MIDDLEWARE ==========

// Health Check melhorado
app.Map("/health", async context =>
{
    try
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var canConnect = await dbContext.Database.CanConnectAsync();

        // Testa Cloudinary se configurado
        bool cloudinaryWorking = false;
        if (!string.IsNullOrEmpty(cloudinarySettings.CloudName))
        {
            try
            {
                var cloudinaryService = scope.ServiceProvider.GetService<CloudinaryService>();
                cloudinaryWorking = cloudinaryService != null;
            }
            catch
            {
                cloudinaryWorking = false;
            }
        }

        var healthStatus = new
        {
            status = canConnect ? "healthy" : "unhealthy",
            database = canConnect ? "connected" : "disconnected",
            cloudinary = cloudinaryWorking ? "connected" : "not_configured",
            timestamp = DateTime.UtcNow,
            environment = app.Environment.EnvironmentName,
            isDevelopment = isDevelopment,
            isRender = isRender,
            services = new
            {
                jwt = !string.IsNullOrEmpty(jwtSettings.Key),
                mercadoPago = !string.IsNullOrEmpty(mercadoPagoToken),
                cloudinary = !string.IsNullOrEmpty(cloudinarySettings.CloudName),
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
            innerError = ex.InnerException?.Message,
            timestamp = DateTime.UtcNow,
            stackTrace = isDevelopment ? ex.StackTrace : null
        });
    }
});

// Rota de teste de configurações
if (isDevelopment)
{
    app.Map("/config-test", () =>
    {
        var config = new
        {
            environment = builder.Environment.EnvironmentName,
            connectionString = connectionString?.Replace("Password=", "Password=***"),
            jwt = new
            {
                hasKey = !string.IsNullOrEmpty(jwtSettings.Key),
                keyLength = jwtSettings.Key?.Length ?? 0,
                issuer = jwtSettings.Issuer,
                audience = jwtSettings.Audience
            },
            cloudinary = new
            {
                hasConfig = !string.IsNullOrEmpty(cloudinarySettings.CloudName),
                cloudName = cloudinarySettings.CloudName,
                hasApiKey = !string.IsNullOrEmpty(cloudinarySettings.ApiKey),
                hasApiSecret = !string.IsNullOrEmpty(cloudinarySettings.ApiSecret)
            }
        };

        return Results.Json(config);
    });
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Clube Mecânico API v1");
        c.RoutePrefix = "swagger";
        c.DisplayRequestDuration();
        c.EnableTryItOutByDefault();
        c.DefaultModelExpandDepth(2);
    });
}
else
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Clube Mecânico API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseSession();
app.UseRouting();
app.UseCors("AllowSpecificOrigins");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger"));

// ========== LOG INICIAL ==========
Console.WriteLine("\n?? Clube Mecânico API INICIADA");
Console.WriteLine("=".PadRight(50, '='));
Console.WriteLine($"?? Ambiente: {app.Environment.EnvironmentName}");
Console.WriteLine($"?? Modo: {(isDevelopment ? "Desenvolvimento" : "Produção")}");
Console.WriteLine($"?? Render: {(isRender ? "Sim" : "Não")}");
Console.WriteLine($"?? Database: {(!string.IsNullOrEmpty(connectionString) ? "Configurado" : "Não configurado")}");
Console.WriteLine($"?? Cloudinary: {(!string.IsNullOrEmpty(cloudinarySettings.CloudName) ? "Configurado" : "Não configurado")}");
Console.WriteLine($"?? URLs:");
Console.WriteLine($"??   Local: https://localhost:5001");
Console.WriteLine($"??   Local: http://localhost:5000");
if (isRender)
{
    var serviceName = Environment.GetEnvironmentVariable("RENDER_SERVICE_NAME") ?? "seu-app";
    Console.WriteLine($"??   Render: https://{serviceName}.onrender.com");
}
Console.WriteLine($"?? Health Check: /health");
if (isDevelopment)
{
    Console.WriteLine($"?? Config Test: /config-test");
}
Console.WriteLine("=".PadRight(50, '='));

app.Run();

// ========== MÉTODOS AUXILIARES ==========

string GetConnectionString(IConfiguration configuration, bool isRender)
{
    // Primeiro, tenta pegar da configuração padrão
    var connectionString = configuration.GetConnectionString("DefaultConnection");

    if (isRender)
    {
        Console.WriteLine("? Executando no Render - Processando variáveis de ambiente...");

        // Opção 1: Tenta obter diretamente da variável de ambiente
        var connectionStringFromEnv = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrEmpty(connectionStringFromEnv))
        {
            Console.WriteLine("? Usando ConnectionStrings__DefaultConnection do Render");
            return connectionStringFromEnv;
        }

        // Opção 2: Tenta obter como variável separada
        var dbHost = Environment.GetEnvironmentVariable("DB_HOST");
        var dbPort = Environment.GetEnvironmentVariable("DB_PORT");
        var dbName = Environment.GetEnvironmentVariable("DB_NAME");
        var dbUser = Environment.GetEnvironmentVariable("DB_USER");
        var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");

        if (!string.IsNullOrEmpty(dbHost))
        {
            Console.WriteLine("? Usando variáveis de ambiente separadas para conexão");
            return $"Host={dbHost};" +
                   $"Port={dbPort ?? "5432"};" +
                   $"Database={dbName};" +
                   $"Username={dbUser};" +
                   $"Password={dbPassword};" +
                   $"SSL Mode=Require;" +
                   $"Trust Server Certificate=true;" +
                   $"Pooling=true;" +
                   $"Timeout=30;";
        }

        // Opção 3: Tenta converter DATABASE_URL (formato PostgreSQL)
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrEmpty(databaseUrl))
        {
            try
            {
                Console.WriteLine($"? DATABASE_URL encontrada: {databaseUrl.Substring(0, Math.Min(databaseUrl.Length, 50))}...");

                // Remove o protocolo postgres:// se existir
                string connectionStringFromUrl;

                if (databaseUrl.StartsWith("postgres://"))
                {
                    // Formato: postgres://username:password@host:port/database
                    var uriString = databaseUrl.Replace("postgres://", "postgresql://");
                    var uri = new Uri(uriString);

                    var userInfo = uri.UserInfo.Split(':');
                    var username = userInfo[0];
                    var password = userInfo.Length > 1 ? userInfo[1] : "";

                    connectionStringFromUrl = $"Host={uri.Host};" +
                                            $"Port={uri.Port};" +
                                            $"Database={uri.AbsolutePath.Trim('/')};" +
                                            $"Username={username};" +
                                            $"Password={password};" +
                                            $"SSL Mode=Require;" +
                                            $"Trust Server Certificate=true;" +
                                            $"Pooling=true;" +
                                            $"Timeout=30;";
                }
                else if (databaseUrl.StartsWith("postgresql://"))
                {
                    // Já está no formato correto
                    var uri = new Uri(databaseUrl);

                    var userInfo = uri.UserInfo.Split(':');
                    var username = userInfo[0];
                    var password = userInfo.Length > 1 ? userInfo[1] : "";

                    connectionStringFromUrl = $"Host={uri.Host};" +
                                            $"Port={uri.Port};" +
                                            $"Database={uri.AbsolutePath.Trim('/')};" +
                                            $"Username={username};" +
                                            $"Password={password};" +
                                            $"SSL Mode=Require;" +
                                            $"Trust Server Certificate=true;" +
                                            $"Pooling=true;" +
                                            $"Timeout=30;";
                }
                else
                {
                    // Tenta como string de conexão direta
                    Console.WriteLine("? Usando DATABASE_URL como string de conexão direta");
                    connectionStringFromUrl = databaseUrl;
                }

                Console.WriteLine("? Conexão do banco gerada com sucesso a partir do DATABASE_URL");
                return connectionStringFromUrl;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? ERRO ao processar DATABASE_URL: {ex.Message}");
                Console.WriteLine($"? StackTrace: {ex.StackTrace}");

                // Log detalhado para debugging
                Console.WriteLine($"? DATABASE_URL value: {databaseUrl}");

                // Não throw - vamos tentar usar a connection string padrão
            }
        }

        Console.WriteLine("? Nenhuma configuração específica do Render encontrada. Usando configuração padrão.");
    }

    return connectionString;
}

JwtSettings GetJwtSettings(IConfiguration configuration)
{
    return new JwtSettings
    {
        Key = configuration["JwtSettings:Key"],
        Issuer = configuration["JwtSettings:Issuer"],
        Audience = configuration["JwtSettings:Audience"],
        ExpirationDays = int.TryParse(configuration["JwtSettings:ExpirationDays"], out var days) ? days : 7
    };
}

CloudinarySettings GetCloudinarySettings(IConfiguration configuration)
{
    return new CloudinarySettings
    {
        CloudName = configuration["Cloudinary:CloudName"],
        ApiKey = configuration["Cloudinary:ApiKey"],
        ApiSecret = configuration["Cloudinary:ApiSecret"]
    };
}

void LogConfigurationSafe(string connectionString, JwtSettings jwtSettings,
    CloudinarySettings cloudinarySettings, string mercadoPagoToken,
    string pixChavePix, bool isRender)
{
    Console.WriteLine("\n?? CONFIGURAÇÕES CARREGADAS:");
    Console.WriteLine("-".PadRight(40, '-'));

    // Database (log seguro)
    if (!string.IsNullOrEmpty(connectionString))
    {
        var safeConn = connectionString.Contains("Password=")
            ? connectionString.Substring(0, connectionString.IndexOf("Password=")) + "Password=***"
            : connectionString;
        Console.WriteLine($"? Database: {(isRender ? "Render" : "Local")}");
        Console.WriteLine($"  Connection: {safeConn}");
    }

    // JWT
    Console.WriteLine($"? JWT: {(!string.IsNullOrEmpty(jwtSettings.Key) ? "Configurado" : "Não configurado")}");
    if (!string.IsNullOrEmpty(jwtSettings.Key))
    {
        Console.WriteLine($"  Issuer: {jwtSettings.Issuer}");
        Console.WriteLine($"  Audience: {jwtSettings.Audience}");
        Console.WriteLine($"  Key Length: {jwtSettings.Key.Length} caracteres");
    }

    // Cloudinary
    Console.WriteLine($"? Cloudinary: {(!string.IsNullOrEmpty(cloudinarySettings.CloudName) ? "Configurado" : "Não configurado")}");
    if (!string.IsNullOrEmpty(cloudinarySettings.CloudName))
    {
        Console.WriteLine($"  Cloud Name: {cloudinarySettings.CloudName}");
        Console.WriteLine($"  API Key: {(string.IsNullOrEmpty(cloudinarySettings.ApiKey) ? "Não" : "Sim")}");
        Console.WriteLine($"  API Secret: {(string.IsNullOrEmpty(cloudinarySettings.ApiSecret) ? "Não" : "Sim")}");
    }

    // Mercado Pago
    Console.WriteLine($"? Mercado Pago: {(!string.IsNullOrEmpty(mercadoPagoToken) ? "Configurado" : "Não configurado")}");

    // PIX
    Console.WriteLine($"? PIX: {(!string.IsNullOrEmpty(pixChavePix) ? "Configurado" : "Não configurado")}");

    Console.WriteLine("-".PadRight(40, '-'));
}

void ValidateRequiredSettings(JwtSettings jwtSettings, string connectionString)
{
    var errors = new List<string>();

    if (string.IsNullOrEmpty(jwtSettings.Key))
        errors.Add("Chave JWT não configurada. Configure JwtSettings:Key ou JwtSettings__Key");

    if (jwtSettings.Key != null && jwtSettings.Key.Length < 32)
        Console.WriteLine($"?? AVISO: Chave JWT tem apenas {jwtSettings.Key.Length} caracteres. Mínimo recomendado: 32");

    if (string.IsNullOrEmpty(connectionString))
        errors.Add("String de conexão com o banco não configurada. Configure ConnectionStrings:DefaultConnection ou DATABASE_URL");

    if (errors.Any())
        throw new Exception($"Erros de configuração:\n{string.Join("\n", errors.Select(e => $"  • {e}"))}");
}

void ConfigureJwtAuthentication(IServiceCollection services, JwtSettings jwtSettings)
{
    var tokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
        ValidateIssuer = !string.IsNullOrEmpty(jwtSettings.Issuer),
        ValidateAudience = !string.IsNullOrEmpty(jwtSettings.Audience),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        RequireExpirationTime = true
    };

    if (!string.IsNullOrEmpty(jwtSettings.Issuer))
        tokenValidationParameters.ValidIssuer = jwtSettings.Issuer;

    if (!string.IsNullOrEmpty(jwtSettings.Audience))
        tokenValidationParameters.ValidAudience = jwtSettings.Audience;

    services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = tokenValidationParameters;
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"?? Autenticação falhou: {context.Exception.Message}");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Console.WriteLine("?? Token JWT validado com sucesso");
                return Task.CompletedTask;
            }
        };
    });
}

void ConfigureSwagger(IServiceCollection services)
{
    services.AddEndpointsApiExplorer();
    services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Clube Mecânico API",
            Version = "v1",
            Description = "API para gerenciamento do Clube Mecânico",
            Contact = new OpenApiContact
            {
                Name = "Suporte",
                Email = "suporte@clubemecanico.com"
            }
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

        // Ordena os endpoints por ordem alfabética
        c.OrderActionsBy(apiDesc => $"{apiDesc.ActionDescriptor.RouteValues["controller"]}_{apiDesc.HttpMethod}");

        // Inclui comentários XML se existirem
        var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            c.IncludeXmlComments(xmlPath);
        }
    });
}

void ConfigureCors(IServiceCollection services, bool isDevelopment)
{
    services.AddCors(options =>
    {
        options.AddPolicy("AllowSpecificOrigins", policy =>
        {
            if (isDevelopment)
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            }
            else
            {
                policy.WithOrigins(
                        "https://www.clubemecanico.com.br/", "https://www.clubemecanico.com.br/",
                        "https://localhost:5173"
                    )
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            }
        });
    });
}

// ========== CLASSES AUXILIARES ==========
public class JwtSettings
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpirationDays { get; set; } = 7;
}

public class CloudinarySettings
{
    public string CloudName { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
}

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