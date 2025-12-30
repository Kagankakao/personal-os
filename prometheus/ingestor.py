import sqlite3
import logging
import re
import glob
import json
from .engine import PrometheusEngine
import os

logger = logging.getLogger(__name__)

class JournalIngestor:
    def __init__(self, db_path: str, engine: PrometheusEngine):
        self.db_path = db_path
        self.engine = engine
        # KEGOMODORO path relative to prometheus directory
        self.kegomodoro_path = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "kegomodoro")

    def sync(self):
        """Synchronize all journal sources with Prometheus"""
        entries_to_upsert = []
        
        # 1. Sync from SQLite database
        entries_to_upsert.extend(self._sync_sqlite())
        
        # 2. Sync from KEGOMODORO text journals
        entries_to_upsert.extend(self._sync_kegomodoro_journals())
        
        # 3. Upsert all entries
        if entries_to_upsert:
            logger.info(f"Upserting {len(entries_to_upsert)} total entries to Prometheus")
            self.engine.upsert_entries(entries_to_upsert)
        else:
            logger.info("No entries to sync")

    def _sync_sqlite(self):
        """Sync from SQLite JournalEntries table"""
        entries = []
        try:
            if not os.path.exists(self.db_path):
                logger.warning(f"Database not found: {self.db_path}")
                return entries
                
            conn = sqlite3.connect(self.db_path)
            cursor = conn.cursor()
            
            cursor.execute("SELECT Id, UserId, Date, TimeWorked, NoteText, MoodDetected FROM JournalEntries")
            rows = cursor.fetchall()
            
            for row in rows:
                jid, uid, date, time_worked, note, mood = row
                if not note:
                    continue
                    
                entries.append({
                    "id": f"sqlite_{jid}",
                    "text": note,
                    "metadata": {
                        "db_id": jid,
                        "user_id": uid,
                        "date": str(date),
                        "time_worked": time_worked,
                        "mood": mood,
                        "source": "sqlite",
                        "text": note
                    }
                })
            
            conn.close()
            logger.info(f"Found {len(entries)} entries in SQLite")
            
        except Exception as e:
            logger.error(f"Failed to sync SQLite: {e}")
        
        return entries

    def _sync_kegomodoro_journals(self):
        """Sync from KEGOMODORO text journal files"""
        entries = []
        try:
            # Find all journal.txt files in user folders
            users_path = os.path.join(self.kegomodoro_path, "dependencies", "texts", "Users")
            if not os.path.exists(users_path):
                logger.info(f"KEGOMODORO users path not found: {users_path}")
                return entries
            
            for user_dir in os.listdir(users_path):
                journal_path = os.path.join(users_path, user_dir, "journal.txt")
                if os.path.exists(journal_path):
                    entries.extend(self._parse_journal_file(journal_path, user_dir))
            
            logger.info(f"Found {len(entries)} entries in KEGOMODORO journals")
            
        except Exception as e:
            logger.error(f"Failed to sync KEGOMODORO journals: {e}")
        
        return entries

    def _parse_journal_file(self, filepath, username):
        """Parse a KEGOMODORO journal.txt file into entries"""
        entries = []
        try:
            with open(filepath, 'r', encoding='utf-8', errors='ignore') as f:
                content = f.read()
            
            # Split by date pattern (MM/DD/YYYY)
            date_pattern = r'(\d{1,2}/\d{1,2}/\d{4})'
            parts = re.split(date_pattern, content)
            
            current_date = "unknown"
            for i, part in enumerate(parts):
                part = part.strip()
                if not part:
                    continue
                    
                # Check if this is a date
                if re.match(date_pattern, part):
                    current_date = part
                else:
                    # This is content - split by lines and create entries
                    lines = [l.strip() for l in part.split('\n') if l.strip()]
                    for line_idx, line in enumerate(lines):
                        # Skip time stamps (HH:MM:SS format)
                        if re.match(r'^\d{2}:\d{2}:\d{2}$', line):
                            continue
                        # Skip if too short
                        if len(line) < 2:
                            continue
                            
                        entry_id = f"kego_{username}_{current_date}_{line_idx}"
                        entries.append({
                            "id": entry_id,
                            "text": line,
                            "metadata": {
                                "date": current_date,
                                "username": username,
                                "source": "kegomodoro",
                                "text": line
                            }
                        })
                        
        except Exception as e:
            logger.error(f"Failed to parse journal file {filepath}: {e}")
        
        return entries

if __name__ == "__main__":
    logging.basicConfig(level=logging.INFO)
    db = "../keganos.db"
    engine = PrometheusEngine()
    ingestor = JournalIngestor(db, engine)
    ingestor.sync()
