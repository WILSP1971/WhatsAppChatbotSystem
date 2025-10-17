public class JitsiService
{
    private readonly IConfiguration _configuration;
    private readonly string _jitsiBaseUrl;

    public JitsiService(IConfiguration configuration)
    {
        _configuration = configuration;
        _jitsiBaseUrl = _configuration["Jitsi:BaseUrl"] ?? "https://meet.jit.si";
    }

    public string GenerateVideoCallLink(string phoneNumber, string? customerName = null)
    {
        // ✅ Crear nombre de sala único y corto
        var roomSlug = $"whatsapp-{Guid.NewGuid().ToString().Substring(0, 8)}";
        
        // ✅ Nombre a mostrar (usar nombre del cliente o últimos 4 dígitos del teléfono)
        var displayName = !string.IsNullOrEmpty(customerName) 
            ? customerName 
            : $"Cliente {phoneNumber.Substring(Math.Max(0, phoneNumber.Length - 4))}";
        
        // ✅ Título de la reunión
        var subject = "Consulta de Salud";
        
        // ✅ Construir URL con parámetros
        var baseUrl = _jitsiBaseUrl.TrimEnd('/');
        var encodedSubject = Uri.EscapeDataString(subject);
        var encodedDisplayName = Uri.EscapeDataString(displayName);
        
        // ✅ Configurar Jitsi con prejoin habilitado
        var fragments = new List<string>
        {
            $"config.subject={encodedSubject}",
            "config.prejoinConfig.enabled=true",
            $"userInfo.displayName={encodedDisplayName}"
        };
        
        var fragmentString = string.Join("&", fragments);
        var fullUrl = $"{baseUrl}/{roomSlug}#{fragmentString}";
        
        Console.WriteLine($"📹 Videollamada generada:");
        Console.WriteLine($"   🔗 URL: {fullUrl}");
        Console.WriteLine($"   👤 Paciente: {displayName}");
        Console.WriteLine($"   🏠 Sala: {roomSlug}");
        
        return fullUrl;
    }
}


