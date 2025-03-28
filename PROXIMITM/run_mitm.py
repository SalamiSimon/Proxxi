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
        # Convert to absolute path if it's not already
        if not os.path.isabs(db_path):
            abs_db_path = os.path.abspath(db_path)
        else:
            abs_db_path = db_path
            
        print(f"[DEBUG] Initializing ResponseModifier with database path: {db_path}")
        print(f"[DEBUG] Absolute database path: {abs_db_path}")
        print(f"[DEBUG] Current working directory: {os.getcwd()}")
        print(f"[DEBUG] Database file exists: {os.path.exists(abs_db_path)}")
        
        self.db = TargetDatabase(db_path)
        self.targets = self.db.get_all_targets()  # Already only returns enabled targets
        print(f"[DEBUG] Loaded {len(self.targets)} enabled targets from database")
        for i, target in enumerate(self.targets):
            print(f"[DEBUG] Target {i+1}: ID={target['id']}, URL='{target['url']}', Type={target['modification_type']}, Enabled={target['is_enabled'] == 1}")
        
    def reload_targets(self):
        """Reload targets from the database"""
        print("[DEBUG] Reloading targets from database")
        old_count = len(self.targets)
        self.targets = self.db.get_all_targets()  # Already only returns enabled targets
        print(f"[DEBUG] Reloaded targets: was {old_count}, now {len(self.targets)}")
        for i, target in enumerate(self.targets):
            print(f"[DEBUG] Target {i+1}: ID={target['id']}, URL='{target['url']}', Type={target['modification_type']}, Enabled={target['is_enabled'] == 1}")
        
    def _url_matches(self, flow_url: str, target_url: str) -> bool:
        """Check if the flow URL matches the target URL pattern"""
        # Simple exact match
        if flow_url == target_url:
            print(f"[DEBUG] URL exact match: '{flow_url}' == '{target_url}'")
            return True
            
        # Treat target_url as regex pattern if it starts with ^ or contains special chars
        if target_url.startswith('^') or any(c in target_url for c in '.*+?[](){}|'):
            try:
                pattern = re.compile(target_url)
                result = bool(pattern.search(flow_url))
                print(f"[DEBUG] URL regex match: '{flow_url}' ~= '{target_url}' => {result}")
                return result
            except re.error as e:
                print(f"[DEBUG] Regex error for '{target_url}': {e}")
                # If regex is invalid, fall back to simple string match
                result = target_url in flow_url
                print(f"[DEBUG] Falling back to substring match: '{target_url}' in '{flow_url}' => {result}")
                return result
                
        # Simple substring match
        result = target_url in flow_url
        print(f"[DEBUG] URL substring match: '{target_url}' in '{flow_url}' => {result}")
        return result
        
    def _find_matching_targets(self, flow: http.HTTPFlow) -> List[Dict[str, Any]]:
        """Find all targets that match the current flow"""
        print(f"[DEBUG] Finding matches for URL: '{flow.request.url}', status: {flow.response.status_code}")
        matches = []
        
        print(f"[DEBUG] Checking {len(self.targets)} targets for matches")
        for target in self.targets:
            # This check is redundant as get_all_targets() already filters for enabled targets,
            # but we'll keep it for clarity and safety
            if not target.get('is_enabled', 1):
                print(f"[DEBUG] Skipping disabled target ID={target['id']}, URL='{target['url']}'")
                continue
            
            print(f"[DEBUG] Testing target ID={target['id']}, URL='{target['url']}', enabled={target['is_enabled']}")
            url_match = self._url_matches(flow.request.url, target['url'])
            status_match = (
                target['status_code'] is None or 
                target['status_code'] == flow.response.status_code
            )
            
            print(f"[DEBUG] Result for target ID={target['id']}: URL match={url_match}, status match={status_match}")
            
            if url_match and status_match:
                print(f"[DEBUG] Target {target['id']} matches!")
                matches.append(target)
                
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
            
            # Check if content modification is needed based on modification type
            if target['modification_type'] == 'none':
                print("[DEBUG] Type is 'none', only changing status code, no content modification")
                return
                
            # For dynamic and static types, check if we have content to modify
            if target['modification_type'] == 'static':
                # Skip if no static response is provided
                if not target['static_response'] or not target['static_response'].strip():
                    print("[DEBUG] No static response provided, skipping content modification")
                    return
                    
                # Check content type for JSON responses (only modify JSON)
                content_type = flow.response.headers.get("Content-Type", "").lower()
                if not ("application/json" in content_type or "application/problem+json" in content_type):
                    print("[DEBUG] Skipping non-JSON response for static modification")
                    return
                
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
                # Skip if no dynamic code is provided
                if not target['dynamic_code'] or not target['dynamic_code'].strip():
                    print("[DEBUG] No dynamic code provided, skipping content modification")
                    return
                
                # Check content type for JSON responses (only modify JSON)
                content_type = flow.response.headers.get("Content-Type", "").lower()
                if not ("application/json" in content_type or "application/problem+json" in content_type):
                    print("[DEBUG] Skipping non-JSON response for dynamic modification")
                    return
                
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
        
        # Try to use an absolute path to the database from the script location
        script_dir = os.path.dirname(os.path.abspath(__file__))
        abs_db_path = os.path.join(script_dir, db_path)
        print(f"[DEBUG] Script directory: {script_dir}")
        print(f"[DEBUG] Using database path: {abs_db_path}")
        
        self.modifier = ResponseModifier(abs_db_path)
        
    def response(self, flow: http.HTTPFlow) -> None:
        """Handle HTTP responses"""
        self.modifier.response(flow)
        
    def reload(self) -> None:
        """Reload targets from the database"""
        print("[DEBUG] Reload function called")
        self.modifier.reload_targets()


# Addon instance for mitmproxy
print("[DEBUG] Creating MITMAddon instance")
addon = MITMAddon()

# Functions exposed to mitmproxy
def response(flow: http.HTTPFlow) -> None:
    print(f"[DEBUG] Received response: {flow.request.url}")
    addon.response(flow)
    
def reload() -> None:
    print("[DEBUG] Reload requested")
    addon.reload_targets()

# Print message to indicate script loaded successfully
print("[DEBUG] MITM Modular addon loaded successfully!")

# Force a reload to make sure we have the latest targets
print("[DEBUG] Forcing reload to ensure targets are loaded correctly")
addon.reload()

# List loaded targets for verification
print("[DEBUG] Targets after reload:")
for i, target in enumerate(addon.modifier.targets):
    print(f"[DEBUG] Target {i+1}: ID={target['id']}, URL='{target['url']}', Type={target['modification_type']}, Enabled={target['is_enabled'] == 1}")