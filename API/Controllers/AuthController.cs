using ClubeMecanico_API.API.DTOs;
using ClubeMecanico.Application.Interfaces; // ADICIONE ESTE USING
using ClubeMecanico_API.Domain.Entities;
using ClubeMecanico_API.Domain.Enums;
using ClubeMecanico_API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    // MUDE AQUI: AuthService → IAuthService
    private readonly IAuthService _authService;
    private readonly AppDbContext _context;

    // MUDE AQUI TAMBÉM: AuthService → IAuthService
    public AuthController(IAuthService authService, AppDbContext context)
    {
        _authService = authService; // ← IAuthService
        _context = context;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDTO request)
    {
        // Use o método assíncrono (melhor prática)
        var user = await _authService.AutenticarAsync(request.Email, request.Senha);

        if (user == null)
            return Unauthorized(new { message = "Credenciais inválidas" });

        var token = _authService.GenerateJwtToken(user.Id, user.Email, user.Tipo);

        return Ok(new
        {
            Token = token,
            Usuario = new // Adicione informações do usuário na resposta
            {
                user.Id,
                user.Nome_Completo,
                user.Email,
                user.CPF,
                user.Tipo
            }
        });
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegistrarUsuarioDTO request)
    {
        try
        {
            // Use o serviço atualizado para incluir endereço
            var usuario = await _authService.RegistrarAsync(
                request.Email,
                request.Senha,
                request.Nome_Completo,
                request.CPF,
                request.Telefone,
                request.Data_Nascimento,
                request.Tipo,
                request.Endereco.CEP,
                request.Endereco.Logradouro,
                request.Endereco.Numero,
                request.Endereco.Complemento,
                request.Endereco.Bairro,
                request.Endereco.Cidade,
                request.Endereco.Estado,
                request.Endereco.Tipo);

            var token = _authService.GenerateJwtToken(usuario!.Id, usuario.Email, usuario.Tipo);

            return Ok(new
            {
                message = "Usuário registrado com sucesso!",
                token,
                usuario = new
                {
                    usuario.Id,
                    usuario.Nome_Completo,
                    usuario.Email,
                    usuario.CPF,
                    usuario.Tipo,
                    Endereco = usuario.Enderecos?.FirstOrDefault() // Retorna o endereço principal
                }
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Erro interno no servidor", detalhes = ex.Message });
        }
    }
}