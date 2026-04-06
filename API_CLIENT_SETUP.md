# SmartEV API Client Setup

The SmartEV API protocol is defined in [`api.proto`](./API/api.proto) and published to [buf.build/smartevp8/api](https://buf.build/smartevp8/api).

---

## For TypeScript/React Clients

### Install from buf.build registry

```bash
npm install @buf/smartevp8_api.bufbuild_es@latest
```

### Usage in React

```typescript
import { Envelope, InitRequest } from '@buf/smartevp8_api.bufbuild_es/smartev/api/v1/api_pb';

// Create a request
const request = new InitRequest({
  maximumEvs: 100,
  seed: 42,
  clientId: 'react-app',
});

// Wrap in Envelope
const envelope = new Envelope({
  requestId: crypto.randomUUID(),
  init: request,
});

// Send via WebSocket (binary)
const data = Envelope.toBinary(envelope);
webSocket.send(data);

// Receive response
webSocket.onmessage = (event) => {
  const response = Envelope.fromBinary(new Uint8Array(event.data));
  if (response.initResponse) {
    // Handle init response
  }
};
```

### Install peer dependency

The generated code requires `@bufbuild/protobuf`:

```bash
npm install @bufbuild/protobuf@^2.0.0
```

---

## For SmartEV Local Development (C#)

The SmartEV API project generates C# types locally:

```bash
cd SmartEV
buf generate
# C# output: API/Generated/Api.cs
```

---

## CI/CD Integration

The **GitHub Actions workflows** automatically:

1. **Pull requests** (`.github/workflows/buf-pull-request.yaml`):
   - Lints proto schema
   - Checks for breaking changes against published registry

2. **Merge to main** (`.github/workflows/publish-api.yml`):
   - Lints proto schema
   - Checks for breaking changes
   - Runs `buf push` to publish to buf.build registry

---

## Local Development

### Generate code locally for testing

```bash
# Generate all languages
buf generate

# C# output: API/Generated/Api.cs
# TypeScript output: gen/es/smartev/api/v1/api_pb.ts
```

### Commit strategy

- ✅ Commit: `api.proto`, `buf.yaml`, `buf.gen.yaml`, generated C# code (`API/Generated/`)
- ❌ Don't commit: Generated TypeScript (`gen/` — clients pull from buf.build)

---

## Troubleshooting

### "Cannot find package @buf/smartevp8_api.bufbuild_es"

- Ensure buf.build module is public
- Check `buf.yaml` has correct `name: buf.build/smartevp8/api`

### "BUF_TOKEN not found" in GitHub Actions

- Add `BUF_TOKEN` secret to repo settings (from buf.build account)
- Ensure token has permission to push to `buf.build/smartevp8/api` module

### Schema changes not available

- Check workflows ran successfully (`.github/workflows/buf-pull-request.yaml` or `.github/workflows/publish-api.yml`)
- Allow ~1 minute for buf.build to index the new commit
- Try clearing npm cache: `npm cache clean --force`
