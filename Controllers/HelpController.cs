// Controllers/HelpController.cs
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TonyTI_Web.Models;

namespace TonyTI_Web.Controllers
{
    // Controller responsável pela página de ajuda e endpoint que consulta a IA
    public class HelpController : Controller
    {
        private readonly ILogger<HelpController> _logger;
        private readonly IOpenAiService _openAiService;

        // Injetar serviço de IA (implemente IOpenAiService no seu projeto)
        public HelpController(ILogger<HelpController> logger, IOpenAiService openAiService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _openAiService = openAiService ?? throw new ArgumentNullException(nameof(openAiService));
        }

        // GET: /Help
        // Renderiza a view de ajuda (Views/Help/Index.cshtml ou Home/Help conforme sua rota)
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // POST: /Help/Ask
        // Recebe JSON { "question": "..." } e retorna { "answer": "..." }
        [HttpPost]
        [Route("Help/Ask")]
        public async Task<IActionResult> Ask([FromBody] AskRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Question))
            {
                return BadRequest(new { error = "Pergunta obrigatória." });
            }

            try
            {
                var prompt = BuildPrompt(request.Question, request.Context);
                var aiResponse = await _openAiService.GetResponseAsync(prompt);

                return Ok(new { answer = aiResponse ?? string.Empty });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao consultar IA.");
                return StatusCode(500, new { error = "Erro interno ao processar a pergunta." });
            }
        }

        // Monta prompt simples para enviar ao serviço de IA
        private static string BuildPrompt(string question, string? context)
        {
            // Breve instrução + contexto opcional + pergunta
            var ctxPart = string.IsNullOrWhiteSpace(context) ? string.Empty :
                $"Contexto adicional: {context}\n\n";

            return $@"
Você é um assistente técnico de suporte (tone: objetivo, passo-a-passo). 
Responda apenas com instruções técnicas úteis. Não solicite senhas.
{ctxPart}
Pergunta: {question}
";
        }

        // DTO usado pelo endpoint Ask
        public class AskRequest
        {
            public string Question { get; set; } = string.Empty; // texto da pergunta
            public string? Context { get; set; } // opcional: id do ticket ou contexto curto
        }
    }

    // Interface esperada pelo controller. Se já existir no projeto, remova esta definição.
    public interface IOpenAiService
    {
        // Envia prompt para a IA e retorna texto simples com a resposta
        Task<string?> GetResponseAsync(string prompt);
    }
}
