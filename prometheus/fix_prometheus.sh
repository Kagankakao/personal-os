#!/bin/bash
# Script to fix Prometheus dependencies in MSYS2 UCRT64 environment.
# Run this inside your MSYS2 UCRT64 terminal.

echo ">>> [Check] Verifying MSYS2 Environment..."
LOG_FILE="prometheus/fix_log.txt"
exec > >(tee -a "$LOG_FILE") 2>&1

echo "LOG START: $(date)"
if [ "$MSYSTEM" != "UCRT64" ]; then
    echo "ERROR: You are not in the UCRT64 environment (Current: $MSYSTEM)."
    echo "Please close this terminal and open 'MSYS2 UCRT64'."
    exit 1
fi

echo ">>> [Phase 1] Syncing Package Databases..."
pacman -Sy --noconfirm

echo ">>> [Phase 2] Installing pre-built system dependencies via pacman..."
# Check if python is correct
python_path=$(which python)
echo "Using python at: $python_path"

# These are the heavy ones that fail in pip
PACKAGES=(
    mingw-w64-ucrt-x86_64-python-numpy
    mingw-w64-ucrt-x86_64-python-grpcio
    mingw-w64-ucrt-x86_64-python-grpcio-tools
    mingw-w64-ucrt-x86_64-python-protobuf
    mingw-w64-ucrt-x86_64-python-pydantic
    mingw-w64-ucrt-x86_64-python-httpx
    mingw-w64-ucrt-x86_64-python-anyio
    mingw-w64-ucrt-x86_64-python-fastapi
    mingw-w64-ucrt-x86_64-python-uvicorn
)

for pkg in "${PACKAGES[@]}"; do
    echo "Installing $pkg..."
    pacman -S --noconfirm --needed "$pkg" || { echo "ERROR: Failed to install $pkg. Are you running as Administrator?"; exit 1; }
done

echo ">>> [Phase 3] Configuring virtual environment..."
# Ensure venv can see system packages
if [ -f "prometheus/venv/pyvenv.cfg" ]; then
    sed -i 's/include-system-site-packages = false/include-system-site-packages = true/' "prometheus/venv/pyvenv.cfg"
    echo "Updated pyvenv.cfg to include system packages."
else
    echo "Generating new venv since old one not found or corrupted..."
    python -m venv prometheus/venv --system-site-packages
fi

echo ">>> [Phase 4] Installing search-specific packages via pip..."
# These are lighter and usually install fine with --no-deps if system has the rest
./prometheus/venv/bin/python.exe -m pip install qdrant-client fastembed --no-deps

echo ">>> [Phase 5] Verifying installation..."
./prometheus/venv/bin/python.exe -c "import grpc, numpy, qdrant_client, fastembed; print('SUCCESS: All Prometheus modules are now importable!')" || { echo "FAIL: Some modules are still missing. Check the errors above."; exit 1; }

echo "Prometheus environment repair complete."
