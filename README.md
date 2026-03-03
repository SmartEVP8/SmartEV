## First time install
1. Install dependencies
    - ``sudo apt install dotnet10 build-essential git cmake pkg-config libbz2-dev libxml2-dev libzip-dev libboost-all-dev lua5.2 liblua5.2-dev libtbb-dev``
    - ``git lfs install``
2. In SmartEV/Core
    - Run ``git lfs pull``
    - Run ``./prepare_osrm.so``
3. In the OSRM_Wrapper repo
    - Run ``cmake -S . -B build && cmake --build build -j4``
    - Copy OSRM_Wrapper/build/libosrm_wrapper.so to SmartEV/Simulation/bin/Release

## If changes are made in C++ project 
1. In the OSRM_Wrapper repo
    - Run ``cmake -S . -B build && cmake --build build -j4``
    - Copy OSRM_Wrapper/build/libosrm_wrapper.so to SmartEV/Simulation/bin/Release

## Running the simulation
1. In SmartEV/Simulation
    - Run ``dotnet run -c Release``