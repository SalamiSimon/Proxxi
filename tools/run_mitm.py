import json
import re
from typing import Dict, Any, List, Optional, Union, Callable
from mitmproxy import http
import os
import sys
import time

# Print startup information
print(f"[DEBUG] Starting MITM Modular addon at {time.strftime('%Y-%m-%d %H:%M:%S')}")
print(f"[DEBUG] Script directory: {os.path.dirname(os.path.abspath(__file__))}")

# Ensure the mitm_modular directory is in the Python path
base_dir = os.path.dirname(os.path.abspath(__file__))
sys.path.append(base_dir)
print(f"[DEBUG] Added to Python path: {base_dir}")
print(f"[DEBUG] Python path now: {sys.path}")

try:
    from mitm_modular.database import TargetDatabase
    print("[DEBUG] Successfully imported TargetDatabase from mitm_modular.database")
except ImportError as e:
    print(f"[DEBUG] Import error: {e}")
    print(f"[DEBUG] Available modules: {[name for name in sys.modules.keys()]}")
    raise

class ResponseModifier:
    def __init__(self, db_path="targets.db"):
        """Initialize the response modifier with a database connection"""
        print(f"[DEBUG] Initializing ResponseModifier with database: {db_path}")
        self.db = TargetDatabase(db_path)
        self.targets = self.db.get_all_targets()  # Already only returns enabled targets
        print(f"[DEBUG] Loaded {len(self.targets)} enabled targets from database")
        
        # Print detailed information about each target for debugging
        if len(self.targets) == 0:
            print(f"[DEBUG] No enabled targets found in database. Running CLI check...")
            try:
                import subprocess
                import sys
                import os
                
                # Get directory of current script
                script_dir = os.path.dirname(os.path.abspath(__file__))
                
                # Run CLI command to list all targets
                cmd = [sys.executable, "-m", "mitm_modular.cli", "json-list-all"]
                result = subprocess.run(cmd, cwd=script_dir, capture_output=True, text=True)
                
                if result.returncode == 0:
                    print(f"[DEBUG] CLI command output: {result.stdout.strip()}")
                    print(f"[DEBUG] CLI stderr: {result.stderr.strip()}")
                else:
                    print(f"[DEBUG] CLI command failed with code {result.returncode}")
                    print(f"[DEBUG] CLI stderr: {result.stderr.strip()}")
            except Exception as e:
                print(f"[DEBUG] Error checking targets via CLI: {e}")
        else:
            for i, target in enumerate(self.targets):
                print(f"[DEBUG] Target {i+1}: ID={target['id']}, URL={target['url']}, Type={target['modification_type']}, Status={target.get('status_code')}, Target Status={target.get('target_status_code')}, Enabled={target['is_enabled'] == 1}")
                
    def reload_targets(self):
        """Reload targets from the database"""
        print("[DEBUG] Reloading targets from database")
        old_count = len(self.targets)
        
        # Re-initialize the database connection to ensure we're getting fresh data
        self.db = TargetDatabase(self.db.db_path)
        self.targets = self.db.get_all_targets()
        
        print(f"[DEBUG] Reloaded targets: was {old_count}, now {len(self.targets)}")
        
        # Print details about reloaded targets
        if len(self.targets) > 0:
            for i, target in enumerate(self.targets):
                print(f"[DEBUG] Reloaded target {i+1}: ID={target['id']}, URL={target['url']}, Type={target['modification_type']}, Status={target.get('status_code')}, Target Status={target.get('target_status_code')}, Enabled={target['is_enabled'] == 1}")
        else:
            print("[DEBUG] No enabled targets found after reload")

    def _url_matches(self, flow_url: str, target_url: str) -> bool:
        """Check if the flow URL matches the target URL pattern"""
        # Simple exact match
        if flow_url == target_url:
            print(f"[DEBUG] URL exact match: {flow_url} == {target_url}")
            return True
        
        # Parse the URLs to handle query parameters properly
        import urllib.parse
        
        # For URL pattern with query parameters (e.g., "endpoint?param=value")
        if '?' in target_url:
            try:
                # Parse both URLs
                if not target_url.startswith(('http://', 'https://')):
                    # Add a dummy scheme for parsing relative URLs
                    target_to_parse = f"http://example.com/{target_url}"
                else:
                    target_to_parse = target_url
                    
                parsed_target = urllib.parse.urlparse(target_to_parse)
                parsed_flow = urllib.parse.urlparse(flow_url)
                
                # Extract path and query components
                target_path = parsed_target.path.lstrip('/')
                if target_path.startswith('example.com/'):
                    target_path = target_path[12:]  # Remove the dummy host
                    
                target_query = parsed_target.query
                flow_path = parsed_flow.path
                flow_query = parsed_flow.query
                
                print(f"[DEBUG] Target - Path: '{target_path}', Query: '{target_query}'")
                print(f"[DEBUG] Flow - Path: '{flow_path}', Query: '{flow_query}'")
                
                # Check path (either full match or endpoint is in the path)
                path_match = target_path in flow_path or flow_path.endswith(target_path)
                
                # Check query (either full match or params subset match)
                query_match = False
                if target_query:
                    # If the target query is a subset of flow query
                    query_match = target_query in flow_query
                    
                    # If that didn't work, try parsing and comparing parameters
                    if not query_match:
                        target_params = urllib.parse.parse_qs(target_query)
                        flow_params = urllib.parse.parse_qs(flow_query)
                        
                        query_match = all(
                            k in flow_params and any(tv in flow_params[k] for tv in v)
                            for k, v in target_params.items()
                        )
                else:
                    # No query in target pattern means we only care about the path
                    query_match = True
                
                print(f"[DEBUG] Path match: {path_match}, Query match: {query_match}")
                
                if path_match and query_match:
                    print(f"[DEBUG] Match successful for endpoint with query!")
                    return True
                
            except Exception as e:
                print(f"[DEBUG] Error parsing URLs: {e}")
                # Fall back to simple substring match
        
        # Fallback: simple substring match
        result = target_url in flow_url
        print(f"[DEBUG] Simple substring match: '{target_url}' in '{flow_url}' => {result}")
        return result
        
    def _find_matching_targets(self, flow: http.HTTPFlow) -> List[Dict[str, Any]]:
        """Find all targets that match the current flow"""
        flow_url = flow.request.url
        print(f"[DEBUG] Finding matches for URL: {flow_url}, status: {flow.response.status_code}")
        
        # Parse the URL to check for query parameters
        import urllib.parse
        parsed_url = urllib.parse.urlparse(flow_url)
        path = parsed_url.path
        query = parsed_url.query
        print(f"[DEBUG] URL parsed - Host: {parsed_url.netloc}, Path: {path}, Query: {query}")
        
        matches = []
        
        for target in self.targets:
            # This check is redundant as get_all_targets() already filters for enabled targets,
            # but we'll keep it for clarity and safety
            if not target.get('is_enabled', 1):
                print(f"[DEBUG] Skipping disabled target ID={target['id']}")
                continue
            
            target_url = target['url']
            print(f"[DEBUG] Checking target ID={target['id']}, URL pattern='{target_url}'")
            
            # Try multiple matching approaches
            url_match = False  # Start with no match
            
            # Check 1: Standard substring check in the full URL
            std_match = self._url_matches(flow_url, target_url)
            if std_match:
                url_match = True
                print(f"[DEBUG] Standard URL match succeeded")
            
            if not url_match and ('?' in target_url):
                print(f"[DEBUG] Trying combined path+query match for '{target_url}'")
                
                # Split the target into path and query parts
                target_parts = target_url.split('?', 1)
                target_path_part = target_parts[0]
                target_query_part = target_parts[1] if len(target_parts) > 1 else ""
                
                # Check if both path and query components match (case-insensitive)
                path_contains = target_path_part and target_path_part.lower() in path.lower()
                query_contains = target_query_part and target_query_part.lower() in query.lower()
                
                print(f"[DEBUG] Path check (case-insensitive): '{target_path_part}' in '{path}' = {path_contains}")
                print(f"[DEBUG] Query check (case-insensitive): '{target_query_part}' in '{query}' = {query_contains}")
                
                # Special case when both path and query parts should match
                if path_contains and query_contains:
                    print(f"[DEBUG] Both path and query parts match - SUCCESS!")
                    url_match = True
                    
                # Special case for simple filename.ext?param=value pattern
                elif not target_path_part.startswith('/') and '/' in path:
                    # Extract just the endpoint name from the path
                    endpoint = path.split('/')[-1]
                    print(f"[DEBUG] Checking endpoint: '{endpoint}' vs target path: '{target_path_part}'")
                    if endpoint.lower() == target_path_part.lower() and query_contains:
                        print(f"[DEBUG] Endpoint name and query match - SUCCESS!")
                        url_match = True
            
            # Check 3: If the target URL looks like a simple endpoint, try to match it as a suffix of the path
            if not url_match and '?' not in target_url and not target_url.startswith('/') and not target_url.startswith('http'):
                print(f"[DEBUG] Trying endpoint match: '{target_url}' as endpoint in '{path}'")
                path_lower = path.lower()
                target_lower = target_url.lower()
                if path_lower.endswith('/' + target_lower) or path_lower.endswith(target_lower):
                    print(f"[DEBUG] Endpoint match succeeded (case-insensitive)!")
                    url_match = True
            
            status_match = (
                target['status_code'] is None or 
                target['status_code'] == flow.response.status_code
            )
            
            print(f"[DEBUG] Target ID={target['id']} match results: URL={url_match}, status={status_match}")
            
            if url_match and status_match:
                print(f"[DEBUG] Target {target['id']} matches!")
                matches.append(target)
            else:
                print(f"[DEBUG] Target {target['id']} does not match. URL match: {url_match}, Status match: {status_match}")
                
        print(f"[DEBUG] Found {len(matches)} matching targets")
        return matches
    
    def _apply_dynamic_modification(self, response_data: Dict[str, Any], 
                                   dynamic_code: str) -> Dict[str, Any]:
        """Apply dynamic code to modify the response data"""
        print("[DEBUG] Applying dynamic modification code")
        # Create a local namespace with response_data available
        local_namespace = {"response_data": response_data}
        
        # Execute the dynamic code with the response_data in its namespace
        try:
            print(f"[DEBUG] Executing dynamic code: {dynamic_code[:50]}...")
            exec(dynamic_code, {}, local_namespace)
            # Return the potentially modified response_data
            print("[DEBUG] Dynamic code executed successfully")
            return local_namespace["response_data"]
        except Exception as e:
            print(f"[DEBUG] Error executing dynamic code: {e}")
            # Return original data on error
            return response_data
    
    def response(self, flow: http.HTTPFlow) -> None:
        """Process HTTP responses"""
        # Skip if no response
        if not flow.response:
            return
            
        print(f"\n[DEBUG] ========== Processing response for {flow.request.url} ==========")
        
        # Check for JSON responses
        content_type = flow.response.headers.get("Content-Type", "").lower()
        print(f"[DEBUG] Content-Type: {content_type}")
        
        if not ("application/json" in content_type or "application/problem+json" in content_type):
            print("[DEBUG] Skipping non-JSON response")
            return
            
        # Find matching targets
        matching_targets = self._find_matching_targets(flow)
        if not matching_targets:
            print("[DEBUG] No matching targets found")
            return
            
        try:
            # Handle the response based on the first matching target
            # (Could be extended to apply multiple targets in sequence)
            target = matching_targets[0]
            print(f"[DEBUG] Selected target: ID={target['id']}, type={target['modification_type']}")
            
            # Change the status code if requested
            if target['target_status_code'] is not None:
                print(f"[DEBUG] Changing status code from {flow.response.status_code} to {target['target_status_code']}")
                flow.response.status_code = target['target_status_code']
            
            if target['modification_type'] == 'static':
                # Replace with static JSON response
                try:
                    print("[DEBUG] Applying static response")
                    static_data = json.loads(target['static_response'])
                    flow.response.content = json.dumps(static_data).encode('utf-8')
                    flow.response.headers["Content-Length"] = str(len(flow.response.content))
                    print(f"[DEBUG] Static response applied, new content length: {len(flow.response.content)}")
                except json.JSONDecodeError as e:
                    print(f"[DEBUG] Error: Static response is not valid JSON for target {target['id']}: {e}")
                    
            elif target['modification_type'] == 'dynamic':
                # Apply dynamic modification
                try:
                    # Parse JSON response
                    print("[DEBUG] Parsing JSON response")
                    response_data = json.loads(flow.response.content)
                    
                    # Apply dynamic code
                    print("[DEBUG] Applying dynamic modification")
                    modified_data = self._apply_dynamic_modification(
                        response_data, target['dynamic_code']
                    )
                    
                    # Update the response
                    print("[DEBUG] Updating response with modified data")
                    new_content = json.dumps(modified_data).encode('utf-8')
                    flow.response.content = new_content
                    flow.response.headers["Content-Length"] = str(len(new_content))
                    print(f"[DEBUG] Dynamic response applied, new content length: {len(new_content)}")
                except json.JSONDecodeError as e:
                    print(f"[DEBUG] Error: Response is not valid JSON for URL {flow.request.url}: {e}")
                except Exception as e:
                    print(f"[DEBUG] Error modifying response: {e}")
            
            print("[DEBUG] Response modification complete")
        
        except Exception as e:
            print(f"[DEBUG] Error handling response: {e}")
            import traceback
            traceback.print_exc()

# Mitmproxy addon class
class MITMAddon:
    def __init__(self, db_path="targets.db"):
        print(f"[DEBUG] Initializing MITMAddon with database: {db_path}")
        self.modifier = ResponseModifier(db_path)
        
    def response(self, flow: http.HTTPFlow) -> None:
        """Handle HTTP responses"""
        self.modifier.response(flow)
        
    def reload(self) -> None:
        """Reload targets from the database"""
        print("[DEBUG] Reload function called")
        self.modifier.reload_targets()
        # Also try to verify database contents directly
        try:
            import subprocess
            import sys
            import os
            
            # Get directory of current script
            script_dir = os.path.dirname(os.path.abspath(__file__))
            
            # Run CLI command to list all targets
            cmd = [sys.executable, "-m", "mitm_modular.cli", "json-list-all"]
            result = subprocess.run(cmd, cwd=script_dir, capture_output=True, text=True)
            
            if result.returncode == 0:
                print(f"[DEBUG] CLI verification after reload: {result.stdout.strip()}")
            else:
                print(f"[DEBUG] CLI verification failed: {result.stderr.strip()}")
        except Exception as e:
            print(f"[DEBUG] Error during CLI verification: {e}")


# Addon instance for mitmproxy
print("[DEBUG] Creating MITMAddon instance")
addon = MITMAddon()

# Functions exposed to mitmproxy
def response(flow: http.HTTPFlow) -> None:
    print(f"[DEBUG] Received response: {flow.request.url}")
    addon.response(flow)
    
def reload() -> None:
    print("[DEBUG] Reload requested")
    addon.reload()

# Print message to indicate script loaded successfully
print("[DEBUG] MITM Modular addon loaded successfully!")