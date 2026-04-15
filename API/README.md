# SmartEV API

Minimal WebSocket API that hosts the Engine and streams events to a single connected client.

## Run

From the repo root:

- `cd SmartEV`
- `dotnet run --project API/API.csproj`

The API is configured to listen on:

- `http://localhost:5000`

## Regenerating protobuf code

If you change the protocol:

- `cd SmartEV`
- `buf generate`

Generates code to API/Generated
