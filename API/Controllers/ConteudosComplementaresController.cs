using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CloudinaryDotNet.Actions;
using ClubeMecanico_API.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using ClubeMecanico_API.Infrastructure.Data;

[ApiController]
[Route("api/[controller]")]
public class ConteudosComplementaresController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly CloudinaryService _cloudinary;
    private readonly ILogger<ConteudosComplementaresController> _logger;

    public ConteudosComplementaresController(
        AppDbContext context,
        CloudinaryService cloudinary,
        ILogger<ConteudosComplementaresController> logger)
    {
        _context = context;
        _cloudinary = cloudinary;
        _logger = logger;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(10_485_760)] // 10MB
    public async Task<IActionResult> UploadPdf([FromForm] IFormFile file, [FromForm] int cursoId)
    {
        try
        {
            _logger.LogInformation("Iniciando upload de PDF. CursoId: {CursoId}", cursoId);

            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "Nenhum arquivo enviado" });

            // Upload para Cloudinary
            var uploadResult = await _cloudinary.UploadPdfAsync(file, cursoId);

            if (uploadResult == null)
                return BadRequest(new { success = false, message = "Erro no upload do PDF" });

            // CORREÇÃO: Use uploadResult.OriginalFilename (com F maiúsculo)
            return Ok(new
            {
                success = true,
                url = uploadResult.Url,
                publicId = uploadResult.PublicId,
                tamanhoArquivo = uploadResult.Size,
                nomeArquivo = uploadResult.OriginalFilename,
                message = "PDF enviado com sucesso!"
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Erro de validação no upload");
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no upload do PDF");
            return StatusCode(500, new { success = false, message = "Erro interno no servidor" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CriarConteudoComplementarDto dto)
    {
        try
        {
            // Validar se curso existe
            var curso = await _context.Cursos.FindAsync(dto.CursoId);
            if (curso == null)
                return NotFound(new { success = false, message = "Curso não encontrado" });

            var conteudo = new ConteudoComplementar
            {
                CursoId = dto.CursoId,
                Titulo = dto.Titulo,
                Descricao = dto.Descricao,
                Tipo = dto.Tipo,
                Url = dto.Url,
                DataCriacao = DateTime.UtcNow
            };

            _context.ConteudosComplementares.Add(conteudo);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                id = conteudo.Id,
                titulo = conteudo.Titulo,
                url = conteudo.Url,
                message = "Material complementar adicionado com sucesso!"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar conteúdo complementar");
            return StatusCode(500, new { success = false, message = "Erro interno do servidor" });
        }
    }

    [HttpGet("curso/{cursoId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByCurso(int cursoId)
    {
        try
        {
            var conteudos = await _context.ConteudosComplementares
                .Where(cc => cc.CursoId == cursoId)
                .OrderByDescending(cc => cc.DataCriacao)
                .Select(cc => new
                {
                    cc.Id,
                    cc.Titulo,
                    cc.Descricao,
                    cc.Tipo,
                    cc.Url,
                    cc.DataCriacao,
                })
                .ToListAsync();

            return Ok(new { success = true, data = conteudos });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar conteúdos complementares");
            return StatusCode(500, new { success = false, message = "Erro interno do servidor" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var conteudo = await _context.ConteudosComplementares.FindAsync(id);
            if (conteudo == null)
                return NotFound(new { success = false, message = "Material não encontrado" });

            _context.ConteudosComplementares.Remove(conteudo);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Material removido com sucesso!" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao deletar conteúdo complementar");
            return StatusCode(500, new { success = false, message = "Erro interno do servidor" });
        }
    }
}

public class CriarConteudoComplementarDto
{
    public int CursoId { get; set; }
    public string Titulo { get; set; }
    public string Descricao { get; set; }
    public string Tipo { get; set; } = "pdf";
    public string Url { get; set; }
    public string PublicId { get; set; }
    public long? TamanhoArquivo { get; set; }
}