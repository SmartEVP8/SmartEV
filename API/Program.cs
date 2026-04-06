using API;
using API.Services;
using OpenApiUi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IWebSocketService, WebSocketService>();
builder.Services.AddSingleton<ISimulationStateService, SimulationStateService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseOpenApiUi(config =>
    {
        config.OpenApiSpecPath = "/openapi/v1.json";
    });
}

app.UseHttpsRedirection();
app.UseWebSockets();
app.MapSimulationEndpoints();
app.Run();
