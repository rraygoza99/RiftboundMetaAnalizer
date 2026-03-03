using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using FluentValidation;
using RiftboundMetaAnalizer.Application;
using RiftboundMetaAnalizer.Domain;
using RiftboundMetaAnalizer.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// 1. Register Infrastructure (Database & Caching)
builder.Services.AddDbContext<RiftContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "Riftbound_";
});

// 2. Register Application Services
builder.Services.AddScoped<IDeckService, DeckService>();
builder.Services.AddScoped<IMetaService, MetaService>();
builder.Services.AddValidatorsFromAssemblyContaining<DeckValidator>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Seed the database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<RiftContext>();
    await context.Database.MigrateAsync();
    var filePath = Path.Combine(AppContext.BaseDirectory, "src", "Infrastructure", "Data", "Riftbound - All Card Info - All Card Data.csv");
    await DataSeeder.SeedAsync(context, filePath);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 3. The Deck Submission Endpoint
app.MapPost("/api/decks", async (Deck deck, IDeckService deckService) =>
{
    var result = await deckService.CreateDeckAsync(deck);

    return result.IsSuccess 
        ? Results.Created($"/api/decks/{result.Value}", result.Value) 
        : Results.BadRequest(new { Errors = result.Errors });
})
.WithName("CreateDeck");

app.MapGet("/api/meta/champion-synergy/{championId}", async (string championId, IMetaService metaService) =>
{
    var result = await metaService.GetChampionSynergyAsync(championId);

    return result.IsSuccess
        ? Results.Ok(result.Value)
        : Results.NotFound(new { Errors = result.Errors });
})
.WithName("GetChampionSynergy");

app.Run();