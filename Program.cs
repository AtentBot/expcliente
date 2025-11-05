using Microsoft.EntityFrameworkCore;
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
        Description = "API de estabelecimentos com segurança via X-API-KEY e sessão"
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
        Description = "Token de sessão retornado no login"
    });

    // Exigir ambos por padrão (para rotas /api)
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

// === Configurações ===
builder.Services.Configure<ApiKey.Options>(builder.Configuration.GetSection("ApiKeyAuth"));

builder.Services.AddDbContext<AppDbContext>(options =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
        .UseSnakeCaseNamingConvention()
);

builder.Services.AddControllersWithViews();
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddScoped<Services.CreditService>();
builder.Services.AddHttpContextAccessor();

StripeConfiguration.ApiKey = builder.Configuration["StripeSettings:SecretKey"];

var app = builder.Build();

// Segurança SOMENTE nas rotas /api
app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/api"), branch =>
{
    branch.UseMiddleware<ApiKeyMiddleware>();
    branch.UseMiddleware<SessionAuthMiddleware>();
});

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ExpCliente API v1");
    c.RoutePrefix = "swagger";
});

app.UseExceptionHandler("/error");
app.UseHsts();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// NÃO usar Authorization sem Authentication configurado
// app.UseAuthorization();
// NÃO registrar SessionAuth aqui (já está dentro do branch /api)
// app.UseMiddleware<SessionAuthMiddleware>();


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

app.Run();


