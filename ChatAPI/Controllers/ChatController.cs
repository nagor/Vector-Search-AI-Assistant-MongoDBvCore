using Microsoft.AspNetCore.Mvc;
using SharedLib.Models;
using SharedLib.Services;

namespace ChatAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly ILogger<ChatController> _logger;
    private readonly ChatService _chatService;

    public ChatController(ILogger<ChatController> logger, ChatService chatService)
    {
        _logger = logger;
        _chatService = chatService;
    }

    [HttpGet]
    [Route("sessions")]
    public async Task<List<string>> Get()
    {
        List<Session> sessions = await _chatService.GetAllChatSessionsAsync();
        return sessions.Select(s => s.Id).ToList();
    }

    [HttpGet]
    [Route("messages/{sessionId}")]
    public async Task<List<Message>> Get(string sessionId)
    {
        List<Message> messages = await _chatService.GetChatSessionMessagesAsync(sessionId);
        return messages;
    }
}