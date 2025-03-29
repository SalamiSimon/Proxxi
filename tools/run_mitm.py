import json
import re
from typing import Dict, Any, List, Optional, Union, Callable
from mitmproxy import http
import os
import sys
import time

# Minimal startup information
print(f"Starting MITM Modular addon at {time.strftime('%Y-%m-%d %H:%M:%S')}")

# Ensure the mitm_modular directory is in the Python path
base_dir = os.path.dirname(os.path.abspath(__file__))
sys.path.append(base_dir)

try:
    from mitm_modular.database import TargetDatabase
except ImportError as e:
    print(f"Import error: {e}")
    print(f"Available modules: {[name for name in sys.modules.keys()]}")
    raise

class ResponseModifier:
    def __init__(self, db_path="targets.db"):
        """Initialize the response modifier with a database connection"""
        # Convert to absolute path if it's not already
        if not os.path.isabs(db_path):
            abs_db_path = os.path.abspath(db_path)
        else:
            abs_db_path = db_path
            
        self.db = TargetDatabase(db_path)
        self.targets = self.db.get_all_targets()  # Already only returns enabled targets
        print(f"Loaded {len(self.targets)} enabled targets from database")
        
    def reload_targets(self):
        """Reload targets from the database"""
        old_count = len(self.targets)
        self.targets = self.db.get_all_targets()
        print(f"Reloaded targets: was {old_count}, now {len(self.targets)}")
        
    def _url_matches(self, flow_url: str, target_url: str) -> bool:
        """Check if the flow URL matches the target URL pattern"""
        # Simple exact match
        if flow_url == target_url:
            return True
            
        # Treat target_url as regex pattern if it starts with ^ or contains special chars
        if target_url.startswith('^') or any(c in target_url for c in '.*+?[](){}|'):
            try:
                pattern = re.compile(target_url)
                return bool(pattern.search(flow_url))
            except re.error as e:
                print(f"Regex error for '{target_url}': {e}")
                # If regex is invalid, fall back to simple string match
                return target_url in flow_url
                
        # Simple substring match
        return target_url in flow_url
        
    def _find_matching_targets(self, flow: http.HTTPFlow) -> List[Dict[str, Any]]:
        """Find all targets that match the current flow"""
        matches = []
        
        for target in self.targets:
            # This check is redundant as get_all_targets() already filters for enabled targets
            if not target.get('is_enabled', 1):
                continue
            
            url_match = self._url_matches(flow.request.url, target['url'])
            status_match = (
                target['status_code'] is None or 
                target['status_code'] == flow.response.status_code
            )
            
            if url_match and status_match:
                matches.append(target)
                
        return matches
    
    def _apply_dynamic_modification(self, response_data: Dict[str, Any], 
                                   dynamic_code: str) -> Dict[str, Any]:
        """Apply dynamic code to modify the response data"""
        # Create a local namespace with response_data available
        local_namespace = {"response_data": response_data}
        
        # Execute the dynamic code with the response_data in its namespace
        try:
            exec(dynamic_code, {}, local_namespace)
            return local_namespace["response_data"]
        except Exception as e:
            print(f"Error executing dynamic code: {e}")
            # Return original data on error
            return response_data
    
    def response(self, flow: http.HTTPFlow) -> None:
        """Process HTTP responses"""
        # Skip if no response
        if not flow.response:
            return
            
        # Find matching targets
        matching_targets = self._find_matching_targets(flow)
        if not matching_targets:
            return
            
        try:
            # Handle the response based on the first matching target
            target = matching_targets[0]
            
            # Change the status code if requested
            if target['target_status_code'] is not None:
                flow.response.status_code = target['target_status_code']
            
            # Check if content modification is needed based on modification type
            if target['modification_type'] == 'none':
                return
                
            # For dynamic and static types, check if we have content to modify
            if target['modification_type'] == 'static':
                # Skip if no static response is provided
                if not target['static_response'] or not target['static_response'].strip():
                    return
                    
                # Check content type for JSON responses (only modify JSON)
                content_type = flow.response.headers.get("Content-Type", "").lower()
                if not ("application/json" in content_type or "application/problem+json" in content_type):
                    return
                
                # Replace with static JSON response
                try:
                    static_data = json.loads(target['static_response'])
                    flow.response.content = json.dumps(static_data).encode('utf-8')
                    flow.response.headers["Content-Length"] = str(len(flow.response.content))
                except json.JSONDecodeError as e:
                    print(f"Error: Static response is not valid JSON for target {target['id']}: {e}")
                    
            elif target['modification_type'] == 'dynamic':
                # Skip if no dynamic code is provided
                if not target['dynamic_code'] or not target['dynamic_code'].strip():
                    return
                
                # Check content type for JSON responses (only modify JSON)
                content_type = flow.response.headers.get("Content-Type", "").lower()
                if not ("application/json" in content_type or "application/problem+json" in content_type):
                    return
                
                # Apply dynamic modification
                try:
                    # Parse JSON response
                    response_data = json.loads(flow.response.content)
                    
                    # Apply dynamic code
                    modified_data = self._apply_dynamic_modification(
                        response_data, target['dynamic_code']
                    )
                    
                    # Update the response
                    new_content = json.dumps(modified_data).encode('utf-8')
                    flow.response.content = new_content
                    flow.response.headers["Content-Length"] = str(len(new_content))
                except json.JSONDecodeError as e:
                    print(f"Error: Response is not valid JSON for URL {flow.request.url}: {e}")
                except Exception as e:
                    print(f"Error modifying response: {e}")
        
        except Exception as e:
            print(f"Error handling response: {e}")
            import traceback
            traceback.print_exc()

# Mitmproxy addon class
class MITMAddon:
    def __init__(self, db_path="targets.db"):
        # Try to use an absolute path to the database from the script location
        script_dir = os.path.dirname(os.path.abspath(__file__))
        abs_db_path = os.path.join(script_dir, db_path)
        
        self.modifier = ResponseModifier(abs_db_path)
        
    def response(self, flow: http.HTTPFlow) -> None:
        """Handle HTTP responses"""
        self.modifier.response(flow)
        
    def reload(self) -> None:
        """Reload targets from the database"""
        self.modifier.reload_targets()


# Addon instance for mitmproxy
addon = MITMAddon()

# Functions exposed to mitmproxy
def response(flow: http.HTTPFlow) -> None:
    addon.response(flow)
    
def reload() -> None:
    addon.reload_targets()

# Force a reload to make sure we have the latest targets
addon.reload()