using System.Net.Http.Headers;

public class FileIOService
{
    private readonly HttpClient _httpClient;

    public FileIOService()
    {
        _httpClient = new HttpClient();
    }

    public async Task<string?> UploadDocumentAsync(byte[] fileBytes, string fileName)
    {
        try
        {
            var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(GetMimeType(fileName));
            content.Add(fileContent, "file", fileName);

            // file.io permite 1 descarga por defecto, para WhatsApp necesitamos más
            // pero es gratis y rápido
            var response = await _httpClient.PostAsync("https://file.io/?expires=1d", content);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
                
                if (result.TryGetProperty("link", out var link))
                {
                    var url = link.GetString();
                    Console.WriteLine($"✅ Documento subido a file.io: {url}");
                    return url;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
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
            _ => "application/octet-stream"
        };
    }
}
