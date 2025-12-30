import logging
import os
import json
import numpy as np
from .embedding_manager import EmbeddingManager
from typing import List, Dict, Any

logger = logging.getLogger(__name__)

class PrometheusEngine:
    def __init__(self, storage_path: str = "./prometheus_data"):
        self.storage_path = storage_path
        os.makedirs(storage_path, exist_ok=True)
        
        self.vectors_file = os.path.join(storage_path, "vectors.npy")
        self.metadata_file = os.path.join(storage_path, "metadata.json")
        
        self.embedder = EmbeddingManager()
        self.collection_name = "personal_journal"
        
        # In-memory storage
        self.vectors = None # np.array
        self.metadata = [] # list of dicts
        
        self._load_data()

    def _load_data(self):
        """Load vectors and metadata from disk"""
        try:
            if os.path.exists(self.vectors_file):
                self.vectors = np.load(self.vectors_file)
            
            if os.path.exists(self.metadata_file):
                with open(self.metadata_file, 'r', encoding='utf-8') as f:
                    self.metadata = json.load(f)
            
            logger.info(f"Loaded {len(self.metadata)} vectors from disk.")
        except Exception as e:
            logger.error(f"Failed to load Prometheus data: {e}")
            self.vectors = None
            self.metadata = []

    def _save_data(self):
        """Save vectors and metadata to disk"""
        try:
            if self.vectors is not None:
                np.save(self.vectors_file, self.vectors)
            
            with open(self.metadata_file, 'w', encoding='utf-8') as f:
                json.dump(self.metadata, f, ensure_ascii=False, indent=2)
                
            logger.info(f"Saved {len(self.metadata)} vectors to disk.")
        except Exception as e:
            logger.error(f"Failed to save Prometheus data: {e}")

    def search(self, query: str, limit: int = 5, date_filter=None) -> List[Any]:
        """Perform semantic search using Numpy Cosine Similarity"""
        if self.vectors is None or len(self.metadata) == 0:
            return []

        logger.info(f"Searching for: {query}")
        
        # 1. Get query embedding
        query_vec = np.array(self.embedder.get_dense_embeddings(query)[0])
        
        # 2. Compute Cosine Similarities
        # Normalize vectors for cosine similarity (dot product on normalized vectors)
        norms = np.linalg.norm(self.vectors, axis=1)
        query_norm = np.linalg.norm(query_vec)
        
        if query_norm == 0:
            return []
            
        # Avoid division by zero
        valid_indices = norms > 0
        if not np.any(valid_indices):
            return []
            
        similarities = np.dot(self.vectors[valid_indices], query_vec) / (norms[valid_indices] * query_norm)
        
        # 3. Sort and limit
        # Get indices of top results
        top_indices = np.argsort(similarities)[::-1][:limit]
        
        results = []
        actual_metadata = [m for i, m in enumerate(self.metadata) if valid_indices[i]]
        
        for idx in top_indices:
            score = float(similarities[idx])
            meta = actual_metadata[idx]
            
            # Wrap in a compatibility object that looks like Qdrant's point
            results.append(type('Point', (), {
                'payload': meta,
                'score': score
            }))
            
        return results

    def upsert_entries(self, entries):
        """Upsert journal entries into local store"""
        # entries: list of dicts {id, text, metadata}
        new_vectors = []
        new_metadata = []
        
        # Build map for existing IDs to allow updates
        id_to_idx = { m.get("id"): i for i, m in enumerate(self.metadata) }
        
        for entry in entries:
            text = entry['text']
            vec = self.embedder.get_dense_embeddings(text)[0]
            
            eid = entry.get('id')
            if eid in id_to_idx:
                idx = id_to_idx[eid]
                # Update existing
                if self.vectors is not None:
                    self.vectors[idx] = vec
                self.metadata[idx] = entry['metadata']
                self.metadata[idx]["id"] = eid
            else:
                # Add new
                new_vectors.append(vec)
                meta = entry['metadata']
                meta["id"] = eid
                new_metadata.append(meta)
        
        if new_vectors:
            if self.vectors is None:
                self.vectors = np.array(new_vectors)
            else:
                self.vectors = np.vstack([self.vectors, np.array(new_vectors)])
            self.metadata.extend(new_metadata)
            
        self._save_data()

if __name__ == "__main__":
    logging.basicConfig(level=logging.INFO)
    engine = PrometheusEngine()
    print("Prometheus Lite Engine initialized with Numpy")
