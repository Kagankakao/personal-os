from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from typing import Optional, List
import logging
import os
from .engine import PrometheusEngine
from .ingestor import JournalIngestor
from .memory import ConversationMemory

# Setup logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("prometheus_server")

app = FastAPI(title="Prometheus RAG Server")

# Initialize components
db_path = os.environ.get("KEGANOS_DB_PATH", "../keganos.db")
engine = None
ingestor = None
memory = None

@app.on_event("startup")
async def startup_event():
    global engine, ingestor, memory
    logger.info("Starting Prometheus Engine...")
    try:
        engine = PrometheusEngine()
        ingestor = JournalIngestor(db_path, engine)
        memory = ConversationMemory()
        
        # Auto-sync on startup (Non-blocking background sync)
        import asyncio
        logger.info("Scheduling background journal sync...")
        asyncio.create_task(asyncio.to_thread(ingestor.sync))
        logger.info(f"Prometheus ready. Memory contains {len(memory.metadata)} past conversations.")
    except Exception as e:
        logger.error(f"Startup failed: {e}")

class QueryRequest(BaseModel):
    question: str
    user_id: Optional[int] = None
    limit: Optional[int] = 5

class RememberRequest(BaseModel):
    question: str
    response: str
    user_id: Optional[int] = None

class SyncRequest(BaseModel):
    user_id: Optional[int] = None

@app.post("/ask")
async def ask(request: QueryRequest):
    """Semantic search endpoint with memory context"""
    if not engine:
        raise HTTPException(status_code=503, detail="Prometheus not initialized")
    
    try:
        # Search journal entries
        journal_results = engine.search(request.question, limit=request.limit)
        
        # Search conversation memory
        memory_results = []
        if memory:
            memory_results = memory.recall(request.question, limit=3)
        
        # Format journal results
        formatted_journal = []
        for p in journal_results:
            formatted_journal.append({
                "text": p.payload.get("text", ""),
                "date": p.payload.get("date", ""),
                "score": p.score,
                "source": "journal",
                "metadata": p.payload
            })
        
        # Format memory results
        formatted_memory = []
        for m in memory_results:
            formatted_memory.append({
                "text": f"You previously said: {m.get('response', '')[:200]}",
                "date": m.get("timestamp", ""),
                "question": m.get("question", ""),
                "source": "memory"
            })
            
        return {
            "answer": "Search results returned",
            "context": formatted_journal,
            "memory": formatted_memory
        }
    except Exception as e:
        logger.error(f"Search failed: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/remember")
async def remember(request: RememberRequest):
    """Store a conversation exchange in memory"""
    if not memory:
        raise HTTPException(status_code=503, detail="Memory not initialized")
    
    try:
        success = memory.remember(request.question, request.response, request.user_id)
        return {"status": "success" if success else "failed", "memory_size": len(memory.metadata)}
    except Exception as e:
        logger.error(f"Remember failed: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/sync")
async def sync(request: SyncRequest):
    """Trigger database synchronization"""
    if not ingestor:
        raise HTTPException(status_code=503, detail="Ingestor not initialized")
    
    try:
        ingestor.sync()
        return {"status": "success", "message": "Database synchronized"}
    except Exception as e:
        logger.error(f"Sync failed: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.get("/health")
async def health():
    memory_count = len(memory.metadata) if memory else 0
    return {"status": "ok", "engine": "ready" if engine else "startup", "memories": memory_count}

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="127.0.0.1", port=8080)
