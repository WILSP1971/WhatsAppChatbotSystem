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
            
            var uploadParams = new RawUploadParams
            {
                File = new FileDescription(fileName, stream),
                PublicId = $"whatsapp-docs/{Guid.NewGuid()}",
                Overwrite = true
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);
            
            if (uploadResult.StatusCode == System.Net.HttpStatusCode.OK)
            {
                Console.WriteLine($"✅ Documento subido: {uploadResult.SecureUrl}");
                return uploadResult.SecureUrl.ToString();
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error subiendo documento: {ex.Message}");
            return null;
        }
    }
}

