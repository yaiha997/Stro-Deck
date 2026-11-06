import asyncio
import os
from pathlib import Path
import shutil
import subprocess
import sys

from mcp.server.fastmcp import FastMCP

mcp = FastMCP("Stro-Deck Nest Hub")

HUB_IP = os.environ.get("STRO_DECK_HUB_IP", "").strip()
DEFAULT_URL = os.environ.get("STRO_DECK_URL", "").strip()


def find_catt():
    configured = os.environ.get("STRO_DECK_CATT_PATH")
    if configured:
        return configured

    installed = shutil.which("catt")
    if installed:
        return installed

    python_dir = Path(sys.executable).resolve().parent
    for candidate in (python_dir / "Scripts" / "catt.exe", python_dir / "catt.exe"):
        if candidate.is_file():
            return str(candidate)
    return None


async def run_catt(args):
    catt_path = find_catt()
    if not catt_path:
        return "Error: catt was not found. Run setup_mcp.ps1 to install and configure it."
    if not HUB_IP:
        return "Error: Set STRO_DECK_HUB_IP to the Nest Hub IP address."

    env = os.environ.copy()
    env["PYTHONIOENCODING"] = "utf-8"
    cmd = [catt_path, "-d", HUB_IP] + args
    try:
        process = await asyncio.create_subprocess_exec(
            *cmd,
            env=env,
            stdin=subprocess.DEVNULL,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        )
    except FileNotFoundError:
        return f"Error: catt executable not found: {catt_path}"
    try:
        stdout, stderr = await asyncio.wait_for(process.communicate(), timeout=30)
    except asyncio.TimeoutError:
        process.kill()
        await process.communicate()
        return "Error: catt did not respond within 30 seconds."

    output = stdout.decode("utf-8", errors="replace").strip()
    if process.returncode == 0:
        return output or "Command completed successfully."
    error = stderr.decode("utf-8", errors="replace").strip() or output or f"catt exited with code {process.returncode}."
    return f"Error: {error}"


@mcp.tool()
async def cast_site(url: str = "") -> str:
    """Cast a web page or site (like Stro-Deck) to Google Nest Hub."""
    target = url.strip() or DEFAULT_URL
    if not target:
        return "Error: Configure STRO_DECK_URL with the current address shown by the Stro-Deck app, or pass a URL."
    return await run_catt(["cast_site", target])


@mcp.tool()
async def stop_cast() -> str:
    """Stop currently active Cast session on the Nest Hub."""
    return await run_catt(["stop"])


@mcp.tool()
async def play_media(url: str) -> str:
    """Play a supported media URL on the Nest Hub."""
    return await run_catt(["cast", url])


@mcp.tool()
async def set_volume(level: int) -> str:
    """Set Nest Hub volume to a value from 0 to 100."""
    if level < 0 or level > 100:
        return "Error: level must be between 0 and 100."
    return await run_catt(["volume", str(level)])


@mcp.tool()
async def hub_status() -> str:
    """Get current Google Cast status and playback state from the Nest Hub."""
    return await run_catt(["status"])


if __name__ == "__main__":
    mcp.run()
