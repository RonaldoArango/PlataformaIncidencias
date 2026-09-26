using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlataformaIncidencias.Data;
using PlataformaIncidencias.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite("Data Source=incidencias.db"));

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<ApplicationDbContext>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    db.Database.EnsureCreated();

    var userManager =
        scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

    const string email = "supervisor@incidencias.com";
    const string password = "Supervisor123";

    var usuario = await userManager.FindByEmailAsync(email);

    if (usuario == null)
    {
        usuario = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        await userManager.CreateAsync(usuario, password);
    }

    if (!db.Incidencias.Any())
    {
        db.Incidencias.AddRange(
            new Incidencia
            {
                Estacion = "Estacion Central",
                Descripcion = "Bicicleta con freno averiado",
                Prioridad = "Alta",
                Estado = "Abierta"
            },
            new Incidencia
            {
                Estacion = "Estacion Norte",
                Descripcion = "Anclaje no libera bicicleta",
                Prioridad = "Media",
                Estado = "Abierta"
            },
            new Incidencia
            {
                Estacion = "Estacion Sur",
                Descripcion = "Pantalla de estacion apagada",
                Prioridad = "Baja",
                Estado = "Abierta"
            }
        );

        await db.SaveChangesAsync();
    }


}

app.Run();