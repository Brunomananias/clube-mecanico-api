using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class CloudinaryService
{
    private readonly Cloudinary _cloudinary;
    private readonly ILogger<CloudinaryService> _logger;

    public CloudinaryService(IConfiguration configuration, ILogger<CloudinaryService> logger)
    {
        _logger = logger;

        var cloudName = configuration["Cloudinary:CloudName"];
        var apiKey = configuration["Cloudinary:ApiKey"];
        var apiSecret = configuration["Cloudinary:ApiSecret"];

        if (string.IsNullOrEmpty(cloudName) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
        {
            throw new InvalidOperationException("Credenciais do Cloudinary não configuradas");
        }

        var account = new Account(cloudName, apiKey, apiSecret);
        _cloudinary = new Cloudinary(account);
    }

    public async Task<PdfUploadResult> UploadPdfAsync(IFormFile file, int cursoId)
    {
        try
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("Arquivo inválido");

            if (!file.FileName.ToLower().EndsWith(".pdf"))
                throw new ArgumentException("Apenas arquivos PDF são permitidos");

            if (file.Length > 10 * 1024 * 1024)
                throw new ArgumentException("O arquivo não pode exceder 10MB");

            _logger.LogInformation("Upload do PDF: {FileName} ({Size} bytes)",
                file.FileName, file.Length);

            using var stream = file.OpenReadStream();

            var uploadParams = new RawUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = $"cursos/{cursoId}/pdfs",
                PublicId = $"pdf_{Guid.NewGuid()}_{DateTime.Now:yyyyMMddHHmmss}",
                Overwrite = false,
                Tags = "curso_pdf,complementar"
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
                throw new Exception($"Erro no Cloudinary: {uploadResult.Error.Message}");

            // CORREÇÃO: Crie um objeto próprio para retorno
            return new PdfUploadResult
            {
                Url = uploadResult.SecureUrl?.ToString() ?? uploadResult.Url?.ToString(),
                PublicId = uploadResult.PublicId,
                Size = uploadResult.Bytes,
                OriginalFilename = uploadResult.OriginalFilename ?? file.FileName
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no upload do PDF");
            throw;
        }
    }

    public async Task<bool> DeletePdfAsync(string publicId)
    {
        try
        {
            var deleteParams = new DeletionParams(publicId)
            {
                ResourceType = ResourceType.Raw
            };

            var result = await _cloudinary.DestroyAsync(deleteParams);
            return result.Result == "ok";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao deletar PDF do Cloudinary");
            return false;
        }
    }
}

// Classe para retorno padronizado
public class PdfUploadResult
{
    public string Url { get; set; }
    public string PublicId { get; set; }
    public long Size { get; set; }
    public string OriginalFilename { get; set; }
}