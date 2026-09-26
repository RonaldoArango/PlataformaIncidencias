using Algolia.Search.Clients;
using Algolia.Search.Models.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlataformaIncidencias.Data;
using PlataformaIncidencias.Models;

namespace PlataformaIncidencias.Controllers
{
    [Authorize]
    public class OperacionesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public OperacionesController(
            ApplicationDbContext context,
            IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<IActionResult> Incidencias(string? busqueda)
        {
            List<Incidencia> incidencias;

            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                var applicationId = _configuration["Algolia:ApplicationId"];
                var apiKey = _configuration["Algolia:ApiKey"];
                var indexName = _configuration["Algolia:IndexName"];

                var client = new SearchClient(applicationId!, apiKey!);

                var searchParams = new SearchParams(
                    new SearchParamsObject
                    {
                        Query = busqueda
                    }
                );

                var respuesta =
                    await client.SearchSingleIndexAsync<Incidencia>(
                        indexName!,
                        searchParams
                    );

                incidencias = respuesta.Hits
                    .Where(x => x.Estado == "Abierta")
                    .ToList();
            }
            else
            {
                incidencias = await _context.Incidencias
                    .Where(x => x.Estado == "Abierta")
                    .ToListAsync();
            }

            ViewBag.Busqueda = busqueda;

            return View(incidencias);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cerrar(int id)
        {
            var incidencia = await _context.Incidencias.FindAsync(id);

            if (incidencia != null)
            {
                incidencia.Estado = "Cerrada";
                await _context.SaveChangesAsync();

                TempData["Mensaje"] =
                    "La incidencia fue cerrada correctamente.";
            }

            return RedirectToAction(nameof(Incidencias));
        }
    }
}