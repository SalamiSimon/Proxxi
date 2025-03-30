# Proxxi - MITM Proxy Tools

This directory contains Python scripts for the Proxxi application, which uses mitmproxy to intercept and modify HTTP responses.

## Prerequisites

- Python 3.8 or newer installed on your system
- mitmproxy package installed (`pip install mitmproxy`)

## Main Components

- `check_python.py` - Checks if Python and mitmproxy are available on the system
- `run_mitm.py` - The main script that runs with mitmproxy to intercept and modify HTTP traffic
- `mitm_modular/` - A Python package containing the core functionality:
  - `cli.py` - Command-line interface for managing targets
  - `database.py` - Database operations for storing and retrieving targets
  - `mitm_core.py` - Core interception and modification logic

## First Run

On first run, the application checks if Python is available on your system and asks to install mitmproxy if needed.

## Manual Setup

If automatic setup fails, you can manually set up the required dependencies:

1. Install Python from [python.org](https://www.python.org/downloads/)
2. Install mitmproxy: `pip install mitmproxy`
3. Run the application

## Tools Directory Structure

```
tools/
├── README.md               # This file
├── check_python.py         # Python environment checker
├── run_mitm.py             # Main mitmproxy script
├── targets.db              # SQLite database of targets
├── wrapper.py              # Python module import wrapper
└── mitm_modular/           # Core Python package
    ├── __init__.py
    ├── cli.py              # Command-line interface
    ├── database.py         # Database operations
    ├── mitm_core.py        # Core interception logic
    └── samples/            # Sample responses and code
``` 