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

        public async Task<IActionResult> Incidencias(
            string? prioridad,
            string? busqueda)
        {
            var consulta = _context.Incidencias
                .Where(x => x.Estado == "Abierta")
                .AsQueryable();

            // Filtro por prioridad
            if (!string.IsNullOrEmpty(prioridad))
            {
                consulta = consulta.Where(x => x.Prioridad == prioridad);
            }

            var incidencias = await consulta.ToListAsync();

            // Busqueda mediante Algolia
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

                var idsAlgolia = respuesta.Hits
                    .Select(x => x.Id)
                    .ToList();

                // Conserva solo incidencias que siguen abiertas en SQLite
                incidencias = incidencias
                    .Where(x => idsAlgolia.Contains(x.Id))
                    .ToList();
            }

            ViewBag.Prioridad = prioridad;
            ViewBag.Busqueda = busqueda;

            // Indicador de incidencias criticas
            ViewBag.Criticas = await _context.Incidencias
                .CountAsync(x =>
                    x.Estado == "Abierta" &&
                    x.Prioridad == "Alta");

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

            TempData["Mensaje"] =
                "La incidencia fue cerrada correctamente.";

            return RedirectToAction(nameof(Incidencias));
        }
    }
}