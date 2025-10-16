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

    // Verificación del webhook (GET)
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

    // Recepción de mensajes (POST)
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
                var messageBody = message.GetProperty("text").GetProperty("body").GetString() ?? "";

                Console.WriteLine($"💬 Mensaje de {from}: {messageBody}");

                // Crear o recuperar conversación
                var conversation = _conversationManager.GetOrCreateConversation(from);

                // Agregar mensaje del cliente
                var customerMessage = new Message
                {
                    Content = messageBody,
                    Type = MessageType.Customer,
                    Sender = conversation.CustomerName,
                    MessageId = messageId
                };

                _conversationManager.AddMessage(conversation.ConversationId, customerMessage);

                // ⭐ NOTIFICAR A TODOS LOS OPERADORES DEL NUEVO MENSAJE
                await _hubContext.Clients.All.SendAsync("NewMessageReceived", conversation.ConversationId, customerMessage);

                // ⭐ VERIFICAR SI HAY UN OPERADOR ATENDIENDO
                if (conversation.Status == ConversationStatus.Active && !string.IsNullOrEmpty(conversation.AssignedOperator))
                {
                    // Hay un operador atendiendo - NO responder automáticamente
                    Console.WriteLine($"👤 Operador activo - Bot NO responde");
                    
                    // Notificar al operador específico
                    await _hubContext.Clients.Client(conversation.AssignedOperator)
                        .SendAsync("CustomerMessageReceived", conversation.ConversationId, customerMessage);
                }
                else
                {
                    // No hay operador - intentar respuesta automática del bot
                    var (handled, botResponse) = await _aiBotService.ProcessMessage(messageBody, from);

                    if (handled)
                    {
                        // El bot manejó la consulta
                        conversation.Status = ConversationStatus.BotHandling;
                        
                        var botMessage = new Message
                        {
                            Content = botResponse ?? "",
                            Type = MessageType.Bot,
                            Sender = "Bot Automático"
                        };
                        
                        _conversationManager.AddMessage(conversation.ConversationId, botMessage);
                        
                        // Notificar a operadores
                        await _hubContext.Clients.All.SendAsync("BotHandledMessage", conversation);
                    }
                    else
                    {
                        // Requiere atención humana
                        conversation.Status = ConversationStatus.Waiting;

                        // Intentar asignar automáticamente a operador disponible
                        var availableOperator = _conversationManager.GetAvailableOperator();
                        
                        if (availableOperator != null)
                        {
                            _conversationManager.AssignOperator(conversation.ConversationId, availableOperator);
                            await _hubContext.Clients.Client(availableOperator)
                                .SendAsync("NewConversationAssigned", conversation);
                        }
                        else
                        {
                            // Notificar a todos los operadores que hay una conversación esperando
                            await _hubContext.Clients.All
                                .SendAsync("NewConversationWaiting", conversation);
                        }
                    }
                }
            }

            return Ok();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error procesando webhook: {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
            return Ok(); // WhatsApp espera 200 OK siempre
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