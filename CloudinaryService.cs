using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

public class CloudinaryService
{
    private readonly Cloudinary _cloudinary;
    private readonly string _cloudName;

    public CloudinaryService(IConfiguration configuration)
    {
        _cloudName = configuration["Cloudinary:CloudName"] ?? "";
        
        var account = new Account(
            _cloudName,
            configuration["Cloudinary:ApiKey"],
            configuration["Cloudinary:ApiSecret"]
        );
        
        _cloudinary = new Cloudinary(account);
    }

    public async Task<string?> UploadImageAsync(byte[] fileBytes, string fileName)
    {
        try
        {
            using var stream = new MemoryStream(fileBytes);
            
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(fileName, stream),
                PublicId = $"whatsapp/{Guid.NewGuid()}",
                Overwrite = true
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);
            
            if (uploadResult.StatusCode == System.Net.HttpStatusCode.OK)
            {
                Console.WriteLine($"✅ Imagen subida: {uploadResult.SecureUrl}");
                return uploadResult.SecureUrl.ToString();
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error subiendo imagen: {ex.Message}");
            return null;
        }
    }

    public async Task<string?> UploadDocumentAsync(byte[] fileBytes, string fileName)
    {
        try
        {
            using var stream = new MemoryStream(fileBytes);
            
            // ✅ Obtener extensión
            var extension = Path.GetExtension(fileName).ToLower();
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            
            // ✅ Sanitizar nombre de archivo (remover caracteres especiales)
            var sanitizedName = new string(fileNameWithoutExt
                .Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-')
                .ToArray());
            
            if (string.IsNullOrEmpty(sanitizedName))
            {
                sanitizedName = "document";
            }
            
            // ✅ Crear PublicId único pero con extensión
            var publicId = $"whatsapp-docs/{sanitizedName}_{Guid.NewGuid().ToString().Substring(0, 8)}{extension}";
            
            var uploadParams = new RawUploadParams
            {
                File = new FileDescription(fileName, stream),
                PublicId = publicId,
                Overwrite = true
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);
            
            if (uploadResult.StatusCode == System.Net.HttpStatusCode.OK)
            {
                // ✅ Construir URL con flags específicos para WhatsApp
                var baseUrl = uploadResult.SecureUrl.ToString();
                
                // ✅ IMPORTANTE: Agregar flag fl_attachment para forzar descarga
                var urlParts = baseUrl.Split(new[] { "/upload/" }, StringSplitOptions.None);
                string finalUrl;
                
                if (urlParts.Length == 2)
                {
                    // Insertar flags después de /upload/
                    finalUrl = $"{urlParts[0]}/upload/fl_attachment/{urlParts[1]}";
                }
                else
                {
                    finalUrl = baseUrl;
                }
                
                Console.WriteLine($"✅ Documento subido exitosamente");
                Console.WriteLine($"   🔗 URL original: {baseUrl}");
                Console.WriteLine($"   🔗 URL con flags: {finalUrl}");
                Console.WriteLine($"   📎 Nombre: {fileName}");
                Console.WriteLine($"   📏 Tamaño: {uploadResult.Bytes} bytes");
                
                return finalUrl;
            }

            Console.WriteLine($"❌ Upload falló: {uploadResult.StatusCode}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error subiendo documento: {ex.Message}");
            Console.WriteLine($"   Stack: {ex.StackTrace}");
            return null;
        }
    }
    
}





