using ChatAPI.Models;
using Microsoft.AspNetCore.Mvc;
using SharedLib.Models;
using SharedLib.Services;

namespace ChatAPI.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ChatController : ControllerBase
{
    private readonly ILogger<ChatController> _logger;
    private readonly ChatService _chatService;

    public ChatController(ILogger<ChatController> logger, ChatService chatService)
    {
        _logger = logger;
        _chatService = chatService;
    }

    [HttpPost]
    [Route("sessions")]
    public async Task<string> CreateSession()
    {
        string sessionId = await _chatService.CreateNewChatSessionAsync();
        return sessionId;
    }

    [HttpGet]
    [Route("messages/{sessionId}")]
    public async Task<IActionResult> GetSessionMessages(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest();
        }

        List<Message> messages = await _chatService.GetChatSessionMessagesAsync(sessionId);
        return Ok(messages);
    }

    [HttpPost]
    [Route("messages/{sessionId}")]
    public async Task<IActionResult> PostMessage(string sessionId, [FromBody] MessagePost? message)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest("SessionId is required.");
        }

        if (string.IsNullOrWhiteSpace(message?.UserPrompt))
        {
            return BadRequest("UserPrompt is required.");
        }

        List<Message> messages = await _chatService.GetChatCompletionProductSearchAsync(sessionId, message.UserPrompt, "clothes", null, null, null);
        return Ok(messages);
    }
}