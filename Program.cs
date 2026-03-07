using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using FluentValidation;
using RiftboundMetaAnalizer.Application;
using RiftboundMetaAnalizer.Domain;
using RiftboundMetaAnalizer.Infrastructure.Data;
using RiftboundMetaAnalizer.Infrastructure.Scraping;

using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

if (string.IsNullOrEmpty(builder.Environment.EnvironmentName))
{
    builder.Environment.EnvironmentName = "Development";
}

builder.Services.AddDbContext<RiftContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"))
           .EnableDetailedErrors()
           .EnableSensitiveDataLogging());

var redisConfig = builder.Configuration.GetConnectionString("Redis")
                  ?? builder.Configuration["Redis:ConnectionString"];
if (!string.IsNullOrEmpty(redisConfig))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConfig;
        options.InstanceName = "Riftbound_";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddScoped<IDeckService, DeckService>();
builder.Services.AddScoped<IMetaService, MetaService>();
builder.Services.AddScoped<ITournamentScraper, TournamentScraper>();
builder.Services.AddScoped<ICardScraper, CardScraper>();
builder.Services.AddValidatorsFromAssemblyContaining<DeckValidator>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowUI", policy =>
    {
        var origins = new List<string> { "http://localhost:5173" };
        var extra = builder.Configuration["AllowedOrigins"];
        if (!string.IsNullOrEmpty(extra))
        {
            origins.AddRange(extra.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
        policy.WithOrigins(origins.ToArray())
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<RiftContext>();
    await context.Database.MigrateAsync();
    var filePath = Path.Combine(AppContext.BaseDirectory, "src", "Infrastructure", "Data", "Riftbound - All Card Info - All Card Data.csv");
    //await DataSeeder.SeedAsync(context, filePath);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowUI");
app.MapControllers();

app.Run();