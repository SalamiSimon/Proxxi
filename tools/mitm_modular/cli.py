import argparse
import json
import os
import sys
from tabulate import tabulate

# Fix imports to work whether run directly or as a module
try:
    # Try direct import first (when running directly from the module directory)
    from database import TargetDatabase
    print("Imported TargetDatabase directly")
except ImportError:
    # Try package import (when installed or parent dir is in path)
    try:
        from mitm_modular.database import TargetDatabase
        print("Imported TargetDatabase from package")
    except ImportError:
        # Add parent directory to Python path to find module
        current_dir = os.path.dirname(os.path.abspath(__file__))
        parent_dir = os.path.dirname(current_dir)
        if parent_dir not in sys.path:
            sys.path.insert(0, parent_dir)
            print(f"Added {parent_dir} to Python path")
        
        # Try both import styles again
        try:
            from database import TargetDatabase
            print("Imported TargetDatabase directly after path fix")
        except ImportError:
            try:
                from mitm_modular.database import TargetDatabase
                print("Imported TargetDatabase from package after path fix")
            except ImportError as e:
                print(f"ERROR: Failed to import TargetDatabase: {e}")
                print(f"Python path: {sys.path}")
                print(f"Current directory: {os.getcwd()}")
                sys.exit(1)

# Add a new command to cli.py
def json_list_targets(db):
    """List all targets in JSON format for machine consumption"""
    targets = db.get_all_targets()
    print(json.dumps(targets))

def json_list_all_targets(db):
    """List all targets in JSON format including disabled ones"""
    targets = db.get_all_targets_including_disabled()
    print(json.dumps(targets))

def list_targets(db):
    """List all targets in the database"""
    targets = db.get_all_targets()
    
    if not targets:
        print("No targets found.")
        return
    
    # Format the table
    table_data = []
    for t in targets:
        # Truncate long fields
        dynamic_code = t['dynamic_code'][:30] + '...' if t['dynamic_code'] and len(t['dynamic_code']) > 30 else t['dynamic_code']
        static_response = t['static_response'][:30] + '...' if t['static_response'] and len(t['static_response']) > 30 else t['static_response']
        
        table_data.append([
            t['id'],
            t['url'],
            t['status_code'] or 'Any',
            t['target_status_code'] or 'No change',
            t['modification_type'],
            dynamic_code,
            static_response,
            'Enabled' if t['is_enabled'] else 'Disabled'
        ])
    
    headers = ['ID', 'URL', 'Match Status', 'Target Status', 'Type', 'Dynamic Code', 'Static Response', 'Status']
    print(tabulate(table_data, headers=headers, tablefmt='grid'))

def add_target(db, args):
    """Add a new target to the database"""
    # For dynamic modification
    if args.type == 'dynamic':
        if args.code_file:
            with open(args.code_file, 'r') as f:
                dynamic_code = f.read()
        elif args.code:
            dynamic_code = args.code
        else:
            print("Error: For dynamic modifications, you must provide --code or --code-file")
            return False
            
        target_id = db.add_target(
            url=args.url,
            status_code=args.status,
            target_status_code=args.target_status,
            modification_type='dynamic',
            dynamic_code=dynamic_code
        )
        
    # For static modification
    elif args.type == 'static':
        if args.response_file:
            with open(args.response_file, 'r') as f:
                static_response = f.read()
        elif args.response:
            static_response = args.response
        else:
            print("Error: For static modifications, you must provide --response or --response-file")
            return False
            
        # Validate JSON
        try:
            json.loads(static_response)
        except json.JSONDecodeError as e:
            print(f"Error: Invalid JSON in static response: {e}")
            return False
            
        target_id = db.add_target(
            url=args.url,
            status_code=args.status,
            target_status_code=args.target_status,
            modification_type='static',
            static_response=static_response
        )
        
    # For none modification (status code only)
    elif args.type == 'none':
        if not args.target_status:
            print("Error: For 'none' modification type, you must provide --target-status")
            return False
            
        target_id = db.add_target(
            url=args.url,
            status_code=args.status,
            target_status_code=args.target_status,
            modification_type='none'
        )
    
    print(f"Target added with ID: {target_id}")
    return True

def delete_target(db, target_id):
    """Delete a target from the database"""
    if db.delete_target(target_id):
        print(f"Target {target_id} deleted successfully")
    else:
        print(f"Error: Target {target_id} not found")

def enable_disable_target(db, target_id, enable=True):
    """Enable or disable a target"""
    if db.update_target(target_id, is_enabled=1 if enable else 0):
        print(f"Target {target_id} {'enabled' if enable else 'disabled'} successfully")
    else:
        print(f"Error: Target {target_id} not found")

def view_target(db, target_id):
    """View details of a specific target"""
    target = db.get_target(target_id)
    if not target:
        print(f"Error: Target {target_id} not found")
        return
        
    print(f"Target ID: {target['id']}")
    print(f"URL: {target['url']}")
    print(f"Match Status Code: {target['status_code'] or 'Any'}")
    print(f"Target Status Code: {target['target_status_code'] or 'No change'}")
    print(f"Modification Type: {target['modification_type']}")
    print(f"Enabled: {'Yes' if target['is_enabled'] else 'No'}")
    
    if target['modification_type'] == 'dynamic':
        print("\nDynamic Code:")
        print("-------------")
        print(target['dynamic_code'])
    elif target['modification_type'] == 'static':
        print("\nStatic Response:")
        print("---------------")
        print(target['static_response'])
    elif target['modification_type'] == 'none':
        print("\nNo content modification (status code only)")

def reload_targets(db):
    """Reload targets in the proxy"""
    print("Reload command received. Targets will be reloaded on next request.")
    # In a real implementation, this would signal the proxy to reload

def delete_all_targets(db):
    """Delete all targets from the database"""
    # Get all targets including disabled ones
    targets = db.get_all_targets_including_disabled()
    
    if not targets:
        print("No targets found to delete.")
        return
    
    # Delete each target one by one
    success_count = 0
    for target in targets:
        target_id = target['id']
        if db.delete_target(target_id):
            success_count += 1
            print(f"Target {target_id} deleted successfully")
        else:
            print(f"Error: Failed to delete target {target_id}")
    
    print(f"Deleted {success_count} out of {len(targets)} targets")

def main():
    parser = argparse.ArgumentParser(description='MITM Response Modifier CLI')
    subparsers = parser.add_subparsers(dest='command', help='Command to execute')
    
    # List command
    list_parser = subparsers.add_parser('list', help='List all targets')
    
    # JSON list commands
    json_list_parser = subparsers.add_parser('json-list', help='List all enabled targets in JSON format')
    json_list_all_parser = subparsers.add_parser('json-list-all', help='List all targets in JSON format, including disabled ones')
    
    # Add command
    add_parser = subparsers.add_parser('add', help='Add a new target')
    add_parser.add_argument('url', help='Target URL or URL pattern')
    add_parser.add_argument('--status', type=int, help='HTTP status code to match (optional)')
    add_parser.add_argument('--target-status', type=int, help='Target HTTP status code to set (optional)')
    add_parser.add_argument('--type', choices=['dynamic', 'static', 'none'], required=True, 
                           help='Modification type (none = status code only)')
    
    # Dynamic code options
    add_parser.add_argument('--code', help='Dynamic Python code for modification')
    add_parser.add_argument('--code-file', help='File containing dynamic Python code')
    
    # Static response options
    add_parser.add_argument('--response', help='Static JSON response')
    add_parser.add_argument('--response-file', help='File containing static JSON response')
    
    # Delete command
    delete_parser = subparsers.add_parser('delete', help='Delete a target')
    delete_parser.add_argument('id', type=int, help='Target ID to delete')
    
    # Delete All command
    delete_all_parser = subparsers.add_parser('delete-all', help='Delete all targets')
    
    # Enable command
    enable_parser = subparsers.add_parser('enable', help='Enable a target')
    enable_parser.add_argument('id', type=int, help='Target ID to enable')
    
    # Disable command
    disable_parser = subparsers.add_parser('disable', help='Disable a target')
    disable_parser.add_argument('id', type=int, help='Target ID to disable')
    
    # View command
    view_parser = subparsers.add_parser('view', help='View target details')
    view_parser.add_argument('id', type=int, help='Target ID to view')
    
    # Reload command
    reload_parser = subparsers.add_parser('reload', help='Tell the proxy to reload targets')
    
    # Database option
    parser.add_argument('--db', default='targets.db', help='Database file path')
    
    args = parser.parse_args()
    
    if not args.command:
        parser.print_help()
        return
        
    db = TargetDatabase(args.db)
    
    try:
        if args.command == 'list':
            list_targets(db)
        elif args.command == 'json-list':
            json_list_targets(db)
        elif args.command == 'json-list-all':
            json_list_all_targets(db)
        elif args.command == 'add':
            add_target(db, args)
        elif args.command == 'delete':
            delete_target(db, args.id)
        elif args.command == 'delete-all':
            delete_all_targets(db)
        elif args.command == 'enable':
            enable_disable_target(db, args.id, enable=True)
        elif args.command == 'disable':
            enable_disable_target(db, args.id, enable=False)
        elif args.command == 'view':
            view_target(db, args.id)
        elif args.command == 'reload':
            reload_targets(db)
    finally:
        db.close()

if __name__ == '__main__':
    main() 