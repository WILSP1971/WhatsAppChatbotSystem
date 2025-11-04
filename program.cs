using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

// Configurar puerto dinámico para Railway
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

Console.WriteLine($"🚀 Servidor configurado en puerto: {port}");

// Configurar servicios
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddHttpClient(); // Para llamadas HTTP a API
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

// Servicios personalizados
builder.Services.AddSingleton<ConversationManager>();
builder.Services.AddSingleton<WhatsAppService>();
builder.Services.AddSingleton<AIBotService>();
builder.Services.AddSingleton<ApiIntegrationService>();
builder.Services.AddSingleton<CloudinaryService>();
builder.Services.AddSingleton<JitsiService>();

var app = builder.Build();

// Configurar middleware
app.UseCors("AllowAll");
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/chatHub");

Console.WriteLine("✅ Aplicación iniciada correctamente");

app.Run();

// ============================================
// MODELOS DE DATOS
// ============================================

public class WhatsAppMessage
{
    public string From { get; set; } = "";
    public string Body { get; set; } = "";
    public string MessageId { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class Conversation
{
    public string ConversationId { get; set; } = Guid.NewGuid().ToString();
    public string PhoneNumber { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public List<Message> Messages { get; set; } = new();
    public ConversationStatus Status { get; set; } = ConversationStatus.Waiting;
    public string? AssignedOperator { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    public Dictionary<string, string> Context { get; set; } = new(); // Para mantener contexto del bot
}

public class Message
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
    public string Content { get; set; } = "";
    public MessageType Type { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Sender { get; set; } = "";
    public string? MediaUrl { get; set; } // Para imágenes/archivos
    public string? MediaType { get; set; } // image, document, etc.
}

public enum ConversationStatus
{
    Waiting,
    Active,
    BotHandling,
    Closed
}

public enum MessageType
{
    Customer,
    Operator,
    Bot,
    System
}

public class Operator
{
    public string OperatorId { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsAvailable { get; set; } = true;
    public List<string> ActiveConversations { get; set; } = new();
}

// ============================================
// MODELOS PARA MENSAJES INTERACTIVOS
// ============================================

public class ButtonOption
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
}

public class ListSection
{
    public string Title { get; set; } = "";
    public List<ListRow> Rows { get; set; } = new();
}

public class ListRow
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
}

// ============================================
// MODELOS DE API
// ============================================

public class Paciente
{
    public string NoCaso { get; set; } = "";
    public string NoIdentificacion { get; set; } = "";
    public string NombrePaciente { get; set; } = "";
    public string ApellidoPaciente { get; set; } = "";
    public string DatosPaciente { get; set; } = "";
}

public class NuevoPaciente
{
    public string Id { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Documento { get; set; } = "";
    public string Telefono { get; set; } = "";
    public string Email { get; set; } = "";
    public DateTime FechaNacimiento { get; set; }
}

public class Cita
{
    public string Fecha { get; set; } = "";
    public string Hora { get; set; } = "";
    public string CodServicio { get; set; } = "";
    public decimal consecutivo { get; set; }
    public string citaControl { get; set; } = "";
    public string Observacion { get; set; } = "";
    public string CedulaMedico { get; set; } = "";
    public string Medico { get; set; } = "";
    public string Paciente { get; set; } = "";
    public string NoIdentificacion { get; set; } = "";
}

public class NuevaCita
{
    public string Id { get; set; } = "";
    public string PacienteId { get; set; } = "";
    public string PacienteNombre { get; set; } = "";
    public DateTime FechaHora { get; set; }
    public string Especialidad { get; set; } = "";
    public string Doctor { get; set; } = "";
    public string Estado { get; set; } = "";
}
public class HistoriaClinica
{
    public string PacienteId { get; set; } = "";
    public string Documento { get; set; } = "";
    public string NombreCompleto { get; set; } = "";
    public DateTime FechaNacimiento { get; set; }
    public string Direccion { get; set; } = "";
    public string Telefono { get; set; } = "";
    public string Email { get; set; } = "";
    public string TipoSangre { get; set; } = "";
    public List<string> Alergias { get; set; } = new();
}

// ============================================
// SERVICIO DE INTEGRACIÓN CON API
// ============================================

public class ApiIntegrationService
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _baseUrl;
    private readonly string _codigoEmpresa;
    //private readonly string _apiKey;

    public ApiIntegrationService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _baseUrl = _configuration["ApiSettings:BaseUrl"] ?? "";
        _codigoEmpresa = _configuration["ApiSettings:CodigoEmpresa"] ?? "C30";

        //_apiKey = _configuration["ApiSettings:ApiKey"] ?? "";
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(_baseUrl);
        //client.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    public async Task<Paciente?> BuscarPacientePorDocumento(string documento)
    {
        try
        {
            //var client = CreateClient();
            //var response = await client.GetAsync($"/Pacientes/{documento}");

            using var client = new HttpClient();
            client.BaseAddress = new Uri(_baseUrl);

            string fullUrl = $"{_baseUrl}/Pacientes?CodigoEmp={_codigoEmpresa}&criterio={documento}";

            // ✅ LOGS DE DEPURACIÓN
            Console.WriteLine($"🔍 Buscando paciente...");
            Console.WriteLine($"   📍 Base URL: {_baseUrl}");
            Console.WriteLine($"   🏢 Código Empresa: {_codigoEmpresa}");
            Console.WriteLine($"   📄 Documento: {documento}");
            Console.WriteLine($"   🌐 URL completa: {_baseUrl}{fullUrl}");

            var response = await client.GetAsync(fullUrl);
            Console.WriteLine($"   📊 Status Code: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"   📦 JSON recibido: {json.Substring(0, Math.Min(200, json.Length))}...");
                
                // ⚠️ Configurar opciones para ignorar propiedades desconocidas
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };
                
                try
                {
                    // Intentar deserializar como lista primero
                    var pacientes = JsonSerializer.Deserialize<List<Paciente>>(json, options);
                    if (pacientes != null && pacientes.Count > 0)
                    {
                        Console.WriteLine($"   ✅ Paciente encontrado (lista): {pacientes[0].NombrePaciente}");
                        return pacientes[0];
                    }
                }
                catch (JsonException ex1)
                {
                    Console.WriteLine($"   ⚠️ No es una lista, intentando como objeto único: {ex1.Message}");
                    
                    try
                    {
                        // Si falla, intentar como objeto único
                        var paciente = JsonSerializer.Deserialize<Paciente>(json, options);
                        if (paciente != null)
                        {
                            Console.WriteLine($"   ✅ Paciente encontrado (objeto): {paciente.NombrePaciente}");
                            return paciente;
                        }
                    }
                    catch (JsonException ex2)
                    {
                        Console.WriteLine($"   ❌ Error deserializando objeto: {ex2.Message}");
                    }
                }
                
                Console.WriteLine($"   ⚠️ No se pudo deserializar el JSON");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"   ❌ Error en respuesta: {errorContent}");
            }
            
            return null;

        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error buscando paciente: {ex.Message}");
            return null;
        }
    }

    public async Task<List<Cita>> ObtenerCitasPorTelefono(string documento)
    {
        try
        {
            using var client = new HttpClient();
            //client.BaseAddress = new Uri(_baseUrl);

            string fullUrl = $"{_baseUrl}/CitasProgramadas?CodigoEmp={_codigoEmpresa}&criterio={documento}";

            var response = await client.GetAsync(fullUrl);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"   📦 JSON: {json.Substring(0, Math.Min(300, json.Length))}");
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };
                
                var citas = JsonSerializer.Deserialize<List<Cita>>(json, options) ?? new List<Cita>();
                Console.WriteLine($"   ✅ {citas.Count} citas encontradas");
                return citas;
            }
            
            return new List<Cita>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error obteniendo citas: {ex.Message}");
            return new List<Cita>();
        }
    }

    public async Task<bool> CrearHistoriaClinica(HistoriaClinica historia)
    {
        try
        {
            var client = CreateClient();
            var json = JsonSerializer.Serialize(historia);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            var response = await client.PostAsync("/historia-clinica", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error creando historia clínica: {ex.Message}");
            return false;
        }
    }
}

// ============================================
// GESTOR DE CONVERSACIONES (ACTUALIZADO)
// ============================================

public class ConversationManager
{
    private readonly Dictionary<string, Conversation> _conversations = new();
    private readonly Dictionary<string, Operator> _operators = new();
    private readonly object _lock = new();

    public Conversation GetOrCreateConversation(string phoneNumber)
    {
        lock (_lock)
        {
            var conversation = _conversations.Values
                .FirstOrDefault(c => c.PhoneNumber == phoneNumber && c.Status != ConversationStatus.Closed);

            if (conversation == null)
            {
                conversation = new Conversation
                {
                    PhoneNumber = phoneNumber,
                    CustomerName = $"Cliente {phoneNumber.Substring(Math.Max(0, phoneNumber.Length - 4))}"
                };
                _conversations[conversation.ConversationId] = conversation;
                Console.WriteLine($"📞 Nueva conversación: {phoneNumber}");
            }

            return conversation;
        }
    }

    public void AddMessage(string conversationId, Message message)
    {
        lock (_lock)
        {
            if (_conversations.TryGetValue(conversationId, out var conversation))
            {
                conversation.Messages.Add(message);
                conversation.LastActivity = DateTime.UtcNow;
            }
        }
    }

    public bool AssignOperator(string conversationId, string operatorId)
    {
        lock (_lock)
        {
            if (_conversations.TryGetValue(conversationId, out var conversation) &&
                _operators.TryGetValue(operatorId, out var operatorInfo))
            {
                conversation.AssignedOperator = operatorId;
                conversation.Status = ConversationStatus.Active;
                operatorInfo.ActiveConversations.Add(conversationId);
                Console.WriteLine($"👤 Operador {operatorInfo.Name} asignado");
                return true;
            }
            return false;
        }
    }

    // ⭐ NUEVO: Devolver control al bot
    public bool ReleaseToBot(string conversationId)
    {
        lock (_lock)
        {
            if (_conversations.TryGetValue(conversationId, out var conversation))
            {
                if (!string.IsNullOrEmpty(conversation.AssignedOperator) &&
                    _operators.TryGetValue(conversation.AssignedOperator, out var operatorInfo))
                {
                    operatorInfo.ActiveConversations.Remove(conversationId);
                }
                
                conversation.AssignedOperator = null;
                conversation.Status = ConversationStatus.BotHandling;
                conversation.Context.Clear(); // Limpiar contexto
                Console.WriteLine($"🤖 Conversación {conversationId} devuelta al bot");
                return true;
            }
            return false;
        }
    }

    public void RegisterOperator(string operatorId, string name)
    {
        lock (_lock)
        {
            _operators[operatorId] = new Operator
            {
                OperatorId = operatorId,
                Name = name,
                IsAvailable = true
            };
            Console.WriteLine($"✅ Operador registrado: {name}");
        }
    }

    public List<Conversation> GetWaitingConversations()
    {
        lock (_lock)
        {
            return _conversations.Values
                .Where(c => c.Status == ConversationStatus.Waiting)
                .OrderBy(c => c.CreatedAt)
                .ToList();
        }
    }

    public Conversation? GetConversation(string conversationId)
    {
        lock (_lock)
        {
            _conversations.TryGetValue(conversationId, out var conversation);
            return conversation;
        }
    }

    public Conversation? GetConversationByPhone(string phoneNumber)
    {
        lock (_lock)
        {
            return _conversations.Values
                .FirstOrDefault(c => c.PhoneNumber == phoneNumber && c.Status != ConversationStatus.Closed);
        }
    }

    public string? GetAvailableOperator()
    {
        lock (_lock)
        {
            return _operators.Values
                .Where(o => o.IsAvailable && o.ActiveConversations.Count < 5)
                .OrderBy(o => o.ActiveConversations.Count)
                .FirstOrDefault()?.OperatorId;
        }
    }

    public bool IsConversationActive(string phoneNumber)
    {
        lock (_lock)
        {
            var conversation = _conversations.Values
                .FirstOrDefault(c => c.PhoneNumber == phoneNumber);
            return conversation != null && conversation.Status == ConversationStatus.Active;
        }
    }
}

// ============================================
// SERVICIO DE WHATSAPP (ACTUALIZADO CON MULTIMEDIA)
// ============================================

public class WhatsAppService
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public WhatsAppService(IConfiguration configuration)
    {
        _configuration = configuration;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization",
            $"Bearer {_configuration["WhatsApp:AccessToken"]}");
    }

    public async Task<bool> SendMessage(string to, string message)
    {
        try
        {
            var phoneNumberId = _configuration["WhatsApp:PhoneNumberId"];
            var url = $"https://graph.facebook.com/v22.0/{phoneNumberId}/messages";

            var payload = new
            {
                messaging_product = "whatsapp",
                to = to,
                type = "text",
                text = new { body = message }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync(url, content);
            
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✅ Mensaje enviado a {to}");
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error enviando mensaje: {ex.Message}");
            return false;
        }
    }

    // ⭐ NUEVO: Enviar imagen
    public async Task<bool> SendImage(string to, string imageUrl, string? caption = null)
    {
        try
        {
            var phoneNumberId = _configuration["WhatsApp:PhoneNumberId"];
            var url = $"https://graph.facebook.com/v22.0/{phoneNumberId}/messages";

            var payload = new
            {
                messaging_product = "whatsapp",
                to = to,
                type = "image",
                image = new 
                { 
                    link = imageUrl,
                    caption = caption
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync(url, content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error enviando imagen: {ex.Message}");
            return false;
        }
    }

    // ⭐ NUEVO: Enviar documento
    public async Task<bool> SendDocument(string to, string documentUrl, string filename, string? caption = null)
    {
        try
        {
            var phoneNumberId = _configuration["WhatsApp:PhoneNumberId"];
            var url = $"https://graph.facebook.com/v22.0/{phoneNumberId}/messages";

            Console.WriteLine($"📄 Enviando documento...");
            Console.WriteLine($"   📱 Para: {to}");
            Console.WriteLine($"   🔗 URL: {documentUrl}");
            Console.WriteLine($"   📎 Nombre: {filename}");

            var payload = new
            {
                messaging_product = "whatsapp",
                to = to,
                type = "document",
                document = new 
                { 
                    link = documentUrl,
                    filename = filename,
                    caption = caption
                }
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            Console.WriteLine($"   📦 Payload: {jsonPayload}");

            var content = new StringContent(
                jsonPayload,
                System.Text.Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync(url, content);
            
            var responseBody = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"   ✅ Documento enviado exitosamente");
                Console.WriteLine($"   📄 Respuesta: {responseBody}");
                return true;
            }
            else
            {
                Console.WriteLine($"   ❌ Error enviando documento");
                Console.WriteLine($"   📄 Status: {response.StatusCode}");
                Console.WriteLine($"   📄 Respuesta: {responseBody}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Excepción enviando documento: {ex.Message}");
            Console.WriteLine($"   Stack: {ex.StackTrace}");
            return false;
        }
    }

    // ✅ NUEVO: Enviar mensaje con botones
    public async Task<bool> SendButtonMessage(string to, string bodyText, List<ButtonOption> buttons)
    {
        try
        {
            var phoneNumberId = _configuration["WhatsApp:PhoneNumberId"];
            var url = $"https://graph.facebook.com/v22.0/{phoneNumberId}/messages";

            var buttonsPayload = buttons.Select(b => new
            {
                type = "reply",
                reply = new
                {
                    id = b.Id,
                    title = b.Title
                }
            }).ToList();

            var payload = new
            {
                messaging_product = "whatsapp",
                to = to,
                type = "interactive",
                interactive = new
                {
                    type = "button",
                    body = new { text = bodyText },
                    action = new
                    {
                        buttons = buttonsPayload
                    }
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync(url, content);
            
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✅ Mensaje con botones enviado a {to}");
                return true;
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"❌ Error: {errorContent}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error enviando botones: {ex.Message}");
            return false;
        }
    }

    // ✅ NUEVO: Enviar lista interactiva
    public async Task<bool> SendListMessage(string to, string bodyText, string buttonText, List<ListSection> sections)
    {
        try
        {
            var phoneNumberId = _configuration["WhatsApp:PhoneNumberId"];
            var url = $"https://graph.facebook.com/v22.0/{phoneNumberId}/messages";

            var sectionsPayload = sections.Select(s => new
            {
                title = s.Title,
                rows = s.Rows.Select(r => new
                {
                    id = r.Id,
                    title = r.Title,
                    description = r.Description
                }).ToList()
            }).ToList();

            var payload = new
            {
                messaging_product = "whatsapp",
                to = to,
                type = "interactive",
                interactive = new
                {
                    type = "list",
                    body = new { text = bodyText },
                    action = new
                    {
                        button = buttonText,
                        sections = sectionsPayload
                    }
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync(url, content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error enviando lista: {ex.Message}");
            return false;
        }
    }

}

// ============================================
// SERVICIO DE IA CON MENÚ INTERACTIVO
// ============================================

public class AIBotService
{
    private readonly WhatsAppService _whatsAppService;
    private readonly ConversationManager _conversationManager;
    private readonly ApiIntegrationService _apiService;

    public AIBotService(WhatsAppService whatsAppService, ConversationManager conversationManager, ApiIntegrationService apiService)
    {
        _whatsAppService = whatsAppService;
        _conversationManager = conversationManager;
        _apiService = apiService;
    }

    public async Task<(bool handled, string? response)> ProcessMessage(string message, string phoneNumber)
    {
        // Si un operador está atendiendo, NO responder
        if (_conversationManager.IsConversationActive(phoneNumber))
        {
            Console.WriteLine($"🚫 Bot NO responde - Operador activo");
            return (false, null);
        }

        var conversation = _conversationManager.GetConversationByPhone(phoneNumber);
        if (conversation == null) return (false, null);

        var lowerMessage = message.ToLower().Trim();

        // MANEJO DE CONTEXTO - Flujo de conversación
        if (conversation.Context.ContainsKey("esperando_opcion"))
        {
            return await HandleMenuOption(lowerMessage, phoneNumber, conversation);
        }

        if (conversation.Context.ContainsKey("solicitando_documento"))
        {
            return await HandleDocumentRequest(message, phoneNumber, conversation);
        }

        if (conversation.Context.ContainsKey("registrando_historia"))
        {
            return await HandleHistoriaClinicaFlow(message, phoneNumber, conversation);
        }

        // Respuestas básicas
        if (lowerMessage.Contains("hola") || lowerMessage.Contains("buenas"))
        {
            var welcomeMsg = "¡Hola! 👋 Bienvenido a nuestro servicio de salud.\n\n" +
                        "Por favor selecciona una opción:\n" +
                        "1️⃣ 📹 Agendar VideoLlamada\n" +
                        "2️⃣ 📋 Registrar Historia Clínica\n" +
                        "3️⃣ 📅 Consultar Citas Programadas\n" +
                        "4️⃣ 👤 Hablar con un Agente\n\n" +
                        "Escribe el número de tu opción (1, 2, 3 o 4)";
            
            conversation.Context["esperando_opcion"] = "menu_principal";
            await _whatsAppService.SendMessage(phoneNumber, welcomeMsg);
            return (true, welcomeMsg);
        }

        // Palabras clave para operador humano
        var humanRequired = new[] { "agente", "operador", "persona", "problema", "queja" };
        if (humanRequired.Any(k => lowerMessage.Contains(k)))
        {
            var transferMsg = "🔄 Te estoy conectando con un agente. Espera un momento...";
            await _whatsAppService.SendMessage(phoneNumber, transferMsg);
            return (false, transferMsg);
        }

        // Mensaje por defecto
        var defaultMsg = "Escribe 'hola' para ver el menú de opciones. 😊";
        await _whatsAppService.SendMessage(phoneNumber, defaultMsg);
        return (true, defaultMsg);
    }

    private async Task<(bool, string?)> HandleMenuOption(string option, string phoneNumber, Conversation conversation)
    {
        string response;
        
        switch (option)
        {
            case "1":
                response = "📹 *VideoLlamada*\n\n" +
                        "Para agendar una videollamada, necesito validar tus datos.\n" +
                        "Por favor ingresa tu número de documento:";
                conversation.Context["solicitando_documento"] = "videollamada";
                conversation.Context.Remove("esperando_opcion");
                break;

            case "2":
                response = "📋 *Registro de Historia Clínica*\n\n" +
                        "Vamos a registrar tus datos.\n" +
                        "Por favor ingresa tu número de documento:";
                conversation.Context["solicitando_documento"] = "historia";
                conversation.Context.Remove("esperando_opcion");
                break;

            case "3":
                response = "📅 *Consulta de Citas*\n\n" +
                        "Por favor ingresa tu número de documento para consultar tus citas:";
                conversation.Context["solicitando_documento"] = "consultar_citas";
                conversation.Context.Remove("esperando_opcion");
                break;

            case "4":
                response = "👤 Te estoy conectando con un agente...";
                await _whatsAppService.SendMessage(phoneNumber, response);
                conversation.Context.Clear();
                return (false, response);

            default:
                response = "Por favor selecciona una opción válida (1, 2, 3 o 4).";
                break;
        }

        await _whatsAppService.SendMessage(phoneNumber, response);
        return (true, response);
    }
 
    private async Task<(bool, string?)> HandleDocumentRequest(string documento, string phoneNumber, Conversation conversation)
    {
        var tipoSolicitud = conversation.Context["solicitando_documento"];
        conversation.Context.Remove("solicitando_documento");

        var paciente = await _apiService.BuscarPacientePorDocumento(documento);

        if (paciente != null)
        {
            var response = $"✅ ¡Hola {paciente.NombrePaciente} {paciente.ApellidoPaciente}!\n\n" +
                        $"Datos encontrados:\n" +
                        $"📄 Documento: {paciente.NoIdentificacion}\n" +
                        $"📋 Caso No: {paciente.NoCaso}\n\n";

            if (tipoSolicitud == "videollamada")
            {
                response += "Un agente te contactará pronto para agendar tu videollamada. 📹";
                conversation.Context.Clear();
            }
            else if (tipoSolicitud == "historia")
            {
                response += "Ya tienes historia clínica registrada.\n¿Deseas actualizarla? (si/no)";
                conversation.Context["actualizar_historia"] = paciente.NoCaso;
            }
            else if (tipoSolicitud == "consultar_citas")
            {
                // Consultar citas usando el documento
                var citas = await _apiService.ObtenerCitasPorTelefono(documento);
                
                if (citas.Any())
                {
                    response = "📋 *Tus citas programadas:*\n\n";
                    foreach (var cita in citas.Take(5))
                    {
                        response += $"📅 Fecha: {cita.Fecha}\n" +
                                $"🕐 Hora: {cita.Hora}\n" +
                                $"👨‍⚕️ Dr. {cita.Medico}\n" +
                                $"🏥 {cita.citaControl}\n";
                        
                        if (!string.IsNullOrEmpty(cita.Observacion))
                        {
                            response += $"📝 Observación: {cita.Observacion}\n";
                        }
                        
                        response += "\n";
                    }
                }
                else
                {
                    response = "No tienes citas programadas actualmente. 📅";
                }
                
                conversation.Context.Clear();
            }

            await _whatsAppService.SendMessage(phoneNumber, response);
            return (true, response);
        }
        else
        {
            var response = "❌ No encontramos tus datos en el sistema.\n\n";
            
            if (tipoSolicitud == "historia")
            {
                response += "Vamos a crear tu historia clínica.\n" +
                        "Ingresa tu nombre completo:";
                conversation.Context["registrando_historia"] = "nombre";
                conversation.Context["documento_nuevo"] = documento;
            }
            else
            {
                response += "Un agente te contactará para completar tu registro.";
                conversation.Context.Clear();
            }

            await _whatsAppService.SendMessage(phoneNumber, response);
            return (true, response);
        }
    }

    private async Task<(bool, string?)> HandleHistoriaClinicaFlow(string input, string phoneNumber, Conversation conversation)
    {
        var paso = conversation.Context["registrando_historia"];
        
        // ✅ Permitir cancelar en cualquier momento
        if (input.ToLower().Trim() == "cancelar" || input.ToLower().Trim() == "salir")
        {
            conversation.Context.Clear();
            
            var cancelMsg = "❌ Registro cancelado.\n\nEscribe 'hola' para volver al menú principal.";
            await _whatsAppService.SendMessage(phoneNumber, cancelMsg);
            return (true, cancelMsg);
        }
        
        string response;

        switch (paso)
        {
            case "nombre":
                conversation.Context["nombre"] = input;
                conversation.Context["registrando_historia"] = "fecha_nacimiento";
                
                // ✅ Enviar con botón de cancelar
                var buttons = new List<ButtonOption>
                {
                    new ButtonOption { Id = "cancelar", Title = "❌ Cancelar" }
                };
                
                response = "📅 Ingresa tu fecha de nacimiento (DD/MM/AAAA):\n\n_Ejemplo: 15/03/1990_";
                await _whatsAppService.SendMessage(phoneNumber, response);
                await _whatsAppService.SendButtonMessage(phoneNumber, "¿Deseas cancelar el registro?", buttons);
                return (true, response);

            case "fecha_nacimiento":
                // ✅ Validar formato de fecha
                if (!IsValidDate(input))
                {
                    response = "❌ Fecha inválida. Por favor usa el formato DD/MM/AAAA\n\nEjemplo: 15/03/1990\n\nEscribe 'cancelar' para salir.";
                    await _whatsAppService.SendMessage(phoneNumber, response);
                    return (true, response);
                }
                
                conversation.Context["fecha_nacimiento"] = input;
                conversation.Context["registrando_historia"] = "direccion";
                response = "🏠 Ingresa tu dirección completa:\n\n_Escribe 'cancelar' para salir_";
                break;

            case "direccion":
                conversation.Context["direccion"] = input;
                conversation.Context["registrando_historia"] = "telefono";
                response = "📱 Ingresa tu número de teléfono:\n\n_Escribe 'cancelar' para salir_";
                break;

            case "telefono":
                // ✅ Validar teléfono
                if (!IsValidPhone(input))
                {
                    response = "❌ Teléfono inválido. Debe contener solo números.\n\nEscribe 'cancelar' para salir.";
                    await _whatsAppService.SendMessage(phoneNumber, response);
                    return (true, response);
                }
                
                conversation.Context["telefono"] = input;
                conversation.Context["registrando_historia"] = "email";
                response = "📧 Ingresa tu correo electrónico:\n\n_Ejemplo: nombre@ejemplo.com_\n_Escribe 'cancelar' para salir_";
                break;

            case "email":
                // ✅ Validar email
                if (!IsValidEmail(input))
                {
                    response = "❌ Email inválido. Por favor ingresa un email válido.\n\nEjemplo: nombre@ejemplo.com\n\nEscribe 'cancelar' para salir.";
                    await _whatsAppService.SendMessage(phoneNumber, response);
                    return (true, response);
                }
                
                // ✅ Mostrar resumen antes de guardar
                var resumen = $"📋 *Resumen de tus datos:*\n\n" +
                            $"👤 Nombre: {conversation.Context["nombre"]}\n" +
                            $"📅 Fecha Nacimiento: {conversation.Context["fecha_nacimiento"]}\n" +
                            $"🏠 Dirección: {conversation.Context["direccion"]}\n" +
                            $"📱 Teléfono: {conversation.Context["telefono"]}\n" +
                            $"📧 Email: {input}\n\n" +
                            $"¿Confirmas estos datos?";
                
                var confirmButtons = new List<ButtonOption>
                {
                    new ButtonOption { Id = "confirmar_registro", Title = "✅ Confirmar" },
                    new ButtonOption { Id = "cancelar_registro", Title = "❌ Cancelar" }
                };
                
                conversation.Context["email"] = input;
                conversation.Context["registrando_historia"] = "confirmacion";
                
                await _whatsAppService.SendButtonMessage(phoneNumber, resumen, confirmButtons);
                return (true, resumen);

            case "confirmacion":
                if (input == "confirmar_registro" || input.ToLower() == "si" || input.ToLower() == "confirmar")
                {
                    // Guardar en BD vía API
                    var historia = new HistoriaClinica
                    {
                        Documento = conversation.Context["documento_nuevo"],
                        NombreCompleto = conversation.Context["nombre"],
                        Direccion = conversation.Context["direccion"],
                        Telefono = conversation.Context["telefono"],
                        Email = conversation.Context["email"]
                    };

                    var success = await _apiService.CrearHistoriaClinica(historia);
                    
                    if (success)
                    {
                        response = "✅ *¡Historia clínica creada exitosamente!*\n\n" +
                                "Tus datos han sido registrados correctamente.\n\n" +
                                "Escribe 'hola' para volver al menú principal.";
                    }
                    else
                    {
                        response = "❌ Hubo un error al guardar tus datos.\n" +
                                "Un agente te contactará para ayudarte.";
                    }
                    
                    conversation.Context.Clear();
                }
                else
                {
                    response = "❌ Registro cancelado.\n\nEscribe 'hola' para volver al menú principal.";
                    conversation.Context.Clear();
                }
                break;

            default:
                response = "Escribe 'hola' para comenzar nuevamente.";
                conversation.Context.Clear();
                break;
        }

        await _whatsAppService.SendMessage(phoneNumber, response);
        return (true, response);
    }

    // ✅ Métodos de validación
    private bool IsValidDate(string date)
    {
        var parts = date.Split('/');
        if (parts.Length != 3) return false;
        
        return int.TryParse(parts[0], out var day) && day >= 1 && day <= 31 &&
            int.TryParse(parts[1], out var month) && month >= 1 && month <= 12 &&
            int.TryParse(parts[2], out var year) && year >= 1900 && year <= DateTime.Now.Year;
    }

    private bool IsValidPhone(string phone)
    {
        var cleaned = phone.Replace(" ", "").Replace("-", "").Replace("+", "");
        return cleaned.Length >= 7 && cleaned.All(char.IsDigit);
    }

    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email && email.Contains("@") && email.Contains(".");
        }
        catch
        {
            return false;
        }
    }
}

// ============================================
// SIGNALR HUB (ACTUALIZADO)
// ============================================

public class ChatHub : Hub
{
    private readonly ConversationManager _conversationManager;
    private readonly WhatsAppService _whatsAppService;

    public ChatHub(ConversationManager conversationManager, WhatsAppService whatsAppService)
    {
        _conversationManager = conversationManager;
        _whatsAppService = whatsAppService;
    }

    public async Task RegisterOperator(string operatorName)
    {
        var operatorId = Context.ConnectionId;
        _conversationManager.RegisterOperator(operatorId, operatorName);
        
        await Clients.Caller.SendAsync("OperatorRegistered", operatorId);
        await SendWaitingConversations();
    }

    public async Task TakeConversation(string conversationId)
    {
        var operatorId = Context.ConnectionId;
        var success = _conversationManager.AssignOperator(conversationId, operatorId);
        
        if (success)
        {
            var conversation = _conversationManager.GetConversation(conversationId);
            await Clients.Caller.SendAsync("ConversationAssigned", conversation);
            await Clients.Others.SendAsync("ConversationTaken", conversationId);
        }
    }

    // ⭐ NUEVO: Devolver control al bot
    public async Task ReleaseConversationToBot(string conversationId)
    {
        var success = _conversationManager.ReleaseToBot(conversationId);
        
        if (success)
        {
            var conversation = _conversationManager.GetConversation(conversationId);
            
            // Enviar mensaje al cliente
            await _whatsAppService.SendMessage(
                conversation!.PhoneNumber,
                "🤖 Un agente finalizó la conversación. Escribe 'hola' si necesitas más ayuda."
            );
            
            // Notificar a todos los operadores
            await Clients.All.SendAsync("ConversationReleasedToBot", conversationId);
            
            Console.WriteLine($"✅ Conversación {conversationId} devuelta al bot");
        }
    }

    public async Task SendMessageToCustomer(string conversationId, string message)
    {
        var conversation = _conversationManager.GetConversation(conversationId);
        
        if (conversation != null)
        {
            var operatorMessage = new Message
            {
                Content = message,
                Type = MessageType.Operator,
                Sender = "Operador"
            };

            _conversationManager.AddMessage(conversationId, operatorMessage);
            
            // Enviar por WhatsApp
            await _whatsAppService.SendMessage(conversation.PhoneNumber, message);
            
            // Notificar a todos los operadores
            await Clients.All.SendAsync("MessageSent", conversationId, operatorMessage);
        }
    }

    // ⭐ NUEVO: Enviar imagen al cliente
    public async Task SendImageToCustomer(string conversationId, string imageUrl, string? caption)
    {
        var conversation = _conversationManager.GetConversation(conversationId);
        
        if (conversation != null)
        {
            // Enviar imagen por WhatsApp
            var success = await _whatsAppService.SendImage(conversation.PhoneNumber, imageUrl, caption);
            
            if (success)
            {
                var imageMessage = new Message
                {
                    Content = caption ?? "Imagen",
                    Type = MessageType.Operator,
                    Sender = "Operador",
                    MediaUrl = imageUrl,
                    MediaType = "image"
                };

                _conversationManager.AddMessage(conversationId, imageMessage);
                
                // Notificar a todos los operadores
                await Clients.All.SendAsync("MessageSent", conversationId, imageMessage);
            }
        }
    }

    // ⭐ NUEVO: Enviar documento al cliente
    public async Task SendDocumentToCustomer(string conversationId, string documentUrl, string filename, string? caption)
    {
        var conversation = _conversationManager.GetConversation(conversationId);
        
        if (conversation != null)
        {
            // Enviar documento por WhatsApp
            var success = await _whatsAppService.SendDocument(conversation.PhoneNumber, documentUrl, filename, caption);
            
            if (success)
            {
                var docMessage = new Message
                {
                    Content = caption ?? $"Documento: {filename}",
                    Type = MessageType.Operator,
                    Sender = "Operador",
                    MediaUrl = documentUrl,
                    MediaType = "document"
                };

                _conversationManager.AddMessage(conversationId, docMessage);
                
                // Notificar a todos los operadores
                await Clients.All.SendAsync("MessageSent", conversationId, docMessage);
            }
        }
    }

    public async Task SendWaitingConversations()
    {
        var waiting = _conversationManager.GetWaitingConversations();
        await Clients.All.SendAsync("WaitingConversations", waiting);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        Console.WriteLine($"❌ Operador desconectado: {Context.ConnectionId}");
        await base.OnDisconnectedAsync(exception);
    }

    // ✅ NUEVO: Enviar link de videollamada al cliente
    public async Task SendVideoCallToCustomer(string conversationId)
    {
        var conversation = _conversationManager.GetConversation(conversationId);
        
        if (conversation != null)
        {
            // Generar link de Jitsi
            var jitsiService = new JitsiService(
                Context.GetHttpContext()?.RequestServices.GetRequiredService<IConfiguration>() 
                ?? throw new InvalidOperationException("Configuration not available")
            );
            
            var videoCallUrl = jitsiService.GenerateVideoCallLink(
                conversation.PhoneNumber, 
                conversation.CustomerName
            );
            
            // Crear mensaje con el link
            var message = $"📹 *Invitación a Videollamada*\n\n" +
                        $"Haz clic en el siguiente enlace para unirte a la videollamada:\n\n" +
                        $"{videoCallUrl}\n\n" +
                        $"_La videollamada es segura y privada._";
            
            // Enviar por WhatsApp
            var success = await _whatsAppService.SendMessage(conversation.PhoneNumber, message);
            
            if (success)
            {
                var videoCallMessage = new Message
                {
                    Content = "📹 Invitación a videollamada enviada",
                    Type = MessageType.Operator,
                    Sender = "Operador",
                    MediaUrl = videoCallUrl,
                    MediaType = "video_call"
                };

                _conversationManager.AddMessage(conversationId, videoCallMessage);
                
                // Notificar a todos los operadores
                await Clients.All.SendAsync("MessageSent", conversationId, videoCallMessage);
                
                Console.WriteLine($"✅ Videollamada enviada a {conversation.PhoneNumber}");
                Console.WriteLine($"   🔗 URL: {videoCallUrl}");
            }
        }
    }



}