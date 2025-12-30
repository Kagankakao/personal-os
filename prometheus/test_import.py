import sys
import os

print(f"Python Executable: {sys.executable}")
print(f"Python Version: {sys.version}")
print("--- sys.path ---")
for p in sys.path:
    print(p)
print("----------------")

try:
    import grpc
    print("SUCCESS: grpc imported")
except ImportError as e:
    print(f"FAIL: grpc import failed: {e}")

try:
    import numpy
    print("SUCCESS: numpy imported")
except ImportError as e:
    print(f"FAIL: numpy import failed: {e}")

try:
    import fastembed
    print("SUCCESS: fastembed imported")
except ImportError as e:
    print(f"FAIL: fastembed import failed: {e}")
except Exception as e:
    print(f"FAIL: fastembed encountered unexpected error: {e}")
