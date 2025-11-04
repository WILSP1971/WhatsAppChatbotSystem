using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;

namespace WhatsAppChatbotSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebhookController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ConversationManager _conversationManager;
    private readonly WhatsAppService _whatsAppService;
    private readonly AIBotService _aiBotService;
    private readonly IHubContext<ChatHub> _hubContext;

    public WebhookController(
        IConfiguration configuration,
        ConversationManager conversationManager,
        WhatsAppService whatsAppService,
        AIBotService aiBotService,
        IHubContext<ChatHub> hubContext)
    {
        _configuration = configuration;
        _conversationManager = conversationManager;
        _whatsAppService = whatsAppService;
        _aiBotService = aiBotService;
        _hubContext = hubContext;
    }

    [HttpGet("whatsapp")]
    public IActionResult VerifyWebhook([FromQuery(Name = "hub.mode")] string mode,
                                       [FromQuery(Name = "hub.verify_token")] string token,
                                       [FromQuery(Name = "hub.challenge")] string challenge)
    {
        var verifyToken = _configuration["WhatsApp:VerifyToken"];

        if (mode == "subscribe" && token == verifyToken)
        {
            Console.WriteLine("✅ Webhook verificado correctamente");
            return Ok(challenge);
        }

        Console.WriteLine("❌ Verificación de webhook fallida");
        return Forbid();
    }

    [HttpPost("whatsapp")]
    public async Task<IActionResult> ReceiveMessage([FromBody] JsonElement body)
    {
        try
        {
            Console.WriteLine($"📨 Webhook recibido");

            var entry = body.GetProperty("entry")[0];
            var changes = entry.GetProperty("changes")[0];
            var value = changes.GetProperty("value");

            if (value.TryGetProperty("messages", out var messages))
            {
                var message = messages[0];
                var from = message.GetProperty("from").GetString() ?? "";
                var messageId = message.GetProperty("id").GetString() ?? "";
                
                // ⭐ Obtener tipo de mensaje
                var messageType = message.GetProperty("type").GetString() ?? "text";
                
                string messageBody = "";
                string? mediaUrl = null;
                string? mediaType = null;

                // ✅ Manejar respuestas de botones interactivos
                if (messageType == "interactive")
                {
                    var interactive = message.GetProperty("interactive");
                    var interactiveType = interactive.GetProperty("type").GetString();
                    
                    if (interactiveType == "button_reply")
                    {
                        messageBody = interactive.GetProperty("button_reply").GetProperty("id").GetString() ?? "";
                    }
                    else if (interactiveType == "list_reply")
                    {
                        messageBody = interactive.GetProperty("list_reply").GetProperty("id").GetString() ?? "";
                    }
                }
                else if (messageType == "text")
                {
                    messageBody = message.GetProperty("text").GetProperty("body").GetString() ?? "";
                }

                // ⭐ Procesar diferentes tipos de mensajes
                switch (messageType)
                {
                    case "text":
                        messageBody = message.GetProperty("text").GetProperty("body").GetString() ?? "";
                        break;
                    
                    case "image":
                        if (message.TryGetProperty("image", out var image))
                        {
                            mediaUrl = image.GetProperty("id").GetString(); // ID de media de WhatsApp
                            messageBody = image.TryGetProperty("caption", out var caption) 
                                ? caption.GetString() ?? "Imagen recibida" 
                                : "Imagen recibida";
                            mediaType = "image";
                        }
                        break;
                    
                    case "document":
                        if (message.TryGetProperty("document", out var document))
                        {
                            mediaUrl = document.GetProperty("id").GetString();
                            var filename = document.TryGetProperty("filename", out var fn) 
                                ? fn.GetString() ?? "documento" 
                                : "documento";
                            messageBody = $"Documento recibido: {filename}";
                            mediaType = "document";
                        }
                        break;
                    
                    case "audio":
                        messageBody = "Audio recibido";
                        mediaType = "audio";
                        break;
                    
                    case "video":
                        messageBody = "Video recibido";
                        mediaType = "video";
                        break;
                    
                    default:
                        messageBody = $"Mensaje de tipo: {messageType}";
                        break;
                }

                Console.WriteLine($"💬 Mensaje de {from}: {messageBody}");

                // Crear o recuperar conversación
                var conversation = _conversationManager.GetOrCreateConversation(from);

                // Agregar mensaje del cliente
                var customerMessage = new Message
                {
                    Content = messageBody,
                    Type = MessageType.Customer,
                    Sender = conversation.CustomerName,
                    MessageId = messageId,
                    MediaUrl = mediaUrl,
                    MediaType = mediaType
                };

                _conversationManager.AddMessage(conversation.ConversationId, customerMessage);

                // Notificar a todos los operadores del nuevo mensaje
                await _hubContext.Clients.All.SendAsync("NewMessageReceived", conversation.ConversationId, customerMessage);

                // Verificar si hay un operador atendiendo
                if (conversation.Status == ConversationStatus.Active && !string.IsNullOrEmpty(conversation.AssignedOperator))
                {
                    Console.WriteLine($"👤 Operador activo - Bot NO responde");
                    
                    // Notificar al operador específico
                    await _hubContext.Clients.Client(conversation.AssignedOperator)
                        .SendAsync("CustomerMessageReceived", conversation.ConversationId, customerMessage);
                }
                else
                {
                    // ⭐ Solo procesar con bot si es mensaje de texto
                    if (messageType == "text")
                    {
                        var (handled, botResponse) = await _aiBotService.ProcessMessage(messageBody, from);

                        if (handled)
                        {
                            conversation.Status = ConversationStatus.BotHandling;
                            
                            var botMessage = new Message
                            {
                                Content = botResponse ?? "",
                                Type = MessageType.Bot,
                                Sender = "Bot Automático"
                            };
                            
                            _conversationManager.AddMessage(conversation.ConversationId, botMessage);
                            await _hubContext.Clients.All.SendAsync("BotHandledMessage", conversation);
                        }
                        else
                        {
                            conversation.Status = ConversationStatus.Waiting;
                            var availableOperator = _conversationManager.GetAvailableOperator();
                            
                            if (availableOperator != null)
                            {
                                _conversationManager.AssignOperator(conversation.ConversationId, availableOperator);
                                await _hubContext.Clients.Client(availableOperator)
                                    .SendAsync("NewConversationAssigned", conversation);
                            }
                            else
                            {
                                await _hubContext.Clients.All
                                    .SendAsync("NewConversationWaiting", conversation);
                            }
                        }
                    }
                    else
                    {
                        // Si es multimedia, transferir a operador automáticamente
                        conversation.Status = ConversationStatus.Waiting;
                        await _whatsAppService.SendMessage(from, "He recibido tu archivo. Un agente te atenderá pronto. 👤");
                        
                        await _hubContext.Clients.All.SendAsync("NewConversationWaiting", conversation);
                    }
                }
            }

            return Ok();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error procesando webhook: {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
            return Ok();
        }
    }

    [HttpPost("send-test")]
    public async Task<IActionResult> SendTestMessage([FromBody] TestMessageRequest request)
    {
        var success = await _whatsAppService.SendMessage(request.To, request.Message);
        
        if (success)
            return Ok(new { success = true, message = "Mensaje enviado" });
        else
            return BadRequest(new { success = false, message = "Error al enviar mensaje" });
    }

    [HttpGet("stats")]
    public IActionResult GetStats()
    {
        var waiting = _conversationManager.GetWaitingConversations();
        
        return Ok(new
        {
            waitingConversations = waiting.Count,
            timestamp = DateTime.UtcNow
        });
    }
}

public class TestMessageRequest
{
    public string To { get; set; } = "";
    public string Message { get; set; } = "";
}