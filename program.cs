using Microsoft.AspNetCore.SignalR;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// ⭐ CONFIGURAR PUERTO DINÁMICO PARA RAILWAY
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

Console.WriteLine($"🚀 Servidor configurado en puerto: {port}");

// Configurar servicios
builder.Services.AddControllers();
builder.Services.AddSignalR();
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
}

public class Message
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
    public string Content { get; set; } = "";
    public MessageType Type { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Sender { get; set; } = "";
}

public enum ConversationStatus
{
    Waiting,      // Esperando operador
    Active,       // Operador atendiendo
    BotHandling,  // Bot manejando
    Closed        // Cerrada
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
// GESTOR DE CONVERSACIONES
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
                Console.WriteLine($"💬 Mensaje agregado: {message.Content.Substring(0, Math.Min(30, message.Content.Length))}...");
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
                conversation.Status = ConversationStatus.Active; // ⭐ IMPORTANTE: Cambiar a Active
                operatorInfo.ActiveConversations.Add(conversationId);
                Console.WriteLine($"👤 Operador {operatorInfo.Name} asignado a conversación {conversationId}");
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

    public List<Conversation> GetOperatorConversations(string operatorId)
    {
        lock (_lock)
        {
            return _conversations.Values
                .Where(c => c.AssignedOperator == operatorId && c.Status == ConversationStatus.Active)
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

    // ⭐ NUEVO MÉTODO: Verificar si una conversación está siendo atendida por operador
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
// SERVICIO DE WHATSAPP
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
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"❌ Error enviando mensaje: {error}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error enviando mensaje: {ex.Message}");
            return false;
        }
    }
}

// ============================================
// SERVICIO DE IA (BOT AUTÓNOMO)
// ============================================

public class AIBotService
{
    private readonly WhatsAppService _whatsAppService;
    private readonly ConversationManager _conversationManager;

    public AIBotService(WhatsAppService whatsAppService, ConversationManager conversationManager)
    {
        _whatsAppService = whatsAppService;
        _conversationManager = conversationManager;
    }

    public async Task<(bool handled, string? response)> ProcessMessage(string message, string phoneNumber)
    {
        // ⭐ CRÍTICO: Si un operador está atendiendo, NO responder automáticamente
        if (_conversationManager.IsConversationActive(phoneNumber))
        {
            Console.WriteLine($"🚫 Bot NO responde - Operador activo para {phoneNumber}");
            return (false, null); // NO manejado, el operador debe responder
        }

        var lowerMessage = message.ToLower().Trim();

        // Respuestas automáticas
        var autoResponses = new Dictionary<string, string>
        {
            { "hola", "¡Hola! 👋 Bienvenido a nuestro servicio. ¿En qué puedo ayudarte hoy?" },
            { "horario", "📅 Nuestro horario de atención es:\n🕐 Lunes a Viernes: 8:00 AM - 6:00 PM\n🕐 Sábados: 9:00 AM - 2:00 PM" },
            { "precio", "💰 Para consultas de precios, un agente especializado te atenderá en breve." },
            { "ayuda", "Puedo ayudarte con:\n✅ Información general\n✅ Horarios\n✅ Ubicación\n\nPara asistencia personalizada, escribe 'agente'" },
            { "ubicacion", "📍 Nos encontramos en:\nAv. Principal #123, Ciudad" },
            { "gracias", "¡De nada! 😊 ¿Hay algo más en que pueda ayudarte?" }
        };

        foreach (var key in autoResponses.Keys)
        {
            if (lowerMessage.Contains(key))
            {
                await _whatsAppService.SendMessage(phoneNumber, autoResponses[key]);
                Console.WriteLine($"🤖 Bot respondió automáticamente a: {message}");
                return (true, autoResponses[key]);
            }
        }

        // Palabras clave que requieren operador humano
        var humanRequired = new[] { "agente", "operador", "hablar", "persona", "problema", "queja", "reclamo" };
        
        if (humanRequired.Any(keyword => lowerMessage.Contains(keyword)))
        {
            var transferMessage = "🔄 Te estoy conectando con un agente humano. Por favor espera un momento...";
            await _whatsAppService.SendMessage(phoneNumber, transferMessage);
            Console.WriteLine($"👤 Transferencia a operador solicitada por: {phoneNumber}");
            return (false, transferMessage);
        }

        // Si no entiende el mensaje
        var defaultMessage = "Entiendo que necesitas ayuda. Un agente te atenderá pronto. " +
                           "También puedes escribir 'ayuda' para ver las opciones disponibles.";
        await _whatsAppService.SendMessage(phoneNumber, defaultMessage);
        return (false, defaultMessage);
    }
}

// ============================================
// SIGNALR HUB
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
            Console.WriteLine($"✅ Conversación {conversationId} tomada por operador {operatorId}");
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
}