
import os
import sys

# Add the current directory to Python's path
current_dir = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, current_dir)

# Now import and run the actual module
try:
    from mitm_modular.cli import main
    import sys
    # Pass command line arguments to main
    if __name__ == '__main__':
        sys.argv = sys.argv[1:]  # Remove the script name
        main()
except ImportError as e:
    print(f'Import error: {e}')
    print(f'Python path: {sys.path}')
    print(f'Current directory: {current_dir}')
    sys.exit(1)
