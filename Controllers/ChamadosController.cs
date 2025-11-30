// Controllers/ChamadosController.cs
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using TonyTI_Web.Models;
using TonyTI_Web.Services;

namespace TonyTI_Web.Controllers
{
    // exige autenticação em todas as ações deste controller por padrão
    [Authorize]
    public class ChamadosController : Controller
    {
        private readonly IChamadoService _chamadoService;
        private readonly ILogger<ChamadosController> _logger;
        private readonly IWebHostEnvironment _env;

        public ChamadosController(IChamadoService chamadoService, ILogger<ChamadosController> logger, IWebHostEnvironment env)
        {
            _chamadoService = chamadoService;
            _logger = logger;
            _env = env;
        }

        // GET: /Chamados
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                bool isTecnico = IsUserTecnico(User);

                if (isTecnico)
                {
                    var listAll = await _chamadoService.GetAllAsync();
                    ViewBag.IsTecnico = true;
                    return View(listAll);
                }
                else
                {
                    var email = GetUserEmail(User);
                    if (string.IsNullOrWhiteSpace(email))
                    {
                        _logger.LogWarning("Usuário autenticado sem claim de e-mail tentou acessar Meus Chamados.");
                        // força login (não deveria ocorrer, porque [Authorize] já exige autenticação)
                        return RedirectToAction("Login", "Account");
                    }

                    var list = await _chamadoService.GetByEmailAsync(email);
                    ViewBag.IsTecnico = false;
                    return View(list);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter lista de chamados");
                TempData["Message"] = "Erro ao carregar chamados.";
                return View(new List<Chamado>());
            }
        }

        // GET: /Chamados/Create
        // Permitimos criação pública (sem login) — se desejar mudar, remova AllowAnonymous.
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Create()
        {
            return View();
        }

        // POST: /Chamados/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous] // permite que usuário não autenticado abra chamado
        public async Task<IActionResult> Create([Bind("Nome,Email,Telefone,Urgencia,Descricao")] Chamado model, IFormFile? anexo)
        {
            // validações de entrada
            if (string.IsNullOrWhiteSpace(model.Nome))
                ModelState.AddModelError(nameof(model.Nome), "Nome é obrigatório.");

            // Se usuário estiver autenticado, preferimos usar o email do claim
            var currentUserEmail = GetUserEmail(User);
            if (!string.IsNullOrWhiteSpace(currentUserEmail))
            {
                // sobrescreve email informado no form com o do claim (garante propriedade do chamado)
                model.Email = currentUserEmail;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(model.Email))
                    ModelState.AddModelError(nameof(model.Email), "E-mail é obrigatório.");
            }

            if (string.IsNullOrWhiteSpace(model.Descricao))
                ModelState.AddModelError(nameof(model.Descricao), "Descrição é obrigatória.");

            if (!ModelState.IsValid)
            {
                // retorna com erros visíveis na view
                return View(model);
            }

            try
            {
                model.DataAbertura ??= DateTime.UtcNow;
                model.Status ??= "Aberto";

                if (anexo != null && anexo.Length > 0)
                {
                    var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                    var uploadsFolder = Path.Combine(webRoot, "uploads");
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    var ext = Path.GetExtension(anexo.FileName);
                    var safeFileName = $"{Guid.NewGuid():N}{ext}";
                    var filePath = Path.Combine(uploadsFolder, safeFileName);

                    await using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await anexo.CopyToAsync(stream);
                    }

                    model.Anexo = $"/uploads/{safeFileName}";
                }

                _logger.LogInformation("Tentando salvar chamado: Nome={Nome}, Email={Email}, Telefone={Telefone}", model.Nome, model.Email, model.Telefone);

                // Cria o chamado e captura o id retornado pelo service
                var newId = await _chamadoService.CreateAsync(model);

                if (newId > 0)
                {
                    TempData["Message"] = $"Chamado aberto com sucesso! Código: {newId}";
                    _logger.LogInformation("Chamado {Id} criado por {Nome} / {Email}", newId, model.Nome, model.Email);

                    // permanece na view de detalhes do chamado criado
                    return RedirectToAction(nameof(Details), new { id = newId });
                }
                else
                {
                    // Serviço não retornou id — log e mensagem ao usuário
                    _logger.LogWarning("CreateAsync retornou id inválido ({Id}) ao tentar criar chamado para {Email}", newId, model.Email);
                    ModelState.AddModelError(string.Empty, "Não foi possível abrir o chamado. Tente novamente mais tarde.");
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar chamado");
                ModelState.AddModelError(string.Empty, "Erro ao abrir chamado. Tente novamente mais tarde.");
                return View(model);
            }
        }

        // GET: /Chamados/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var item = await _chamadoService.GetByIdAsync(id);
                if (item == null) return NotFound();

                // segurança extra: se usuário não for técnico só permita ver se for dono do chamado
                if (!IsUserTecnico(User))
                {
                    var email = GetUserEmail(User);
                    if (!string.Equals(email, item.Email, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("Usuário {User} tentou acessar chamado {Id} de outro e-mail.", User?.Identity?.Name, id);
                        return Forbid();
                    }
                }

                return View(item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter detalhes do chamado {Id}", id);
                return StatusCode(500);
            }
        }

        // GET: /Chamados/DownloadAnexo?path=/uploads/abc.pdf
        [HttpGet]
        public IActionResult DownloadAnexo(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return NotFound();

            var normalized = path.Replace('\\', '/').Trim();
            if (!normalized.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase) &&
                !normalized.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Tentativa de download de caminho inválido: {Path}", path);
                return NotFound();
            }

            var relative = normalized.StartsWith("/") ? normalized.Substring(1) : normalized;
            var full = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), relative);

            if (!System.IO.File.Exists(full))
            {
                _logger.LogWarning("Arquivo de anexo não encontrado: {FullPath}", full);
                return NotFound();
            }

            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(full, out var contentType))
            {
                contentType = "application/octet-stream";
            }

            var downloadName = Path.GetFileName(full);

            // Security: ensure only authenticated users can download; additionally allow only owner or technician
            if (!IsUserTecnico(User))
            {
                var email = GetUserEmail(User);
                var possible = _chamadoService.GetAllAsync().Result.FirstOrDefault(c =>
                    string.Equals(c.Anexo?.Replace("\\", "/"), normalized, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(c.Email, email, StringComparison.OrdinalIgnoreCase));
                if (possible == null)
                {
                    _logger.LogWarning("Usuário {User} tentou baixar anexo sem permissão: {Path}", User?.Identity?.Name, path);
                    return Forbid();
                }
            }

            return PhysicalFile(full, contentType, downloadName);
        }

        // POST: /Chamados/Responder (técnico responde, atualiza resposta e status)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Responder(int id, string respostaTecnico)
        {
            if (id <= 0 || string.IsNullOrWhiteSpace(respostaTecnico))
            {
                TempData["Message"] = "Dados inválidos para resposta.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // somente técnico pode responder
            if (!IsUserTecnico(User))
            {
                TempData["Message"] = "Apenas técnicos podem responder chamados.";
                return Forbid();
            }

            try
            {
                var ok = await _chamadoService.UpdateRespostaAsync(id, respostaTecnico, "Respondido");
                TempData["Message"] = ok ? "Resposta enviada com sucesso." : "Não foi possível salvar a resposta. Tente novamente.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar resposta do técnico para chamado {Id}", id);
                TempData["Message"] = "Erro ao salvar resposta. Contate o administrador.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        // -----------------------
        // Helpers locais
        // -----------------------
        private static bool IsUserTecnico(ClaimsPrincipal user)
        {
            if (user?.Identity?.IsAuthenticated != true) return false;

            var perfil = user.FindFirst("Perfil")?.Value ?? user.FindFirst("perfil")?.Value;
            if (!string.IsNullOrEmpty(perfil) && perfil.Equals("Tecnico", StringComparison.OrdinalIgnoreCase)) return true;

            if (user.IsInRole("Tecnico")) return true;

            var roleClaim = user.FindFirst(ClaimTypes.Role)?.Value ?? user.FindFirst("role")?.Value
                            ?? user.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value;
            if (!string.IsNullOrEmpty(roleClaim) && roleClaim.Equals("Tecnico", StringComparison.OrdinalIgnoreCase)) return true;

            var allPerfil = user.Claims
                                .Where(c => string.Equals(c.Type, "Perfil", StringComparison.OrdinalIgnoreCase) ||
                                            string.Equals(c.Type, "perfil", StringComparison.OrdinalIgnoreCase))
                                .Select(c => c.Value)
                                .FirstOrDefault();
            if (!string.IsNullOrEmpty(allPerfil))
            {
                if (allPerfil.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                             .Any(v => v.Trim().Equals("Tecnico", StringComparison.OrdinalIgnoreCase)))
                    return true;
            }

            return false;
        }

        private static string? GetUserEmail(ClaimsPrincipal user)
        {
            if (user == null) return null;
            var email = user.FindFirst(ClaimTypes.Email)?.Value
                        ?? user.FindFirst("email")?.Value
                        ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return email;
        }
    }
}
