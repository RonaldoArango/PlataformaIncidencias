using Algolia.Search.Clients;
using Algolia.Search.Models.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlataformaIncidencias.Data;
using PlataformaIncidencias.Models;
using System.Net.Http.Json;

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

            if (!string.IsNullOrEmpty(prioridad))
            {
                consulta = consulta
                    .Where(x => x.Prioridad == prioridad);
            }

            var incidencias = await consulta.ToListAsync();

            // Busqueda mediante Algolia
            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                var applicationId =
                    _configuration["Algolia:ApplicationId"];

                var apiKey =
                    _configuration["Algolia:ApiKey"];

                var indexName =
                    _configuration["Algolia:IndexName"];

                var client =
                    new SearchClient(applicationId!, apiKey!);

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

                // Solo conserva incidencias que siguen abiertas
                // en la base de datos.
                incidencias = incidencias
                    .Where(x => idsAlgolia.Contains(x.Id))
                    .ToList();
            }

            ViewBag.Prioridad = prioridad;
            ViewBag.Busqueda = busqueda;

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
            var incidencia =
                await _context.Incidencias.FindAsync(id);

            if (incidencia == null)
                return NotFound();

            // 1. Primero guardar el cambio en la base
            incidencia.Estado = "Cerrada";

            await _context.SaveChangesAsync();

            // 2. Luego publicar el evento en PieSocket
            try
            {
                var clusterId =
                    _configuration["PieSocket:ClusterId"];

                var apiKey =
                    _configuration["PieSocket:ApiKey"];

                var apiSecret =
                    _configuration["PieSocket:ApiSecret"];

                using var httpClient = new HttpClient();

                var url =
                    $"https://{clusterId}.piesocket.com/api/publish";

                var datos = new
                {
                    key = apiKey,
                    secret = apiSecret,
                    channelId = "incidencias",

                    message = new
                    {
                        eventName = "IncidenciaActualizada",

                        data = new
                        {
                            Id = incidencia.Id,
                            Estado = incidencia.Estado
                        }
                    }
                };

                var respuesta =
                    await httpClient.PostAsJsonAsync(url, datos);

                Console.WriteLine(
                    $"PieSocket: {respuesta.StatusCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Error PieSocket: {ex.Message}");
            }

            TempData["Mensaje"] =
                "La incidencia fue cerrada correctamente.";

            return RedirectToAction(nameof(Incidencias));
        }
    }
}