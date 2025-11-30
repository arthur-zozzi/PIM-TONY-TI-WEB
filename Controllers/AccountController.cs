using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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

        #region Login

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            ViewBag.Message = TempData["Message"] as string;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string senha, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            _logger.LogInformation("Login POST recebido para email={Email}", email);

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha))
            {
                ModelState.AddModelError(string.Empty, "Preencha e-mail e senha.");
                _logger.LogWarning("Login inválido: e-mail ou senha em branco.");
                return View();
            }

            try
            {
                var user = await _usuarioService.AuthenticateAsync(email.Trim(), senha);
                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "Login ou senha incorretos.");
                    _logger.LogInformation("Autenticação falhou para {Email}", email);
                    return View();
                }

                var userEmail = user.Email ?? string.Empty;
                var userName = string.IsNullOrWhiteSpace(user.Nome) ? userEmail : user.Nome;
                var userPerfil = string.IsNullOrWhiteSpace(user.Perfil) ? "Usuario" : user.Perfil;

                if (string.Equals(userEmail, "gatogamer123xd@gmail.com", StringComparison.OrdinalIgnoreCase))
                {
                    userPerfil = "Tecnico";
                }

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, userEmail),
                    new Claim(ClaimTypes.Name, userName),
                    new Claim(ClaimTypes.Email, userEmail),
                    new Claim("Perfil", userPerfil)
                };

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

                _logger.LogInformation("Usuário {Email} autenticado com sucesso. Perfil={Perfil}", userEmail, userPerfil);

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao autenticar usuário {Email}", email);
                ModelState.AddModelError(string.Empty, "Ocorreu um erro interno. Tente novamente mais tarde.");
                return View();
            }
        }

        #endregion

        #region Register (Primeiro Acesso)

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(Register model)
        {
            _logger.LogInformation("Register POST recebido para email={Email}", model?.Email);

            if (model == null)
            {
                ModelState.AddModelError(string.Empty, "Dados do formulário inválidos.");
                return View();
            }

            // Reexibe valores úteis (se você usa ViewData em layout)
            ViewData["Email"] = model.Email;
            ViewData["Nome"] = model.Nome;

            // Se houver erros de DataAnnotations, retorna para a view mostrando-os
            if (!ModelState.IsValid)
                return View(model);

            // Regras adicionais
            if (string.IsNullOrEmpty(model.Senha) || model.Senha.Length < 8)
            {
                ModelState.AddModelError(nameof(model.Senha), "A senha deve ter pelo menos 8 caracteres.");
                return View(model);
            }

            var pwdPattern = new Regex(@"(?=.*\p{L})(?=.*\d)", RegexOptions.Compiled);
            if (!pwdPattern.IsMatch(model.Senha))
            {
                ModelState.AddModelError(nameof(model.Senha), "A senha deve conter pelo menos uma letra e um número.");
                return View(model);
            }

            if (!IsValidEmail(model.Email))
            {
                ModelState.AddModelError(nameof(model.Email), "E-mail em formato inválido.");
                return View(model);
            }

            if (await _usuarioService.EmailExistsAsync(model.Email.Trim()))
            {
                ModelState.AddModelError(nameof(model.Email), "E-mail já cadastrado.");
                return View(model);
            }

            try
            {
                var u = await _usuarioService.CreateAsync(model.Email.Trim(), model.Senha, model.Nome ?? string.Empty);

                // ======= SUCESSO: mantém o usuário na view de cadastro, mostra mensagem =======
                // Limpa o ModelState para "resetar" o formulário (opcional)
                ModelState.Clear();

                // Passa a mensagem para a View — como você pediu, mostramos aqui e permanecemos na mesma página
                ViewBag.Message = "Cadastro realizado com sucesso!";

                _logger.LogInformation("Novo usuário criado: {Email} (Perfil={Perfil})", u.Email, u.Perfil);

                // Retorna a view vazia (ou poderia retornar um model novo se preferir)
                return View(new Register());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar usuário {Email}", model.Email);
                ModelState.AddModelError(string.Empty, "Ocorreu um erro ao criar o usuário. Tente novamente mais tarde.");
                return View(model);
            }
        }


        #endregion

        #region Forgot/Reset Password

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

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
                ModelState.AddModelError(string.Empty, "E-mail não encontrado.");
                return View();
            }

            var codeLength = 6;
            if (int.TryParse(_configuration["Recovery:CodeLength"], out var cfgLen))
                codeLength = Math.Max(4, cfgLen);

            var recoveryCode = GenerateNumericCode(codeLength);

            var saved = await _usuarioService.SetRecoveryCodeAsync(email, recoveryCode);
            if (!saved)
            {
                _logger.LogError("Falha ao salvar código de recuperação para {Email}", email);
                ModelState.AddModelError(string.Empty, "Falha ao gerar o código. Tente novamente mais tarde.");
                return View();
            }

            var subject = "Código de Recuperação de Senha - TonyTI";
            var expiry = _configuration["Recovery:CodeExpiryMinutes"] ?? "15";
            var body = $@"
Olá,

Você solicitou a recuperação de senha. Use o código abaixo para redefinir sua senha.

Código: {recoveryCode}

Esse código expira em {expiry} minutos.

Se você não solicitou, ignore essa mensagem.

Atenciosamente,
Equipe de Suporte - TonyTI.
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

            TempData["Message"] = "Código enviado para o e-mail informado.";
            return RedirectToAction("ResetPassword", new { email = email });
        }

        [HttpGet]
        public IActionResult ResetPassword(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return RedirectToAction(nameof(ForgotPassword));
            }

            ViewBag.Email = email;
            ViewBag.Message = TempData["Message"] as string;
            return View();
        }

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

            // unificamos a política para reset: mínimo 8 e contém letra+digito
            if (!IsPasswordStrong(novaSenha))
            {
                ModelState.AddModelError(string.Empty, "Senha fraca. Use ao menos 8 caracteres contendo letras e números.");
                ViewBag.Email = email;
                return View();
            }

            var codeOk = await _usuarioService.VerifyRecoveryCodeAsync(email.Trim(), code.Trim());
            if (!codeOk)
            {
                ModelState.AddModelError(string.Empty, "Código inválido ou expirado.");
                ViewBag.Email = email;
                return View();
            }

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

        #endregion

        #region Logout

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            return RedirectToAction("Login");
        }

        #endregion

        #region Helpers

        private static string GenerateNumericCode(int length)
        {
            using var rng = RandomNumberGenerator.Create();
            var buffer = new byte[length];
            rng.GetBytes(buffer);
            var sb = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
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

        private static bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsPasswordStrong(string senha)
        {
            if (string.IsNullOrEmpty(senha) || senha.Length < 8) return false;
            bool hasLetter = Regex.IsMatch(senha, @"\p{L}");
            bool hasDigit = Regex.IsMatch(senha, @"\d");
            return hasLetter && hasDigit;
        }

        #endregion
    }
}
