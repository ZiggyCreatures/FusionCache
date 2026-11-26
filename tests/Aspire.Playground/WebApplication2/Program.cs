using Playground.Shared;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddPlaygroundCache(
    new PlaygroundCacheOptions("WebApplication2", "webApp2-sub", "app2:", TimeSpan.FromSeconds(300)),
    builder.Configuration.GetConnectionString("cache-redis"),
    builder.Configuration.GetConnectionString("cache-servicebus")
);

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "WebApplication2 API", Version = "v1" });
});

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () =>
{
	return Results.Redirect("/swagger");
});
app.UseHttpsRedirection();
app.MapPlaygroundCacheEndpoints();

app.Run();
