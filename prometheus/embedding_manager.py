import logging
import os
import json
import requests
from typing import List, Union

logger = logging.getLogger(__name__)

class EmbeddingManager:
    def __init__(self, api_key: str = None):
        self.api_key = api_key or os.environ.get("GOOGLE_API_KEY")
        if not self.api_key:
            logger.warning("No Google API Key provided for Gemini Embeddings. Search will be disabled.")
        
        self.endpoint = "https://generativelanguage.googleapis.com/v1beta/models/embedding-001:embedContent"
        logger.info("Initializing EmbeddingManager with Google Gemini API")

    def get_dense_embeddings(self, texts: Union[str, List[str]]) -> List[List[float]]:
        """Generate dense vectors using Gemini API"""
        if not self.api_key:
            return []

        if isinstance(texts, str):
            texts = [texts]
        
        results = []
        for text in texts:
            try:
                payload = {
                    "model": "models/embedding-001",
                    "content": {"parts": [{"text": text}]}
                }
                params = {"key": self.api_key}
                response = requests.post(self.endpoint, params=params, json=payload)
                response.raise_for_status()
                
                embedding = response.json()["embedding"]["values"]
                results.append(embedding)
            except Exception as e:
                logger.error(f"Gemini Embedding failed for text: {e}")
                results.append([0.0] * 768) # Fallback to zero vector (Gemini embedding-001 is 768 or 1024)
        
        return results

    def get_sparse_embeddings(self, texts: Union[str, List[str]]):
        """Mock sparse embeddings for compatibility (or implement simple TF-IDF if needed)"""
        # For 'Lite' version, we'll rely on dense search and optionally SQLite FTS.
        return []

if __name__ == "__main__":
    logging.basicConfig(level=logging.INFO)
    apiKey = os.environ.get("GOOGLE_API_KEY")
    manager = EmbeddingManager(apiKey)
    if apiKey:
        test = manager.get_dense_embeddings("Hello world")
        print(f"Embedding size: {len(test[0])}")
    else:
        print("Set GOOGLE_API_KEY to test")
