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

builder.Services.AddDbContext<RiftContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "Riftbound_";
});

builder.Services.AddScoped<IDeckService, DeckService>();
builder.Services.AddScoped<IMetaService, MetaService>();
builder.Services.AddScoped<ITournamentScraper, RiftboundMetaAnalizer.Infrastructure.Scraping.TournamentScraper>();
builder.Services.AddValidatorsFromAssemblyContaining<DeckValidator>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

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

app.MapPost("/api/tournaments/scrape", async ([FromBody] string url, ITournamentScraper scraper, RiftContext context) =>
{
    try
    {
        var results = await scraper.ScrapeTournamentAsync(url);
        
        context.TournamentResults.AddRange(results);
        await context.SaveChangesAsync();

        return Results.Ok(results);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
})
.WithName("ScrapeTournament");

app.Run();