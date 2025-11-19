using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace TonyTI_Web.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        public HomeController(ILogger<HomeController> logger) => _logger = logger;

        public IActionResult Index()
        {
            _logger.LogInformation("Home/Index acessada por {User}", User?.Identity?.Name ?? "anônimo");
            return View();
        }

        // se quiser página de Ajuda aqui pode adicionar:
        [AllowAnonymous]
        public IActionResult Help()
        {
            return View(); // opcional: criar Views/Home/Help.cshtml
        }
    }
}
