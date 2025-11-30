using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace TonyTI_Web.Controllers
{
    // Todas as actions exigem autenticação por padrão
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        
        // TELA PRINCIPAL (LOGADO)
        // 
        [HttpGet]
        public IActionResult Index()
        {
            _logger.LogInformation("Home/Index acessada por {User}", User?.Identity?.Name ?? "Anônimo");
            return View();
        }

         
        // PÁGINA DE PRIVACIDADE (PÚBLICA)

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Privacy()
        {
            _logger.LogInformation("Home/Privacy acessada (público/anônimo permitido).");
            return View();
        }

        [HttpGet]
        public IActionResult Help()
        {
            _logger.LogInformation("Home/Help acessada.");
            return View(); // Views/Home/Help.cshtml (integrada ao ChatGPT)
        }


        // PÁGINA DE ERRO 
        
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Error()
        {
            Response.StatusCode = 500;
            _logger.LogWarning("Página de erro exibida (500).");
            return View(); // Views/Home/Error.cshtml
        }
    }
}