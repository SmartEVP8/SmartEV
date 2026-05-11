set -e

DATA_DIR="data/osrm"
OSM_FILE="output.osm.pbf"
OSRM_BACKEND="osrm-backend-362b388d7e0582291662105d7bfc004a3a44a393"
CAR_FILE="../car/car.lua"

# Package mappings for different systems
declare -A PACKAGE_MAPS
PACKAGE_MAPS[arch]="base-devel git cmake pkgconf bzip2 libxml2 libzip boost onetbb git-lfs"
PACKAGE_MAPS[debian]="build-essential git cmake pkg-config libbz2-dev libxml2-dev libzip-dev libboost-all-dev liblua5.2-dev libtbb-dev git-lfs"
PACKAGE_MAPS[macos]="cmake boost libxml2 libzip lua tbb git-lfs"
PACKAGE_MAPS[ubuntu]="build-essential git cmake pkg-config libbz2-dev libxml2-dev libzip-dev libboost-all-dev liblua5.2-dev libtbb-dev git-lfs"
# Package check commands
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
    elif [[ -f /etc/debian_version ]]; then
        echo "debian"
    elif [[ -f /etc/lsb-release ]]; then
        . /etc/lsb-release
        if [[ "$DISTRIB_ID" == "Ubuntu" ]]; then
            echo "ubuntu"
        else
            echo "unknown"
        fi
    else
        echo "unknown"
    fi
}

check_package() {
    local package="$1"
    local os="$2"

    if [[ -n "${CHECK_COMMANDS[$os]}" ]]; then
        ${CHECK_COMMANDS[$os]} "$package" &> /dev/null
    else
        return 1
    fi
}

check_dependencies() {
    echo "Checking dependencies..."

    local os=$(detect_os)
    echo "Detected OS: $os"

    if [[ "$os" == "unknown" ]]; then
        echo "Unsupported OS"
        exit 1
    fi

    local packages=(${PACKAGE_MAPS[$os]})
    local missing=()

    for package in "${packages[@]}"; do
        check_package "$package" "$os" || missing+=("$package")
    done

    if [ ${#missing[@]} -ne 0 ]; then
        echo "Missing packages: ${missing[*]}"
        exit 1
    fi

    echo "All dependencies satisfied."
}

check_dependencies
# Ensure source exists
if [ ! -d "$OSRM_BACKEND" ]; then
    curl -L https://github.com/Project-OSRM/osrm-backend/archive/362b388d7e0582291662105d7bfc004a3a44a393.tar.gz | tar -xz
fi

# Build if binary doesn't exist
if [ ! -f "$OSRM_BACKEND/build/osrm-extract" ]; then
    cd "$OSRM_BACKEND"
    mkdir -p build && cd build
    # Ensure POSIX prototypes (read/write/close/lseek) are visible to the build
    # by having the compiler implicitly include <unistd.h>. 
    # Also suppress unused-variable being promoted to error in third-party C files
    # (some compilers treat certain -Wno-* flags as unsupported; these are best-effort)
    cmake .. -DCMAKE_BUILD_TYPE=Release -DCMAKE_C_FLAGS='-include unistd.h -Wno-error=unused-variable -Wno-unused-variable' -DCMAKE_CXX_FLAGS='-include unistd.h -Wno-error=unused-variable -Wno-unused-variable'
    make -j4
    # Avoid blocking on an interactive sudo prompt during automated runs.
    if command -v sudo >/dev/null 2>&1; then
        sudo -n make install || echo "sudo install skipped or failed (non-interactive)"
    else
        echo "sudo not found; skipping make install"
    fi
    # locate any built osrm-extract binary so we can run it without requiring
    # a system install. Prefer build/osrm-extract if present, else search.
    if [ -x "$PWD/osrm-extract" ]; then
        OSRM_EXTRACT_BIN="$PWD/osrm-extract"
    else
        OSRM_EXTRACT_BIN=$(find "$PWD" -type f -executable -name osrm-extract -print -quit 2>/dev/null || true)
    fi
    cd ../..
fi

mkdir -p "$DATA_DIR" && cd "$DATA_DIR"

if [ ! -f "../../$OSM_FILE" ]; then
    echo "OSM file not found: $OSM_FILE"
    exit 1
fi

if [ ! -d "$OSM_FILE" ]; then
    cp "../../$OSM_FILE" "$OSM_FILE"
fi


echo "Running OSRM extract..."
if [ -n "$OSRM_EXTRACT_BIN" ] && [ -x "$OSRM_EXTRACT_BIN" ]; then
    "$OSRM_EXTRACT_BIN" -p "$CAR_FILE" "$OSM_FILE"
else
    osrm-extract -p "$CAR_FILE" "$OSM_FILE"
fi

echo "Running OSRM contract (CH pipeline)..."
osrm-contract "$OSM_FILE"

echo "Returning to project root..."
cd ../..
rm -rf "$OSRM_BACKEND"

echo "OSRM dataset preparation complete."
