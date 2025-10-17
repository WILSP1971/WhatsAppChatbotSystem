using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

public class CloudinaryService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryService(IConfiguration configuration)
    {
        var account = new Account(
            configuration["Cloudinary:CloudName"],
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
            
            // ✅ Obtener extensión del archivo
            var extension = Path.GetExtension(fileName).ToLower();
            
            // ✅ Generar un nombre único CON extensión
            var uniqueName = $"{Guid.NewGuid()}{extension}";
            var publicId = $"whatsapp-docs/{uniqueName}";
            
            // ✅ CORRECTO: No usar ResourceType en RawUploadParams
            var uploadParams = new RawUploadParams
            {
                File = new FileDescription(fileName, stream),
                PublicId = publicId,
                Overwrite = true
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);
            
            if (uploadResult.StatusCode == System.Net.HttpStatusCode.OK)
            {
                // ✅ La URL ya incluirá la extensión porque la pusimos en PublicId
                var urlWithExtension = uploadResult.SecureUrl.ToString();
                
                Console.WriteLine($"✅ Documento subido: {urlWithExtension}");
                Console.WriteLine($"   📎 Tipo: {uploadResult.Format}");
                Console.WriteLine($"   📏 Tamaño: {uploadResult.Bytes} bytes");
                
                return urlWithExtension;
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



