using Microsoft.AspNetCore.Mvc;

namespace WhatsAppChatbotSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadController : ControllerBase
{
    private readonly CloudinaryService _cloudinaryService;

    public UploadController(CloudinaryService cloudinaryService)
    {
        _cloudinaryService = cloudinaryService;
    }

    [HttpPost("image")]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { success = false, message = "No se recibió ningún archivo" });


        var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif" };
        if (!allowedTypes.Contains(file.ContentType.ToLower()))
            return BadRequest(new { success = false, message = "Solo se permiten imágenes (JPG, PNG, GIF)" });


        if (file.Length > 5 * 1024 * 1024)
            return BadRequest(new { success = false, message = "La imagen no debe superar 5MB" });

        try
        {
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            var fileBytes = memoryStream.ToArray();

            var url = await _cloudinaryService.UploadImageAsync(fileBytes, file.FileName);

            if (url != null)
            {
                return Ok(new { success = true, url = url });
            }

            return StatusCode(500, new { success = false, message = "Error al subir la imagen" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            return StatusCode(500, new { success = false, message = "Error interno del servidor" });
        }
    }
    

    [HttpPost("document")]
    public async Task<IActionResult> UploadDocument(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { success = false, message = "No se recibió ningún archivo" });

        var allowedTypes = new[] { "application/pdf", "application/msword", 
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "text/plain" };
        if (!allowedTypes.Contains(file.ContentType.ToLower()))
            return BadRequest(new { success = false, message = "Solo se permiten documentos (PDF, DOC, DOCX, TXT)" });

        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(new { success = false, message = "El documento no debe superar 10MB" });

        try
        {
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            var fileBytes = memoryStream.ToArray();

            Console.WriteLine($"📄 Iniciando subida de: {file.FileName}");
            Console.WriteLine($"   📏 Tamaño: {file.Length} bytes");
            Console.WriteLine($"   📎 Tipo: {file.ContentType}");

            // ✅ Usar TmpFiles como principal (más confiable para WhatsApp)
            var tmpFilesService = new TmpFilesService();
            var url = await tmpFilesService.UploadDocumentAsync(fileBytes, file.FileName);

            // ⚠️ Si TmpFiles falla, intentar Cloudinary como respaldo
            if (url == null)
            {
                Console.WriteLine("⚠️ TmpFiles falló, intentando Cloudinary...");
                url = await _cloudinaryService.UploadDocumentAsync(fileBytes, file.FileName);
            }

            // ⚠️ Si Cloudinary también falla, intentar file.io
            if (url == null)
            {
                Console.WriteLine("⚠️ Cloudinary falló, intentando file.io...");
                var fileIOService = new FileIOService();
                url = await fileIOService.UploadDocumentAsync(fileBytes, file.FileName);
            }

            if (url != null)
            {
                Console.WriteLine($"✅ URL final del documento: {url}");
                
                // ✅ Verificar que la URL sea accesible
                try
                {
                    using var httpClient = new HttpClient();
                    httpClient.Timeout = TimeSpan.FromSeconds(5);
                    var testResponse = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    Console.WriteLine($"   🔍 Verificación de URL: {testResponse.StatusCode}");
                    Console.WriteLine($"   📎 Content-Type: {testResponse.Content.Headers.ContentType}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ⚠️ No se pudo verificar URL: {ex.Message}");
                }
                
                return Ok(new { success = true, url = url, filename = file.FileName });
            }

            return StatusCode(500, new { success = false, message = "Error al subir el documento en todos los servicios" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error general: {ex.Message}");
            Console.WriteLine($"   Stack: {ex.StackTrace}");
            return StatusCode(500, new { success = false, message = "Error interno del servidor" });
        }
    }

}

