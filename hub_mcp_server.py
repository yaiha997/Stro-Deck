import os
import sys
import subprocess
from mcp.server.fastmcp import FastMCP

# Initialize FastMCP Server for Google Nest Hub
mcp = FastMCP("Google-Nest-Hub-Controller")

HUB_IP = "192.168.68.102"
CATT_PATH = r"C:\Users\pc\AppData\Local\Programs\Python\Python312\Scripts\catt.exe"

def run_catt(args):
    env = os.environ.copy()
    env["PYTHONIOENCODING"] = "utf-8"
    cmd = [CATT_PATH, "-d", HUB_IP] + args
    result = subprocess.run(cmd, env=env, capture_output=True, text=True, encoding="utf-8", errors="replace")
    return result.stdout.strip() if result.returncode == 0 else f"Error: {result.stderr.strip()}"

@mcp.tool()
def cast_site(url: str = "http://192.168.68.108:8090/") -> str:
    """Cast a web page or site (like Stro-Deck) to Google Nest Hub."""
    return run_catt(["cast_site", url])

@mcp.tool()
def stop_cast() -> str:
    """Stop currently active cast / site on Nest Hub."""
    return run_catt(["stop"])

@mcp.tool()
def play_media(url: str) -> str:
    """Play YouTube or media URL on Nest Hub."""
    return run_catt(["cast", url])

@mcp.tool()
def set_volume(level: int) -> str:
    """Set volume level on Nest Hub (0 - 100)."""
    return run_catt(["volume", str(level)])

@mcp.tool()
def hub_status() -> str:
    """Get current status and playback state of Google Nest Hub."""
    return run_catt(["status"])

if __name__ == "__main__":
    mcp.run()
