using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi.Models;
using Data;
using Service;
using Models;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

// === Swagger ===
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ExpCliente API",
        Version = "v1",
        Description = "API de estabelecimentos com seguran�a via X-API-KEY e sess�o"
    });

    // Header: X-API-KEY
    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "X-API-KEY",
        Description = "Cole aqui sua X-API-KEY"
    });

    // Header: X-SESSION-TOKEN
    c.AddSecurityDefinition("SessionToken", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "X-SESSION-TOKEN",
        Description = "Token de sess�o retornado no login"
    });

    // Exigir ambos por padr�o (para rotas /api)
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" }
            },
            Array.Empty<string>()
        },
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "SessionToken" }
            },
            Array.Empty<string>()
        }
    });
});

// === Configura��es ===
builder.Services.Configure<ApiKey.Options>(builder.Configuration.GetSection("ApiKeyAuth"));

var conn =
    builder.Configuration.GetConnectionString("Default") ??
    builder.Configuration.GetConnectionString("DefaultConnection") ??
    builder.Configuration["ConnectionStrings:Default"] ??
    builder.Configuration["ConnectionStrings:DefaultConnection"];

builder.Services.AddDbContext<AppDbContext>(options =>
    options
        .UseNpgsql(conn)
        .UseSnakeCaseNamingConvention()
);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();
builder.Services.AddScoped<Services.CreditService>();
builder.Services.AddHttpContextAccessor();

StripeConfiguration.ApiKey = builder.Configuration["StripeSettings:SecretKey"];


builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // Se você souber o IP do proxy, configure aqui (mais seguro):
    // o.KnownProxies.Add(System.Net.IPAddress.Parse("172.18.0.2"));

    // OU, se estiver em ambiente controlado e quiser liberar geral (menos seguro):
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

// Seguran�a SOMENTE nas rotas /api
app.UseForwardedHeaders();

app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/api"), branch =>
{
    branch.UseMiddleware<ApiKeyMiddleware>();
    branch.UseMiddleware<SessionAuthMiddleware>();
});

app.UseForwardedHeaders();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ExpCliente API v1");
    c.RoutePrefix = "swagger";
});


app.UseExceptionHandler("/error");

app.UseStaticFiles();
app.UseRouting();

// N�O usar Authorization sem Authentication configurado
// app.UseAuthorization();
// N�O registrar SessionAuth aqui (j� est� dentro do branch /api)
// app.UseMiddleware<SessionAuthMiddleware>();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}
app.Run();


