import asyncio
import os
from pathlib import Path
import subprocess
import sys
import tempfile

from mcp import ClientSession
from mcp.client.stdio import StdioServerParameters, stdio_client

REPO = Path(__file__).resolve().parent
SERVER = REPO / "hub_mcp_server.py"

FAKE_CATT_SOURCE = r"""
using System;
class FakeCatt
{
    static void Main(string[] args)
    {
        Console.WriteLine(String.Join(" ", args));
    }
}
"""


def build_fake_catt(directory):
    windir = Path(os.environ.get("WINDIR", r"C:\Windows"))
    compilers = [
        windir / "Microsoft.NET" / "Framework64" / "v4.0.30319" / "csc.exe",
        windir / "Microsoft.NET" / "Framework" / "v4.0.30319" / "csc.exe",
    ]
    compiler = next((path for path in compilers if path.is_file()), None)
    if not compiler:
        raise RuntimeError("The .NET Framework C# compiler (csc.exe) is required for the sandbox test.")

    source = directory / "FakeCatt.cs"
    executable = directory / "catt-test.exe"
    source.write_text(FAKE_CATT_SOURCE, encoding="utf-8")
    result = subprocess.run(
        [str(compiler), "/nologo", "/target:exe", f"/out:{executable}", str(source)],
        capture_output=True,
        text=True,
        check=False,
    )
    if result.returncode:
        raise RuntimeError(f"Could not build the fake catt command: {result.stderr or result.stdout}")
    return executable


async def run_test(fake_catt):
    env = os.environ.copy()
    env.update(
        {
            "STRO_DECK_HUB_IP": "192.0.2.10",
            "STRO_DECK_URL": "http://127.0.0.1:8090/",
            "STRO_DECK_CATT_PATH": str(fake_catt),
        }
    )
    parameters = StdioServerParameters(
        command=sys.executable,
        args=[str(SERVER)],
        env=env,
    )

    async with stdio_client(parameters) as (read_stream, write_stream):
        async with ClientSession(read_stream, write_stream) as session:
            await session.initialize()
            tools = await session.list_tools()
            names = {tool.name for tool in tools.tools}
            expected = {"cast_site", "stop_cast", "play_media", "set_volume", "hub_status"}
            if names != expected:
                raise AssertionError(f"Unexpected MCP tools: {sorted(names)}")
            print(f"PASS: MCP initialized; discovered {len(names)} tools.")

            status = await session.call_tool("hub_status", {})
            status_text = "\n".join(item.text for item in status.content if hasattr(item, "text"))
            if status.isError or status_text != "-d 192.0.2.10 status":
                raise AssertionError(f"hub_status test failed: {status_text}")
            print("PASS: hub_status invoked the isolated fake catt command with the configured Hub IP.")

            volume = await session.call_tool("set_volume", {"level": 37})
            volume_text = "\n".join(item.text for item in volume.content if hasattr(item, "text"))
            if volume.isError or volume_text != "-d 192.0.2.10 volume 37":
                raise AssertionError(f"set_volume test failed: {volume_text}")
            print("PASS: set_volume invoked the isolated fake catt command.")

            invalid = await session.call_tool("set_volume", {"level": 101})
            invalid_text = "\n".join(item.text for item in invalid.content if hasattr(item, "text"))
            if invalid.isError or "between 0 and 100" not in invalid_text:
                raise AssertionError(f"Invalid volume was not rejected clearly: {invalid_text}")
            print("PASS: Invalid volume was rejected without contacting a device.")


def main():
    if not SERVER.is_file():
        raise RuntimeError(f"MCP server was not found: {SERVER}")
    with tempfile.TemporaryDirectory(prefix="stro-deck-mcp-test-") as temp:
        fake_catt = build_fake_catt(Path(temp))
        asyncio.run(run_test(fake_catt))
    print("All sandbox MCP checks passed. No network request was sent to a Nest Hub.")


if __name__ == "__main__":
    main()
