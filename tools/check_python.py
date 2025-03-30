import os
import sys
import subprocess
import json
import logging
from pathlib import Path

# Set up logging
logging.basicConfig(
    level=logging.DEBUG,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    handlers=[
        logging.StreamHandler(sys.stdout)
    ]
)
logger = logging.getLogger("check_python")

def find_system_python():
    """
    Detect system Python installation
    """
    python_locations = []
    
    try:
        logger.info("Searching for Python installations...")
        
        # Try the most common ways to find Python on Windows
        # 1. Check 'python' on PATH
        try:
            result = subprocess.run(
                ["where", "python"],
                capture_output=True,
                text=True,
                check=False
            )
            
            if result.returncode == 0:
                paths = result.stdout.strip().split('\n')
                for path in paths:
                    if path and os.path.exists(path) and path.endswith('.exe'):
                        logger.info(f"Found Python from PATH: {path}")
                        python_locations.append(path)
            else:
                logger.warning(f"'python' not found in PATH: {result.stderr}")
        except Exception as e:
            logger.error(f"Error checking 'python' in PATH: {e}")
        
        # 2. Check 'py' launcher on PATH
        try:
            result = subprocess.run(
                ["where", "py"],
                capture_output=True,
                text=True,
                check=False
            )
            
            if result.returncode == 0:
                paths = result.stdout.strip().split('\n')
                for path in paths:
                    if path and os.path.exists(path) and path.endswith('.exe'):
                        logger.info(f"Found Python launcher: {path}")
                        
                        # Try to get actual Python path from py launcher
                        try:
                            py_result = subprocess.run(
                                [path, "-3", "-c", "import sys; print(sys.executable)"],
                                capture_output=True,
                                text=True,
                                check=False
                            )
                            if py_result.returncode == 0:
                                actual_path = py_result.stdout.strip()
                                if actual_path and os.path.exists(actual_path):
                                    logger.info(f"Found Python via launcher: {actual_path}")
                                    python_locations.append(actual_path)
                        except Exception as py_err:
                            logger.error(f"Error getting Python path from launcher: {py_err}")
                        
                        # Also add the launcher itself
                        python_locations.append(path)
            else:
                logger.warning(f"'py' launcher not found in PATH: {result.stderr}")
        except Exception as e:
            logger.error(f"Error checking 'py' launcher in PATH: {e}")
        
        # 3. Check common Python installation directories
        common_dirs = [
            os.path.expandvars("%ProgramFiles%\\Python*"),
            os.path.expandvars("%ProgramFiles(x86)%\\Python*"),
            os.path.expandvars("%LocalAppData%\\Programs\\Python\\Python*"),
            os.path.expandvars("%AppData%\\Local\\Programs\\Python\\Python*"),
            os.path.expandvars("%UserProfile%\\AppData\\Local\\Programs\\Python\\Python*"),
            "C:\\Python*"
        ]
        
        logger.info("Checking common installation directories...")
        for pattern in common_dirs:
            logger.debug(f"Searching pattern: {pattern}")
            try:
                # Use Path().glob for the directories
                for dir_path in Path(os.path.dirname(pattern)).glob(os.path.basename(pattern)):
                    python_exe = os.path.join(dir_path, "python.exe")
                    if os.path.exists(python_exe):
                        logger.info(f"Found Python in common directory: {python_exe}")
                        python_locations.append(python_exe)
            except Exception as e:
                logger.error(f"Error searching {pattern}: {e}")
        
        # 4. Check Windows Registry
        try:
            import winreg
            logger.info("Checking Windows Registry for Python installations...")
            
            reg_paths = [
                r"SOFTWARE\Python\PythonCore",
                r"SOFTWARE\Wow6432Node\Python\PythonCore"
            ]
            
            for reg_path in reg_paths:
                try:
                    with winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, reg_path) as key:
                        for i in range(winreg.QueryInfoKey(key)[0]):
                            version = winreg.EnumKey(key, i)
                            try:
                                with winreg.OpenKey(key, f"{version}\\InstallPath") as path_key:
                                    install_path, _ = winreg.QueryValueEx(path_key, "")
                                    python_exe = os.path.join(install_path, "python.exe")
                                    if os.path.exists(python_exe):
                                        logger.info(f"Found Python {version} in registry: {python_exe}")
                                        python_locations.append(python_exe)
                            except Exception as e:
                                logger.error(f"Error accessing registry key for {version}: {e}")
                except Exception as e:
                    logger.error(f"Error accessing registry path {reg_path}: {e}")
                    
            # Check current user registry too
            for reg_path in reg_paths:
                try:
                    with winreg.OpenKey(winreg.HKEY_CURRENT_USER, reg_path) as key:
                        for i in range(winreg.QueryInfoKey(key)[0]):
                            version = winreg.EnumKey(key, i)
                            try:
                                with winreg.OpenKey(key, f"{version}\\InstallPath") as path_key:
                                    install_path, _ = winreg.QueryValueEx(path_key, "")
                                    python_exe = os.path.join(install_path, "python.exe")
                                    if os.path.exists(python_exe):
                                        logger.info(f"Found Python {version} in user registry: {python_exe}")
                                        python_locations.append(python_exe)
                            except Exception as e:
                                logger.error(f"Error accessing user registry key for {version}: {e}")
                except Exception as e:
                    logger.error(f"Error accessing user registry path {reg_path}: {e}")
        except ImportError:
            logger.warning("winreg module not available, skipping registry check")
        except Exception as e:
            logger.error(f"Error checking registry: {e}")
        
        # Remove duplicates and filter for valid installations
        python_locations = list(set(python_locations))
        logger.info(f"Found {len(python_locations)} potential Python installations before validation")
        
        valid_pythons = []
        for python_path in python_locations:
            try:
                # Check Python version
                logger.debug(f"Validating Python at: {python_path}")
                result = subprocess.run(
                    [python_path, "--version"],
                    capture_output=True,
                    text=True,
                    check=False,
                    timeout=5  # Add timeout to prevent hanging
                )
                
                if result.returncode == 0:
                    version_str = result.stdout.strip()
                    logger.info(f"Found valid Python: {python_path} - {version_str}")
                    
                    # Get detailed version info
                    version_result = subprocess.run(
                        [python_path, "-c", "import sys; import json; print(json.dumps({'version': list(sys.version_info), 'executable': sys.executable, 'path': sys.path}))"],
                        capture_output=True,
                        text=True,
                        check=False,
                        timeout=5  # Add timeout to prevent hanging
                    )
                    
                    if version_result.returncode == 0:
                        try:
                            version_info = json.loads(version_result.stdout.strip())
                            valid_pythons.append({
                                "path": python_path,
                                "version": version_info.get("version", [0, 0, 0]),
                                "version_str": version_str,
                                "details": version_info
                            })
                        except json.JSONDecodeError:
                            logger.error(f"Failed to parse version info for {python_path}")
                else:
                    logger.warning(f"Python at {python_path} returned error: {result.stderr}")
            except subprocess.TimeoutExpired:
                logger.warning(f"Timeout while checking Python at {python_path}")
            except Exception as e:
                logger.error(f"Error checking Python at {python_path}: {e}")
        
        # Sort by version, preferring Python 3.8+ but less than 3.12
        valid_pythons.sort(key=lambda x: (
            # Prefer Python 3.8-3.11
            0 if (x["version"][0] == 3 and 8 <= x["version"][1] <= 11) else 1,
            # Then sort by version (higher version first)
            -x["version"][0], -x["version"][1], -x["version"][2]
        ))
        
        logger.info(f"Found {len(valid_pythons)} valid Python installations")
        return valid_pythons
        
    except Exception as e:
        logger.error(f"Error finding system Python: {e}")
        return []

def check_mitmproxy(python_path):
    """
    Check if mitmproxy is installed for the given Python
    """
    try:
        # Check if mitmproxy module can be imported
        result = subprocess.run(
            [python_path, "-c", "import mitmproxy; print('mitmproxy installed')"],
            capture_output=True,
            text=True,
            check=False
        )
        
        # Also check if mitmdump works
        mitmdump_result = subprocess.run(
            [python_path, "-m", "mitmproxy.tools.main", "--version"],
            capture_output=True,
            text=True,
            check=False
        )
        
        return result.returncode == 0 and mitmdump_result.returncode == 0
    except Exception as e:
        logger.error(f"Error checking mitmproxy: {e}")
        return False

def install_mitmproxy(python_path):
    """
    Install mitmproxy using pip
    """
    try:
        logger.info(f"Installing mitmproxy using {python_path}")
        
        # First upgrade pip
        pip_upgrade_cmd = [
            python_path,
            "-m",
            "pip",
            "install",
            "--upgrade",
            "pip"
        ]
        
        logger.info(f"Upgrading pip: {' '.join(pip_upgrade_cmd)}")
        subprocess.run(
            pip_upgrade_cmd,
            check=False
        )
        
        # Install mitmproxy
        pip_install_cmd = [
            python_path,
            "-m",
            "pip",
            "install",
            "mitmproxy"
        ]
        
        logger.info(f"Installing mitmproxy: {' '.join(pip_install_cmd)}")
        result = subprocess.run(
            pip_install_cmd,
            capture_output=True,
            text=True,
            check=False
        )
        
        if result.returncode != 0:
            logger.error(f"Failed to install mitmproxy: {result.stderr}")
            return False
        
        logger.info(f"Installation output: {result.stdout}")
        
        # Verify installation
        if check_mitmproxy(python_path):
            logger.info("mitmproxy installed successfully")
            return True
        else:
            logger.error("mitmproxy installation verification failed")
            return False
        
    except Exception as e:
        logger.error(f"Error installing mitmproxy: {e}")
        return False

def main():
    """
    Main function - check Python and mitmproxy
    """
    result = {
        "python_found": False,
        "python_path": None,
        "python_version": None,
        "mitmproxy_installed": False,
        "mitmproxy_install_attempted": False,
        "mitmproxy_install_success": False,
        "error": None,
        "search_paths": [],
        "system_info": {
            "os": sys.platform,
            "path": os.environ.get("PATH", ""),
            "python_home": os.environ.get("PYTHONHOME", ""),
            "python_path": os.environ.get("PYTHONPATH", "")
        }
    }
    
    try:
        # Find system Python
        logger.info("Starting Python environment search...")
        python_installations = find_system_python()
        
        # Log what we found for diagnostic purposes
        for i, py_inst in enumerate(python_installations):
            logger.info(f"Found Python #{i+1}: {py_inst['path']} - {py_inst['version_str']}")
            result["search_paths"].append({
                "path": py_inst['path'],
                "version": py_inst['version_str']
            })
        
        if not python_installations:
            logger.error("No suitable Python installation found")
            result["error"] = "No suitable Python installation found"
            print_result(result)
            return result
        
        # Use the best Python installation
        best_python = python_installations[0]
        python_path = best_python["path"]
        
        result["python_found"] = True
        result["python_path"] = python_path
        result["python_version"] = best_python["version_str"]
        
        logger.info(f"Using Python at {python_path}")
        
        # Check if mitmproxy is already installed
        if check_mitmproxy(python_path):
            logger.info("mitmproxy is already installed")
            result["mitmproxy_installed"] = True
            print_result(result)
            return result
        
        # Try to install mitmproxy
        logger.info("mitmproxy not found, attempting to install...")
        result["mitmproxy_install_attempted"] = True
        
        if install_mitmproxy(python_path):
            result["mitmproxy_installed"] = True
            result["mitmproxy_install_success"] = True
        else:
            result["error"] = "Failed to install mitmproxy"
        
        print_result(result)
        return result
        
    except Exception as e:
        logger.error(f"Error in main function: {e}")
        result["error"] = str(e)
        print_result(result)
        return result

def print_result(result):
    """
    Print result with a special marker so C# can find the JSON
    """
    # Print a special marker that C# can use to find the beginning of the JSON
    print("\n###JSON_RESULT_START###")
    print(json.dumps(result, indent=2))
    print("###JSON_RESULT_END###")

if __name__ == "__main__":
    result = main()
    sys.exit(0 if result["mitmproxy_installed"] else 1) 