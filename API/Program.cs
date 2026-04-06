using API;
using API.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddLogging();
builder.Services.AddSingleton<ISimulationStateService, SimulationStateService>();
builder.Services.AddSingleton<EnvelopeWebSocketHandler>();
builder.Services.AddSingleton<IEnvelopeMessageHandler, SimulationMessageHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseWebSockets();

// Map WebSocket endpoint
app.MapSimulationWebSocket();

app.Run();
