using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TonyTI_Web.Models;
using TonyTI_Web.Services;

namespace TonyTI_Web.Controllers
{
    [Authorize]
    public class ChamadosController : Controller
    {
        private readonly IChamadoService _svc;
        public ChamadosController(IChamadoService svc) { _svc = svc; }

        public async Task<IActionResult> Index()
        {
            var list = await _svc.GetAllAsync();
            return View(list);
        }

        public async Task<IActionResult> Details(int id)
        {
            var item = await _svc.GetByIdAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }

        [HttpGet]
        public IActionResult Create() => View();

        [HttpPost]
        public async Task<IActionResult> Create(Chamado model)
        {
            if (!ModelState.IsValid) return View(model);
            var created = await _svc.CreateAsync(model);
            return RedirectToAction("Details", new { id = created.Id });
        }

        [HttpPost]
        public async Task<IActionResult> Responder(int id, string resposta)
        {
            var tecnico = User.Identity?.Name ?? "Técnico";
            var ok = await _svc.AddRespostaAsync(id, resposta, tecnico);
            if (!ok) return NotFound();
            return RedirectToAction("Details", new { id });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            await _svc.UpdateStatusAsync(id, status);
            return RedirectToAction("Details", new { id });
        }
    }
}
