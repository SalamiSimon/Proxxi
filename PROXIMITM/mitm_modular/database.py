import sqlite3
import os
import json
from typing import Dict, Any, List, Optional, Union

class TargetDatabase:
    def __init__(self, db_path="targets.db"):
        """Initialize the database connection"""
        self.db_path = db_path
        self.conn = None
        self.cursor = None
        self.initialize()
        
    def initialize(self):
        """Create database and tables if they don't exist"""
        need_to_create = not os.path.exists(self.db_path)
        
        self.conn = sqlite3.connect(self.db_path)
        self.cursor = self.conn.cursor()
        
        if need_to_create:
            self.cursor.execute('''
                CREATE TABLE targets (
                    id INTEGER PRIMARY KEY,
                    url TEXT NOT NULL,
                    status_code INTEGER,
                    target_status_code INTEGER,
                    modification_type TEXT CHECK(modification_type IN ('dynamic', 'static', 'none')),
                    dynamic_code TEXT,
                    static_response TEXT,
                    is_enabled INTEGER DEFAULT 1,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )
            ''')
            self.conn.commit()
        else:
            # Check if we need to update the schema for existing databases
            try:
                # Try to insert a 'none' modification type to see if it's allowed
                self.cursor.execute(
                    "INSERT INTO targets (url, modification_type) VALUES ('test', 'none')"
                )
                # If successful, delete the test row
                self.cursor.execute("DELETE FROM targets WHERE url = 'test' AND modification_type = 'none'")
                self.conn.commit()
            except sqlite3.IntegrityError:
                # If the check constraint fails, we need to alter the table
                # SQLite doesn't support modifying constraints directly, so we need a workaround
                print("Updating database schema to support 'none' modification type...")
                
                # Create a backup of the data
                self.cursor.execute("SELECT * FROM targets")
                targets_data = self.cursor.fetchall()
                
                # Get the column names
                self.cursor.execute("PRAGMA table_info(targets)")
                columns = [column[1] for column in self.cursor.fetchall()]
                
                # Rename the old table
                self.cursor.execute("ALTER TABLE targets RENAME TO targets_old")
                
                # Create the new table with updated CHECK constraint
                self.cursor.execute('''
                    CREATE TABLE targets (
                        id INTEGER PRIMARY KEY,
                        url TEXT NOT NULL,
                        status_code INTEGER,
                        target_status_code INTEGER,
                        modification_type TEXT CHECK(modification_type IN ('dynamic', 'static', 'none')),
                        dynamic_code TEXT,
                        static_response TEXT,
                        is_enabled INTEGER DEFAULT 1,
                        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                    )
                ''')
                
                # Copy the data from old table to new table
                placeholders = ','.join(['?'] * len(columns))
                columns_str = ','.join(columns)
                self.cursor.executemany(
                    f"INSERT INTO targets ({columns_str}) VALUES ({placeholders})",
                    targets_data
                )
                
                # Drop the old table
                self.cursor.execute("DROP TABLE targets_old")
                
                self.conn.commit()
                print("Database schema updated successfully.")
    
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
        self.cursor.execute('SELECT * FROM targets WHERE is_enabled = 1')
        columns = [col[0] for col in self.cursor.description]
        return [dict(zip(columns, row)) for row in self.cursor.fetchall()]
        
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