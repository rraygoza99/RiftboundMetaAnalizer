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
builder.Services.AddScoped<ICardScraper, RiftboundMetaAnalizer.Infrastructure.Scraping.CardScraper>();
builder.Services.AddValidatorsFromAssemblyContaining<DeckValidator>();

builder.Services.AddControllers();
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

app.MapControllers();

app.Run();