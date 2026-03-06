FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# Install dependencies
RUN apt-get update && apt-get install -y \
    build-essential cmake pkg-config \
    libbz2-dev libxml2-dev libzip-dev \
    libboost-all-dev liblua5.2-dev libtbb-dev \
    libboost-all-dev libtbb-dev liblua5.4-dev \
    libxml2-dev libzip-dev libbz2-dev libexpat1-dev \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Build OSRM and process data
RUN curl -L https://github.com/Project-OSRM/osrm-backend/archive/362b388d7e0582291662105d7bfc004a3a44a393.tar.gz | tar -xz \
    && cd osrm-backend-362b388d7e0582291662105d7bfc004a3a44a393 \
    && mkdir -p build && cd build \
    && cmake .. -DCMAKE_BUILD_TYPE=Release \
    && make -j$(nproc) \
    && make install \
    && cd ../.. \
    && rm -rf osrm-backend-362b388d7e0582291662105d7bfc004a3a44a393

WORKDIR /app

COPY output.osm.pbf .
RUN mkdir -p data/osrm && \
    cp output.osm.pbf data/osrm/output.osm.pbf && \
    osrm-extract -p /usr/local/share/osrm/profiles/car.lua data/osrm/output.osm.pbf && \
    osrm-contract data/osrm/output.osm.pbf

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

# Copy .so from OSRM wrapper image
COPY --from=ghcr.io/smartevp8/osrm_wrapper:latest /build/build/libosrm_wrapper.so Core/native/

# Copy everything and build
COPY . .
RUN dotnet build --no-restore