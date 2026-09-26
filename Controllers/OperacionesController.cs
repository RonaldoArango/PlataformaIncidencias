using Algolia.Search.Clients;
using Algolia.Search.Models.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlataformaIncidencias.Data;
using PlataformaIncidencias.Models;
using StackExchange.Redis;
using System.Net.Http.Json;
using System.Text.Json;

namespace PlataformaIncidencias.Controllers
{
    [Authorize]
    public class OperacionesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        private const string CACHE_KEY = "incidencias-abiertas";

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
            List<Incidencia> incidencias;

            // =====================================================
            // BUSQUEDA: ALGOLIA DIRECTAMENTE, SIN CACHE REDIS
            // =====================================================
            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                Console.WriteLine("ALGOLIA: busqueda directa sin Redis");

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
                    });

                var respuesta =
                    await client.SearchSingleIndexAsync<Incidencia>(
                        indexName!,
                        searchParams);

                var idsAlgolia = respuesta.Hits
                    .Select(x => x.Id)
                    .ToList();

                // SQLite confirma cuáles continúan abiertas
                incidencias = await _context.Incidencias
                    .Where(x =>
                        x.Estado == "Abierta" &&
                        idsAlgolia.Contains(x.Id))
                    .ToListAsync();
            }
            else
            {
                // =================================================
                // LISTADO GENERAL: REDIS -> SQLITE
                // =================================================

                incidencias = new List<Incidencia>();

                try
                {
                    var redisConnection =
                        _configuration["Redis:ConnectionString"];

                    var redis =
                        await ConnectionMultiplexer.ConnectAsync(
                            redisConnection!);

                    var database = redis.GetDatabase();

                    var cache =
                        await database.StringGetAsync(CACHE_KEY);

                    if (cache.HasValue)
                    {
                        Console.WriteLine(
                            "REDIS HIT: listado obtenido desde Redis");

                        incidencias =
                            JsonSerializer.Deserialize<List<Incidencia>>(
                                cache.ToString()) ??
                            new List<Incidencia>();
                    }
                    else
                    {
                        Console.WriteLine(
                            "REDIS MISS: consultando SQLite");

                        incidencias =
                            await _context.Incidencias
                                .Where(x => x.Estado == "Abierta")
                                .ToListAsync();

                        var json =
                            JsonSerializer.Serialize(incidencias);

                        await database.StringSetAsync(
                            CACHE_KEY,
                            json,
                            TimeSpan.FromSeconds(60));

                        Console.WriteLine(
                            "REDIS: cache guardada por 60 segundos");
                    }

                    await redis.CloseAsync();
                }
                catch (Exception ex)
                {
                    // Si Redis falla, la aplicación continúa funcionando.
                    Console.WriteLine(
                        $"REDIS ERROR: {ex.Message}");

                    Console.WriteLine(
                        "BASE DE DATOS: usando SQLite");

                    incidencias =
                        await _context.Incidencias
                            .Where(x => x.Estado == "Abierta")
                            .ToListAsync();
                }
            }

            // Filtro de prioridad sobre el resultado
            if (!string.IsNullOrEmpty(prioridad))
            {
                incidencias = incidencias
                    .Where(x => x.Prioridad == prioridad)
                    .ToList();
            }

            ViewBag.Prioridad = prioridad;
            ViewBag.Busqueda = busqueda;

            ViewBag.Criticas =
                await _context.Incidencias.CountAsync(
                    x =>
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

            // 1. GUARDAR PRIMERO EN SQLITE
            incidencia.Estado = "Cerrada";

            await _context.SaveChangesAsync();

            Console.WriteLine(
                $"BD: incidencia {incidencia.Id} cerrada");

            // 2. INVALIDAR REDIS
            try
            {
                var redisConnection =
                    _configuration["Redis:ConnectionString"];

                var redis =
                    await ConnectionMultiplexer.ConnectAsync(
                        redisConnection!);

                var database = redis.GetDatabase();

                await database.KeyDeleteAsync(CACHE_KEY);

                Console.WriteLine(
                    "REDIS: cache invalidada");

                await redis.CloseAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"REDIS ERROR al invalidar: {ex.Message}");
            }

            // 3. PUBLICAR EN PIEHOST
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
                    $"PIESOCKET: {respuesta.StatusCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"PIESOCKET ERROR: {ex.Message}");
            }

            TempData["Mensaje"] =
                "La incidencia fue cerrada correctamente.";

            return RedirectToAction(nameof(Incidencias));
        }
    }
}