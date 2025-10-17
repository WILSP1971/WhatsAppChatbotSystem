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

        // Validar que sea imagen
        var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif" };
        if (!allowedTypes.Contains(file.ContentType.ToLower()))
            return BadRequest(new { success = false, message = "Solo se permiten imágenes (JPG, PNG, GIF)" });

        // Validar tamaño (máximo 5MB)
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

    // [HttpPost("document")]
    // public async Task<IActionResult> UploadDocument(IFormFile file)
    // {
    //     if (file == null || file.Length == 0)
    //         return BadRequest(new { success = false, message = "No se recibió ningún archivo" });

    //     // Validar que sea documento
    //     var allowedTypes = new[] { "application/pdf", "application/msword", 
    //         "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    //         "text/plain" };
    //     if (!allowedTypes.Contains(file.ContentType.ToLower()))
    //         return BadRequest(new { success = false, message = "Solo se permiten documentos (PDF, DOC, DOCX, TXT)" });

    //     // Validar tamaño (máximo 10MB)
    //     if (file.Length > 10 * 1024 * 1024)
    //         return BadRequest(new { success = false, message = "El documento no debe superar 10MB" });

    //     try
    //     {
    //         using var memoryStream = new MemoryStream();
    //         await file.CopyToAsync(memoryStream);
    //         var fileBytes = memoryStream.ToArray();

    //         var url = await _cloudinaryService.UploadDocumentAsync(fileBytes, file.FileName);

    //         if (url != null)
    //         {
    //             return Ok(new { success = true, url = url, filename = file.FileName });
    //         }

    //         return StatusCode(500, new { success = false, message = "Error al subir el documento" });
    //     }
    //     catch (Exception ex)
    //     {
    //         Console.WriteLine($"❌ Error: {ex.Message}");
    //         return StatusCode(500, new { success = false, message = "Error interno del servidor" });
    //     }
    // }

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

            // ✅ Intentar Cloudinary primero
            var url = await _cloudinaryService.UploadDocumentAsync(fileBytes, file.FileName);

            // ⚠️ Si Cloudinary falla, usar file.io como respaldo
            if (url == null)
            {
                Console.WriteLine("⚠️ Cloudinary falló, usando file.io...");
                var fileIOService = new FileIOService();
                url = await fileIOService.UploadDocumentAsync(fileBytes, file.FileName);
            }

            if (url != null)
            {
                Console.WriteLine($"✅ URL final del documento: {url}");
                return Ok(new { success = true, url = url, filename = file.FileName });
            }

            return StatusCode(500, new { success = false, message = "Error al subir el documento" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            return StatusCode(500, new { success = false, message = "Error interno del servidor" });
        }
    }

}
