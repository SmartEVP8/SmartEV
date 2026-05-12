#!/usr/bin/env bash
set -e

[ "${BASH_VERSINFO[0]}" -ge 4 ] || { echo "Bash 4+ required"; exit 1; }

DATA_DIR="data/osrm"
OSM_FILE="output.osm.pbf"
OSRM_BACKEND="osrm-backend"
OSRM_VERSION="26.4.1"
CAR_FILE="../car/car.lua"

declare -A PACKAGE_MAPS
PACKAGE_MAPS[arch]="base-devel git cmake pkgconf bzip2 libxml2 libzip boost boost-libs lua onetbb git-lfs"
PACKAGE_MAPS[debian]="build-essential git cmake pkg-config libbz2-dev libxml2-dev libzip-dev libboost-all-dev liblua5.4-dev libtbb-dev git-lfs"
PACKAGE_MAPS[macos]="cmake boost libxml2 libzip lua tbb git-lfs"
PACKAGE_MAPS[ubuntu]="build-essential git cmake pkg-config libbz2-dev libxml2-dev libzip-dev libboost-all-dev liblua5.4-dev libtbb-dev git-lfs"

declare -A CHECK_COMMANDS
CHECK_COMMANDS[arch]="pacman -Qi"
CHECK_COMMANDS[debian]="dpkg -l"
CHECK_COMMANDS[macos]="brew list"
CHECK_COMMANDS[ubuntu]="apt list --installed"

detect_os() {
    if [[ "$OSTYPE" == "darwin"* ]]; then
        echo "macos"
    elif [[ -f /etc/arch-release ]]; then
        echo "arch"
    elif [[ -f /etc/lsb-release ]]; then
        . /etc/lsb-release
        [[ "$DISTRIB_ID" == "Ubuntu" ]] && echo "ubuntu" || echo "unknown"
    elif [[ -f /etc/debian_version ]]; then
        echo "debian"
    else
        echo "unknown"
    fi
}

check_package() {
    local package="$1" os="$2"
    [[ -n "${CHECK_COMMANDS[$os]}" ]] && ${CHECK_COMMANDS[$os]} "$package" &>/dev/null
}

check_dependencies() {
    local os
    os=$(detect_os)
    echo "Detected OS: $os"
    [[ "$os" == "unknown" ]] && { echo "Unsupported OS"; exit 1; }

    local packages=(${PACKAGE_MAPS[$os]}) missing=()
    for package in "${packages[@]}"; do
        check_package "$package" "$os" || missing+=("$package")
    done

    [[ ${#missing[@]} -ne 0 ]] && { echo "Missing packages: ${missing[*]}"; exit 1; }
    echo "All dependencies satisfied."
}

check_dependencies

if [ ! -d "$OSRM_BACKEND" ]; then
    curl -L "https://github.com/Project-OSRM/osrm-backend/archive/v${OSRM_VERSION}.tar.gz" | tar -xz
    mv "osrm-backend-${OSRM_VERSION}" "$OSRM_BACKEND"
fi

if [ ! -f "$OSRM_BACKEND/build/osrm-extract" ]; then
    cd "$OSRM_BACKEND"
    mkdir -p build && cd build
    cmake .. -DCMAKE_BUILD_TYPE=Release \
        -DCMAKE_C_FLAGS='-include unistd.h -Wno-error=unused-variable -Wno-unused-variable' \
        -DCMAKE_CXX_FLAGS='-include unistd.h -Wno-error=unused-variable -Wno-unused-variable'
    make -j4
    sudo make install
    cd ../..
fi

mkdir -p "$DATA_DIR" && cd "$DATA_DIR"

if [ ! -f "../../$OSM_FILE" ]; then
    echo "OSM file not found: $OSM_FILE"
    exit 1
fi

if [ ! -f "$OSM_FILE" ]; then
    cp "../../$OSM_FILE" "$OSM_FILE"
fi

echo "Running OSRM extract..."
osrm-extract -p "$CAR_FILE" "$OSM_FILE"

echo "Running OSRM contract (CH pipeline)..."
osrm-contract "$OSM_FILE"

cd ../..
rm -rf "$OSRM_BACKEND"
echo "OSRM dataset preparation complete."