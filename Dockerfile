FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# Install OSRM runtime dependencies to match the wrapper image
RUN apt-get update && apt-get install -y \
    libboost-all-dev \
    libtbb-dev \
    liblua5.4-dev \
    libxml2-dev \
    libzip-dev \
    libbz2-dev \
    libexpat1-dev \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Copy solution and restore
COPY EVSimulation.slnx .
COPY Directory.Packages.props .
COPY API/API.csproj API/
COPY Core/Core.csproj Core/
COPY Engine/Engine.csproj Engine/
COPY Headless/Headless.csproj Headless/
COPY Benchmark/API.Benchmark/API.Benchmark.csproj Benchmark/API.Benchmark/
COPY Benchmark/Core.Benchmark/Core.Benchmark.csproj Benchmark/Core.Benchmark/
COPY Benchmark/Engine.Benchmark/Engine.Benchmark.csproj Benchmark/Engine.Benchmark/
COPY Benchmark/Headless.Benchmark/Headless.Benchmark.csproj Benchmark/Headless.Benchmark/
COPY Tests/Headless.test/Headless.test.csproj Tests/Headless.test/
COPY Tests/API.test/API.test.csproj Tests/API.test/
COPY Tests/Engine.test/Engine.test.csproj Tests/Engine.test/
COPY Tests/Core.test/Core.test.csproj Tests/Core.test/
RUN dotnet restore

# Copy the .so from OSRM wrapper image
COPY --from=ghcr.io/smartevp8/osrm_wrapper:latest /build/build/libosrm_wrapper.so Core/native/

# Copy everything else including data/
COPY . .
RUN dotnet build --no-restore -c Release