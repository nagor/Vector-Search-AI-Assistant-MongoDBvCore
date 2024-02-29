using Microsoft.AspNetCore.Mvc;
using SharedLib.Models;
using SharedLib.Services;

namespace ChatAPI.Controllers;

[ApiController]
[Route("[controller]")]
public class ChatController : ControllerBase
{
    private readonly ILogger<ChatController> _logger;
    private readonly ChatService _chatService;

    public ChatController(ILogger<ChatController> logger, ChatService chatService)
    {
        _logger = logger;
        _chatService = chatService;
    }

    [HttpGet(Name = "GetSession")]
    public async Task<List<string>> Get()
    {
        List<Session> sessions = await _chatService.GetAllChatSessionsAsync();
        return sessions.Select(s => s.Id).ToList();
    }
}