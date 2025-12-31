#!/usr/bin/env python3
"""
Prometheus Terminal Chat
A terminal-based interface for chatting with Prometheus AI.
"""

import requests
import os
import sys
from datetime import datetime

# Configuration
API_BASE = "http://127.0.0.1:8080"
USER_ID = 1

# ANSI Colors
class Colors:
    CYAN = '\033[96m'
    GREEN = '\033[92m'
    YELLOW = '\033[93m'
    RED = '\033[91m'
    MAGENTA = '\033[95m'
    BOLD = '\033[1m'
    DIM = '\033[2m'
    RESET = '\033[0m'

def print_banner():
    """Print the Prometheus welcome banner"""
    banner = f"""
{Colors.CYAN}╔═══════════════════════════════════════════════════════════╗
║  {Colors.BOLD}🔥 PROMETHEUS{Colors.RESET}{Colors.CYAN} - Your Personal AI Companion               ║
║     Type your message and press Enter to chat               ║
║     Commands: /quit, /clear, /memories, /health             ║
╚═══════════════════════════════════════════════════════════╝{Colors.RESET}
"""
    print(banner)

def check_health():
    """Check if Prometheus server is running"""
    try:
        response = requests.get(f"{API_BASE}/health", timeout=5)
        if response.status_code == 200:
            data = response.json()
            memories = data.get("memories", 0)
            print(f"{Colors.GREEN}✓ Prometheus is online ({memories} memories){Colors.RESET}")
            return True
        return False
    except:
        return False

def start_server():
    """Try to start the Prometheus server"""
    print(f"{Colors.YELLOW}Starting Prometheus server...{Colors.RESET}")
    # This assumes you're running from the project root
    import subprocess
    import time
    
    try:
        proc = subprocess.Popen(
            [sys.executable, "-m", "prometheus.server"],
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            cwd=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
        )
        
        # Wait for it to start
        for _ in range(10):
            time.sleep(1)
            if check_health():
                return True
        
        print(f"{Colors.RED}✗ Server failed to start{Colors.RESET}")
        return False
    except Exception as e:
        print(f"{Colors.RED}✗ Error: {e}{Colors.RESET}")
        return False

def ask_prometheus(question: str, api_key: str = None) -> str:
    """Send a question to Prometheus and get an AI response"""
    try:
        # Get context and memory from Prometheus server
        response = requests.post(
            f"{API_BASE}/ask",
            json={"question": question, "limit": 10, "user_id": USER_ID},
            timeout=30
        )
        
        if response.status_code != 200:
            return f"Error: {response.status_code}"
        
        data = response.json()
        context = data.get("context", [])
        memory = data.get("memory", [])
        
        # Build context string
        context_text = ""
        if context:
            context_text = "From your journal:\n"
            for item in context:
                date = item.get("date", "?")
                text = item.get("text", "")
                context_text += f"- [{date}] {text}\n"
        
        memory_text = ""
        if memory:
            memory_text = "\n\nYou also remember these past conversations:\n"
            for m in memory:
                q = m.get("question", "")
                memory_text += f"- When asked '{q}', {m.get('text', '')}\n"
        
        # Build the prompt
        current_date = datetime.now().strftime("%B %d, %Y at %I:%M %p")
        
        prompt = f"""You are Prometheus, a warm and supportive AI companion who knows the user personally.
You have genuine memory of past conversations and can reference them naturally.
Speak casually like a close friend. Be encouraging, insightful, and occasionally playful.

IMPORTANT - Today is {current_date}. Use this to correctly reason about dates.
If you recall saying something wrong in a past conversation, acknowledge it gracefully.

{context_text}{memory_text}

User's question: {question}

Respond conversationally. Reference past conversations naturally if relevant:"""

        # If we have an API key, call Gemini
        if api_key:
            ai_response = call_gemini(prompt, api_key)
            
            # Remember this conversation
            try:
                requests.post(
                    f"{API_BASE}/remember",
                    json={"question": question, "response": ai_response, "user_id": USER_ID},
                    timeout=10
                )
            except:
                pass  # Don't fail if memory save fails
            
            return ai_response
        else:
            return f"[No API key - showing context]\n{context_text}{memory_text}"
        
    except requests.exceptions.ConnectionError:
        return "Error: Cannot connect to Prometheus server. Is it running?"
    except Exception as e:
        return f"Error: {e}"

def call_gemini(prompt: str, api_key: str) -> str:
    """Call Gemini API for AI response"""
    try:
        response = requests.post(
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-3-flash-preview:generateContent",
            params={"key": api_key},
            json={
                "contents": [{"parts": [{"text": prompt}]}]
            },
            timeout=60
        )
        
        if response.status_code == 200:
            data = response.json()
            return data["candidates"][0]["content"]["parts"][0]["text"]
        else:
            return f"Gemini API error: {response.status_code}"
    except Exception as e:
        return f"AI Error: {e}"

def show_memories():
    """Show recent memories"""
    try:
        response = requests.get(f"{API_BASE}/health", timeout=5)
        data = response.json()
        print(f"\n{Colors.MAGENTA}📚 Prometheus has {data.get('memories', 0)} memories stored.{Colors.RESET}\n")
    except:
        print(f"{Colors.RED}Cannot retrieve memory info.{Colors.RESET}")

def clear_screen():
    """Clear the terminal screen"""
    os.system('cls' if os.name == 'nt' else 'clear')
    print_banner()

def load_api_key():
    """Load API key from environment or config"""
    # First check environment
    api_key = os.environ.get("GOOGLE_API_KEY")
    if api_key:
        return api_key
    
    # Check config file in kegomodoro
    config_path = os.path.join(
        os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
        "kegomodoro", "user_config.json"
    )
    if os.path.exists(config_path):
        try:
            import json
            with open(config_path, 'r') as f:
                config = json.load(f)
                return config.get("gemini_api_key")
        except:
            pass
    
    return None

def main():
    """Main chat loop"""
    clear_screen()
    
    # Load API key
    api_key = load_api_key()
    if api_key:
        print(f"{Colors.GREEN}✓ API key loaded{Colors.RESET}")
    else:
        print(f"{Colors.YELLOW}⚠ No API key found. Set GOOGLE_API_KEY or use /key command.{Colors.RESET}")
    
    # Check if server is running
    if not check_health():
        print(f"{Colors.YELLOW}⚠ Prometheus server not detected.{Colors.RESET}")
        print(f"Please start KeganOS or run: python -m prometheus.server")
        print(f"\n{Colors.DIM}Waiting for server...{Colors.RESET}")
        
        while not check_health():
            try:
                input(f"{Colors.DIM}Press Enter to retry or Ctrl+C to exit...{Colors.RESET}")
            except KeyboardInterrupt:
                print(f"\n{Colors.DIM}Goodbye!{Colors.RESET}")
                return
    
    print(f"\n{Colors.GREEN}Ready to chat! Type your message below.{Colors.RESET}\n")
    
    while True:
        try:
            # Get user input
            user_input = input(f"{Colors.CYAN}You:{Colors.RESET} ").strip()
            
            if not user_input:
                continue
            
            # Handle commands
            if user_input.lower() == "/quit":
                print(f"\n{Colors.MAGENTA}Prometheus:{Colors.RESET} Until next time, friend! 🔥\n")
                break
            elif user_input.lower() == "/clear":
                clear_screen()
                continue
            elif user_input.lower() == "/memories":
                show_memories()
                continue
            elif user_input.lower() == "/health":
                check_health()
                continue
            elif user_input.lower().startswith("/key "):
                api_key = user_input[5:].strip()
                print(f"{Colors.GREEN}✓ API key set!{Colors.RESET}")
                continue
            elif user_input.lower() == "/key":
                print(f"{Colors.DIM}Usage: /key YOUR_API_KEY{Colors.RESET}")
                continue
            elif user_input.startswith("/"):
                print(f"{Colors.DIM}Commands: /quit, /clear, /memories, /health, /key{Colors.RESET}")
                continue
            
            # Send to Prometheus
            print(f"\n{Colors.DIM}Thinking...{Colors.RESET}")
            response = ask_prometheus(user_input, api_key)
            
            # Display response
            print(f"\n{Colors.MAGENTA}Prometheus:{Colors.RESET} {response}\n")
            
        except KeyboardInterrupt:
            print(f"\n\n{Colors.MAGENTA}Prometheus:{Colors.RESET} See you later! 🔥\n")
            break
        except EOFError:
            break

if __name__ == "__main__":
    main()
