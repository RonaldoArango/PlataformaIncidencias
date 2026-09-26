using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlataformaIncidencias.Data;

namespace PlataformaIncidencias.Controllers
{
    [Authorize]
    public class OperacionesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OperacionesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Incidencias()
        {
            var incidencias = await _context.Incidencias
                .Where(x => x.Estado == "Abierta")
                .ToListAsync();

            return View(incidencias);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cerrar(int id)
        {
            var incidencia = await _context.Incidencias.FindAsync(id);

            if (incidencia == null)
                return NotFound();

            incidencia.Estado = "Cerrada";

            await _context.SaveChangesAsync();

            TempData["Mensaje"] = "La incidencia fue cerrada correctamente.";

            return RedirectToAction(nameof(Incidencias));
        }
    }
}