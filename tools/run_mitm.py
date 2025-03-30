"""
mitmproxy addon for intercepting and modifying HTTP responses based on database targets.
Works with system Python without embedded dependencies.
"""

import os
import sys
import json
import traceback
from pathlib import Path

# Try direct import first (when run from the tools directory)
try:
    from mitm_modular.mitm_core import InterceptAddon
except ImportError:
    # Add parent directory to path
    tools_dir = os.path.dirname(os.path.abspath(__file__))
    modular_dir = os.path.join(tools_dir, "mitm_modular")
    
    if modular_dir not in sys.path:
        sys.path.append(modular_dir)
    
    print(f"Added {modular_dir} to Python path")
    
    try:
        from mitm_modular.mitm_core import InterceptAddon
    except ImportError as e:
        print(f"ERROR: Failed to import InterceptAddon: {e}")
        print(f"Python path: {sys.path}")
        sys.exit(1)

# Get the database path in the tools directory
def get_db_path():
    """Get the path to the targets database."""
    try:
        # Get the directory of this script
        script_dir = os.path.dirname(os.path.abspath(__file__))
        db_path = os.path.join(script_dir, 'targets.db')
        return db_path
    except Exception as e:
        print(f"Error getting database path: {e}")
        return 'targets.db'  # Default fallback

# Create an instance of the InterceptAddon with the database path
addons = [
    InterceptAddon(get_db_path())
]

# For testing when run directly
if __name__ == "__main__":
    print("This script is designed to be used as a mitmproxy addon.")
    print("To run it, use: mitmproxy -s run_mitm.py")
    print(f"Database path: {get_db_path()}")
    sys.exit(0)