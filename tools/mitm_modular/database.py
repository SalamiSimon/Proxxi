import sqlite3
import os
import json
import time
import sys
from typing import Dict, Any, List, Optional, Union

class TargetDatabase:
    def __init__(self, db_path="targets.db"):
        """Initialize the database connection"""
        print(f"[DEBUG] Initializing database with path: {db_path}")
        
        # Check if the path is absolute or relative
        if not os.path.isabs(db_path):
            # Get the directory of this script
            script_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
            db_path = os.path.join(script_dir, db_path)
            print(f"[DEBUG] Using absolute database path: {db_path}")
            
        # Check if the database exists
        if not os.path.exists(db_path):
            print(f"[DEBUG] Database file does not exist at: {db_path}")
            # Try to create the database file
            try:
                self._create_db_if_not_exists(db_path)
                print(f"[DEBUG] Created new database at: {db_path}")
            except Exception as e:
                print(f"[DEBUG] Error creating database: {e}")
        else:
            print(f"[DEBUG] Database file exists at: {db_path}")
            
        self.db_path = db_path
        self.conn = None
        self.cursor = None
        
        # Connect to the database
        try:
            self._connect()
            print(f"[DEBUG] Successfully connected to database")
        except Exception as e:
            print(f"[DEBUG] Error connecting to database: {e}")
            
    def _create_db_if_not_exists(self, db_path):
        """Create the database file if it doesn't exist"""
        # Ensure the parent directory exists
        os.makedirs(os.path.dirname(os.path.abspath(db_path)), exist_ok=True)
        
        # Create the database file and tables
        conn = sqlite3.connect(db_path)
        cursor = conn.cursor()
        
        # Create targets table
        cursor.execute('''
            CREATE TABLE IF NOT EXISTS targets (
                id INTEGER PRIMARY KEY,
                url TEXT NOT NULL,
                status_code INTEGER,
                target_status_code INTEGER,
                modification_type TEXT NOT NULL,
                dynamic_code TEXT,
                static_response TEXT,
                is_enabled INTEGER DEFAULT 1,
                created_at TEXT
            )
        ''')
        
        conn.commit()
        conn.close()

    def _connect(self):
        """Connect to the SQLite database"""
        try:
            print(f"[DEBUG] Connecting to database at: {self.db_path}")
            self.conn = sqlite3.connect(self.db_path)
            self.conn.row_factory = sqlite3.Row  # This enables column access by name
            self.cursor = self.conn.cursor()
            return True
        except sqlite3.Error as e:
            print(f"[DEBUG] Database connection error: {e}")
            return False

    def _disconnect(self):
        """Close the database connection"""
        if self.conn:
            self.conn.close()
            self.conn = None
            self.cursor = None

    def add_target(self, url: str, status_code: int = None, 
                   target_status_code: int = None,
                   modification_type: str = 'dynamic',
                   dynamic_code: str = None, 
                   static_response: str = None) -> int:
        """Add a new target to the database"""
        if modification_type not in ('dynamic', 'static', 'none'):
            raise ValueError("modification_type must be 'dynamic', 'static', or 'none'")
            
        if modification_type == 'dynamic' and not dynamic_code:
            raise ValueError("dynamic_code is required for dynamic modification type")
            
        if modification_type == 'static' and not static_response:
            raise ValueError("static_response is required for static modification type")
        
        query = '''
            INSERT INTO targets (url, status_code, target_status_code, modification_type, dynamic_code, static_response)
            VALUES (?, ?, ?, ?, ?, ?)
        '''
        
        self.cursor.execute(query, (url, status_code, target_status_code, modification_type, dynamic_code, static_response))
        self.conn.commit()
        return self.cursor.lastrowid
        
    def get_all_targets(self) -> List[Dict[str, Any]]:
        """Get all enabled targets from the database"""
        try:
            print(f"[DEBUG] Getting all enabled targets from database")
            if not self.cursor:
                if not self._connect():
                    print(f"[DEBUG] Failed to connect to database for get_all_targets")
                    return []
            
            self.cursor.execute("SELECT * FROM targets WHERE is_enabled = 1")
            rows = self.cursor.fetchall()
            print(f"[DEBUG] Found {len(rows)} enabled targets in database")
            
            # Convert rows to dictionaries
            targets = [dict(row) for row in rows]
            
            # Count targets by type
            types = {}
            for target in targets:
                t = target.get('modification_type', 'unknown')
                types[t] = types.get(t, 0) + 1
            
            print(f"[DEBUG] Target types: {types}")
            return targets
            
        except sqlite3.Error as e:
            print(f"[DEBUG] Error getting targets: {e}")
            return []
        finally:
            self._disconnect()
        
    def get_all_targets_including_disabled(self) -> List[Dict[str, Any]]:
        """Get all targets from the database, including disabled ones"""
        self.cursor.execute('SELECT * FROM targets')
        columns = [col[0] for col in self.cursor.description]
        return [dict(zip(columns, row)) for row in self.cursor.fetchall()]
        
    def get_target(self, target_id: int) -> Optional[Dict[str, Any]]:
        """Get a specific target by ID"""
        self.cursor.execute('SELECT * FROM targets WHERE id = ?', (target_id,))
        row = self.cursor.fetchone()
        if row:
            columns = [col[0] for col in self.cursor.description]
            return dict(zip(columns, row))
        return None
    
    def update_target(self, target_id: int, **kwargs) -> bool:
        """Update a target's properties"""
        allowed_fields = {'url', 'status_code', 'target_status_code', 'modification_type', 
                          'dynamic_code', 'static_response', 'is_enabled'}
        
        updates = {k: v for k, v in kwargs.items() if k in allowed_fields}
        if not updates:
            return False
            
        set_clause = ', '.join([f"{key} = ?" for key in updates.keys()])
        values = list(updates.values()) + [target_id]
        
        query = f"UPDATE targets SET {set_clause} WHERE id = ?"
        self.cursor.execute(query, values)
        self.conn.commit()
        return self.cursor.rowcount > 0
        
    def delete_target(self, target_id: int) -> bool:
        """Delete a target from the database"""
        self.cursor.execute('DELETE FROM targets WHERE id = ?', (target_id,))
        self.conn.commit()
        return self.cursor.rowcount > 0
        
    def close(self):
        """Close the database connection"""
        if self.conn:
            self.conn.close()
            
    def __del__(self):
        """Ensure connection is closed when object is destroyed"""
        self.close() 