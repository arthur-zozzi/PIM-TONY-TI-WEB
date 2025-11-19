using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using TonyTI_Web.Models;
using TonyTI_Web.Services;

namespace TonyTI_Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUsuarioService _usuarioService;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<AccountController> _logger;
        private readonly IConfiguration _configuration;

        public AccountController(
            IUsuarioService usuarioService,
            IEmailSender emailSender,
            ILogger<AccountController> logger,
            IConfiguration configuration)
        {
            _usuarioService = usuarioService;
            _emailSender = emailSender;
            _logger = logger;
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            // se vier uma mensagem via TempData (ex: após reset), mostramos na view
            ViewBag.Message = TempData["Message"] as string;
            return View();
        }

        // ... seu POST Login existente aqui (não alterado) ...

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        // POST: /Account/ForgotPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError(string.Empty, "Informe o e-mail.");
                return View();
            }

            email = email.Trim();
            var exists = await _usuarioService.EmailExistsAsync(email);
            if (!exists)
            {
                // mostra imediatamente que o e-mail não existe (conforme solicitado)
                ModelState.AddModelError(string.Empty, "E-mail não encontrado.");
                return View();
            }

            // gera código de recuperação (numérico)
            var codeLength = 6;
            if (int.TryParse(_configuration["Recovery:CodeLength"], out var cfgLen))
                codeLength = Math.Max(4, cfgLen);

            var recoveryCode = GenerateNumericCode(codeLength);

            // salva o código no banco (recuperacaoSenha)
            var saved = await _usuarioService.SetRecoveryCodeAsync(email, recoveryCode);
            if (!saved)
            {
                _logger.LogError("Falha ao salvar código de recuperação para {Email}", email);
                ModelState.AddModelError(string.Empty, "Falha ao gerar o código. Tente novamente mais tarde.");
                return View();
            }

            // envia e-mail com o código
            var subject = "Código de Recuperação de Senha";
            var expiry = _configuration["Recovery:CodeExpiryMinutes"] ?? "15";
            var body = $@"
                Olá,

                Você solicitou a recuperação de senha. Use o código abaixo para redefinir sua senha.
                
                Código: {recoveryCode}

                Esse código expira em {expiry} minutos.

                Se você não solicitou, ignore essa mensagem.
                
                Atenciosamente, Equipe de Suporte - TonyTI.
            ";

            try
            {
                await _emailSender.SendEmailAsync(email, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao enviar e-mail de recuperação para {Email}", email);
                ModelState.AddModelError(string.Empty, "Não foi possível enviar o e-mail com o código. Contate o administrador.");
                return View();
            }

            // redireciona para a página de inserção do código (ResetPassword)
            TempData["Message"] = "Código enviado para o e-mail informado.";
            return RedirectToAction("ResetPassword", new { email = email });
        }

        [HttpGet]
        public IActionResult ResetPassword(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                // se for acessada sem email, redireciona para ForgotPassword
                return RedirectToAction(nameof(ForgotPassword));
            }

            // a view exibirá um formulário para código + nova senha
            ViewBag.Email = email;
            ViewBag.Message = TempData["Message"] as string;
            return View();
        }

        // POST: /Account/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string email, string code, string novaSenha, string confirmarSenha)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
            {
                ModelState.AddModelError(string.Empty, "Informe e-mail e código.");
                ViewBag.Email = email;
                return View();
            }

            if (string.IsNullOrWhiteSpace(novaSenha) || string.IsNullOrWhiteSpace(confirmarSenha))
            {
                ModelState.AddModelError(string.Empty, "Preencha a nova senha e a confirmação.");
                ViewBag.Email = email;
                return View();
            }

            if (novaSenha != confirmarSenha)
            {
                ModelState.AddModelError(string.Empty, "A senha e a confirmação não coincidem.");
                ViewBag.Email = email;
                return View();
            }

            // verifica o código
            var codeOk = await _usuarioService.VerifyRecoveryCodeAsync(email.Trim(), code.Trim());
            if (!codeOk)
            {
                ModelState.AddModelError(string.Empty, "Código inválido ou expirado.");
                ViewBag.Email = email;
                return View();
            }

            // computa hash da nova senha e atualiza
            var newHash = ComputeHash(novaSenha);
            var updated = await _usuarioService.UpdatePasswordWithHashAsync(email.Trim(), newHash);
            if (!updated)
            {
                _logger.LogError("Falha ao atualizar senha para {Email}", email);
                ModelState.AddModelError(string.Empty, "Não foi possível atualizar a senha. Tente novamente mais tarde.");
                ViewBag.Email = email;
                return View();
            }

            TempData["Message"] = "Senha alterada com sucesso. Faça login com a nova senha.";
            return RedirectToAction("Login");
        }

        // helpers
        private static string GenerateNumericCode(int length)
        {
            var rng = RandomNumberGenerator.Create();
            var buffer = new byte[length];
            rng.GetBytes(buffer);
            var sb = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                // 0..9
                sb.Append((buffer[i] % 10).ToString());
            }
            return sb.ToString();
        }

        private static string ComputeHash(string senha)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(senha ?? string.Empty);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }
}
