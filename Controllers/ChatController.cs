using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly OpenAIThreadService _threadService;

    public ChatController(OpenAIThreadService threadService)
    {
        _threadService = threadService;
    }

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Pergunta))
        {
            return BadRequest(new { erro = "A pergunta não pode estar vazia." });
        }

        try
        {
            // Chama o serviço para obter a resposta
            var resposta = await _threadService.GetResponseFromThreadAsync(request.Pergunta);
            return Ok(new { resposta });
        }
        catch (HttpRequestException ex)
        {
            // Trata erros de requisição HTTP
            return StatusCode(502, new { erro = "Erro ao se comunicar com a API externa.", detalhes = ex.Message });
        }
        catch (Exception ex)
        {
            // Trata outros erros
            return StatusCode(500, new { erro = "Erro interno do servidor.", detalhes = ex.Message });
        }
    }
}

// Classe auxiliar para receber a pergunta no corpo da requisição
public class ChatRequest
{
    public string? Pergunta { get; set; }
}
