using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ClubeMecanico.Application.Interfaces;
using ClubeMecanico_API.Domain.Entities;
using ClubeMecanico_API.Domain.Enums;
using ClubeMecanico_API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthService(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    // Método síncrono (usado pelo controller atual)
    public Usuario? AuthenticateUser(string email, string password)
    {
        var user = _context.Usuarios
            .FirstOrDefault(u => u.Email == email);

        if (user == null || !user.Ativo)
            return null;

        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(password, user.SenhaHash);

        if (isPasswordValid)
        {
            user.RegistrarLogin();
            _context.SaveChanges();
            return user;
        }

        return null;
    }

    // Método assíncrono da interface
    public async Task<Usuario?> AutenticarAsync(string email, string senha)
    {
        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Email == email);

        if (usuario == null || !usuario.Ativo)
            return null;

        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(senha, usuario.SenhaHash);

        if (isPasswordValid)
        {
            usuario.RegistrarLogin();
            await _context.SaveChangesAsync();
            return usuario;
        }

        return null;
    }

    // Método de registro da interface
    public async Task<Usuario?> RegistrarAsync(string email, string senha, string nomeCompleto,
        string? cpf, string? telefone, DateTime? dataNascimento, int tipo)
    {
        // Validações
        if (await _context.Usuarios.AnyAsync(u => u.Email == email))
            throw new ArgumentException("Email já cadastrado");

        if (!string.IsNullOrEmpty(cpf))
        {
            if (await _context.Usuarios.AnyAsync(u => u.CPF == cpf))
                throw new ArgumentException("CPF já cadastrado");

            if (!ValidarCPF(cpf))
                throw new ArgumentException("CPF inválido");
        }

        // Hash seguro da senha
        var senhaHash = HashPassword(senha);

        // Criação do usuário
        var usuario = new Usuario(email, senhaHash, nomeCompleto)
        {
            CPF = cpf,
            Telefone = telefone,
            Data_Nascimento = dataNascimento,
            Tipo = tipo
        };

        await _context.Usuarios.AddAsync(usuario);
        await _context.SaveChangesAsync();

        return usuario;
    }

    public string GenerateJwtToken(long userId, string email)
    {
        // Use a configuração correta do seu appsettings.json
        var key = Encoding.ASCII.GetBytes(_configuration["JwtSettings:Key"]
            ?? throw new InvalidOperationException("JWT Key não configurada"));

        var expiresHours = _configuration.GetValue<int>("Jwt:ExpireHours", 24);

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Email, email)
                // Adicione outras claims conforme necessário
            }),
            Expires = DateTime.UtcNow.AddHours(expiresHours),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature
            )
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    private bool VerifyPassword(string password, string storedHash)
    {
        return BCrypt.Net.BCrypt.Verify(password, storedHash);
    }

    private bool ValidarCPF(string cpf)
    {
        // Remove caracteres não numéricos
        cpf = new string(cpf.Where(char.IsDigit).ToArray());

        if (cpf.Length != 11)
            return false;

        // Verifica se todos os dígitos são iguais
        if (cpf.Distinct().Count() == 1)
            return false;

        // Validação do primeiro dígito verificador
        int soma = 0;
        for (int i = 0; i < 9; i++)
            soma += int.Parse(cpf[i].ToString()) * (10 - i);

        int resto = soma % 11;
        int digito1 = resto < 2 ? 0 : 11 - resto;

        if (digito1 != int.Parse(cpf[9].ToString()))
            return false;

        // Validação do segundo dígito verificador
        soma = 0;
        for (int i = 0; i < 10; i++)
            soma += int.Parse(cpf[i].ToString()) * (11 - i);

        resto = soma % 11;
        int digito2 = resto < 2 ? 0 : 11 - resto;

        return digito2 == int.Parse(cpf[10].ToString());
    }

    // Método opcional: validar token e obter usuário
    public long? GetUsuarioIdFromToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]!);

            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            var userId = long.Parse(jwtToken.Claims
                .First(x => x.Type == ClaimTypes.NameIdentifier).Value);

            return userId;
        }
        catch
        {
            return null;
        }
    }
}