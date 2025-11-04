using System.Net.Http.Headers;

public class TmpFilesService
{
    private readonly HttpClient _httpClient;

    public TmpFilesService()
    {
        _httpClient = new HttpClient();
    }

    public async Task<string?> UploadDocumentAsync(byte[] fileBytes, string fileName)
    {
        try
        {
            var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(fileBytes);
            
            // ✅ Configurar el tipo MIME correcto
            var mimeType = GetMimeType(fileName);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mimeType);
            
            content.Add(fileContent, "file", fileName);

            Console.WriteLine($"📤 Subiendo {fileName} a tmpfiles.org...");
            Console.WriteLine($"   📏 Tamaño: {fileBytes.Length} bytes");
            Console.WriteLine($"   📎 Tipo MIME: {mimeType}");

            var response = await _httpClient.PostAsync("https://tmpfiles.org/api/v1/upload", content);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"   📦 Respuesta: {json}");
                
                var result = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
                
                if (result.TryGetProperty("data", out var data) && 
                    data.TryGetProperty("url", out var urlElement))
                {
                    var url = urlElement.GetString();
                    
                    // ⚠️ tmpfiles.org devuelve URL de vista HTML, necesitamos URL directa
                    // Cambiar: https://tmpfiles.org/123/file.pdf
                    // Por: https://tmpfiles.org/dl/123/file.pdf
                    if (url != null && url.Contains("tmpfiles.org/"))
                    {
                        var directUrl = url.Replace("tmpfiles.org/", "tmpfiles.org/dl/");
                        
                        Console.WriteLine($"   ✅ URL original: {url}");
                        Console.WriteLine($"   ✅ URL directa: {directUrl}");
                        
                        return directUrl;
                    }
                    
                    return url;
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"   ❌ Error HTTP: {response.StatusCode}");
                Console.WriteLine($"   📄 Respuesta: {errorContent}");
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Excepción en TmpFilesService: {ex.Message}");
            Console.WriteLine($"   Stack: {ex.StackTrace}");
            return null;
        }
    }

    private string GetMimeType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLower();
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".txt" => "text/plain",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };
    }
}
