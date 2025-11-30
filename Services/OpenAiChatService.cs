using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TonyTI_Web.Services
{
    public class OpenAiChatService : IChatService
    {
        private readonly HttpClient _http;
        private readonly ILogger<OpenAiChatService> _logger;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly int _maxTokens;
        private readonly double _temperature;

        public OpenAiChatService(HttpClient http, IConfiguration cfg, ILogger<OpenAiChatService> logger)
        {
            _http = http;
            _logger = logger;
            _apiKey = cfg["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI:ApiKey não encontrado em config");
            _model = cfg["OpenAI:Model"] ?? "gpt-4o-mini";
            _maxTokens = int.TryParse(cfg["OpenAI:MaxTokens"], out var t) ? t : 512;
            _temperature = double.TryParse(cfg["OpenAI:Temperature"], out var temp) ? temp : 0.2;

            // Header Bearer
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            _http.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<string> AskAsync(string userEmail, string question)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(question))
                    return "Pergunta vazia.";

                // Filtragem simples: se detecta conteúdo fora do escopo retorna mensagem padrão
                if (IsOutOfScope(question))
                {
                    return "O suporte agradece seu contato, mas infelizmente este ambiente foi criado apenas para perguntas relacionadas a tecnologia e suporte técnico.";
                }

                // Monta payload para /v1/chat/completions
                var payload = new
                {
                    model = _model,
                    messages = new[]
                    {
                        new { role = "system", content = "Você é um assistente de suporte técnico, responda apenas perguntas relacionadas a tecnologia e desenvolvimento de software. Seja objetivo e claro." },
                        new { role = "user", content = question }
                    },
                    max_tokens = _maxTokens,
                    temperature = _temperature
                };

                var json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var resp = await _http.PostAsync("https://api.openai.com/v1/chat/completions", content);
                var respText = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("OpenAI retornou erro {Status}: {Resp}", resp.StatusCode, respText);
                    return "Ocorreu um erro ao consultar o serviço de IA. Tente novamente mais tarde.";
                }

                using var doc = JsonDocument.Parse(respText);
                if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var first = choices[0];
                    if (first.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var contentNode))
                    {
                        var answer = contentNode.GetString() ?? string.Empty;
                        return answer.Trim();
                    }
                }

                _logger.LogWarning("Resposta OpenAI sem campo esperado. Raw: {Raw}", respText);
                return "Não obtivemos uma resposta válida do serviço de IA. Tente novamente mais tarde.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao chamar OpenAI");
                return "Erro interno ao consultar o serviço de IA. Tente novamente mais tarde.";
            }
        }

        // Heurística simples para recusar tópicos fora do nicho tecnológico
        private static bool IsOutOfScope(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return true;
            text = text.ToLowerInvariant();

            // Palavras-chave que indicam claramente fora do escopo
            var outOfScopeKeywords = new[]
            {
                "receita", "cozinhar", "bolo", "forno", "assar", "sobremesa",
                "filme", "romance", "cantor", "horóscopo", "previsão do tempo",
                "astrolog", "livro infantil", "brincadeira"
            };

            foreach (var k in outOfScopeKeywords)
            {
                if (text.Contains(k)) return true;
            }

            // Se encontrar termos técnicos consideramos IN-SCOPE
            var techKeywords = new[] { "error", "erro", "bug", "c#", "asp.net", "mvc", "sql", "database", "api", "javascript", "html", "css", "docker", "azure", "linux", "windows", "git", "smtp", "autenticação", "login", "senha" };
            foreach (var t in techKeywords)
            {
                if (text.Contains(t)) return false; // não é out-of-scope
            }

            // Caso neutro: permitir (assumir tecnologia). Ajuste se preferir bloqueio estrito.
            return false;
        }
    }
}
