import logging
import os
import json
import numpy as np
from datetime import datetime
from .embedding_manager import EmbeddingManager
from typing import List, Dict, Any

logger = logging.getLogger(__name__)

class ConversationMemory:
    """Stores and retrieves conversation history for Prometheus self-awareness"""
    
    def __init__(self, storage_path: str = "./prometheus_data"):
        self.storage_path = storage_path
        os.makedirs(storage_path, exist_ok=True)
        
        self.vectors_file = os.path.join(storage_path, "memory_vectors.npy")
        self.metadata_file = os.path.join(storage_path, "memory_metadata.json")
        
        self.embedder = EmbeddingManager()
        
        # In-memory storage
        self.vectors = None
        self.metadata = []
        
        self._load_data()
    
    def _load_data(self):
        """Load memory vectors and metadata from disk"""
        try:
            if os.path.exists(self.vectors_file):
                self.vectors = np.load(self.vectors_file)
            
            if os.path.exists(self.metadata_file):
                with open(self.metadata_file, 'r', encoding='utf-8') as f:
                    self.metadata = json.load(f)
            
            logger.info(f"Loaded {len(self.metadata)} conversation memories from disk.")
        except Exception as e:
            logger.error(f"Failed to load conversation memory: {e}")
            self.vectors = None
            self.metadata = []
    
    def _save_data(self):
        """Save memory vectors and metadata to disk"""
        try:
            if self.vectors is not None:
                np.save(self.vectors_file, self.vectors)
            
            with open(self.metadata_file, 'w', encoding='utf-8') as f:
                json.dump(self.metadata, f, ensure_ascii=False, indent=2)
            
            logger.info(f"Saved {len(self.metadata)} conversation memories to disk.")
        except Exception as e:
            logger.error(f"Failed to save conversation memory: {e}")
    
    def remember(self, question: str, response: str, user_id: int = None):
        """Store a conversation exchange in memory"""
        timestamp = datetime.now().isoformat()
        
        # Create a combined text for embedding that captures the full exchange
        combined_text = f"User asked: {question}\nPrometheus answered: {response}"
        
        try:
            vec = self.embedder.get_dense_embeddings(combined_text)[0]
            
            entry_id = f"mem_{int(datetime.now().timestamp())}"
            
            new_meta = {
                "id": entry_id,
                "timestamp": timestamp,
                "question": question,
                "response": response,
                "user_id": user_id,
                "text": combined_text,
                "source": "conversation"
            }
            
            if self.vectors is None:
                self.vectors = np.array([vec])
            else:
                self.vectors = np.vstack([self.vectors, np.array([vec])])
            
            self.metadata.append(new_meta)
            self._save_data()
            
            logger.info(f"Remembered conversation: {question[:50]}...")
            return True
            
        except Exception as e:
            logger.error(f"Failed to remember conversation: {e}")
            return False
    
    def recall(self, query: str, limit: int = 5) -> List[Dict[str, Any]]:
        """Search conversation memory for relevant past exchanges"""
        if self.vectors is None or len(self.metadata) == 0:
            return []
        
        try:
            query_vec = np.array(self.embedder.get_dense_embeddings(query)[0])
            
            # Cosine similarity search
            norms = np.linalg.norm(self.vectors, axis=1)
            query_norm = np.linalg.norm(query_vec)
            
            if query_norm == 0:
                return []
            
            valid_indices = norms > 0
            if not np.any(valid_indices):
                return []
            
            similarities = np.dot(self.vectors[valid_indices], query_vec) / (norms[valid_indices] * query_norm)
            
            # Get top results
            top_indices = np.argsort(similarities)[::-1][:limit]
            
            results = []
            actual_metadata = [m for i, m in enumerate(self.metadata) if valid_indices[i]]
            
            for idx in top_indices:
                if similarities[idx] > 0.3:  # Only include if somewhat relevant
                    results.append(actual_metadata[idx])
            
            return results
            
        except Exception as e:
            logger.error(f"Failed to recall memories: {e}")
            return []
    
    def get_recent(self, count: int = 3) -> List[Dict[str, Any]]:
        """Get the most recent conversations"""
        if not self.metadata:
            return []
        return self.metadata[-count:]


if __name__ == "__main__":
    logging.basicConfig(level=logging.INFO)
    memory = ConversationMemory()
    
    # Test
    memory.remember("What are my notes?", "I see you just started your journey!")
    results = memory.recall("What did I ask before?")
    print(f"Recalled: {results}")
