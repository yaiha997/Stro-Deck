# Stream Deck PC

Stream Deck PC turns a Windows computer into a customizable remote control. It runs a small web server on the computer and serves a button grid to a browser on another device on the same local network. Configure buttons in the desktop app; pressing a button in the browser performs its configured action on the Windows computer.

## Requirements

- Windows
- A phone, tablet, or other browser-enabled device on the same local network
- Windows PowerShell and the .NET Framework C# compiler (`csc.exe`) to rebuild from source
- Optional: Google Nest Hub with WebView remote debugging enabled
- Optional: OpenCode and Windows Terminal for the Greeting project launcher described below

## Run the application

1. Start `StreamDeckPC.exe`.
2. The desktop app displays the address to use, for example `http://192.168.1.8:8090/`.
3. Open that address in a browser on the other device. Both devices must be on the same reachable network.
4. If Windows Firewall prompts you, allow the app on your trusted private network.

The server listens on port 8090 by default and tries the next 19 ports if that port is unavailable. The displayed address reflects the active port and computer IP address.

## How it works

1. `Program.cs` starts the Windows Forms desktop application.
2. `MainForm.cs` provides the button editor, server controls, and configuration persistence.
3. `Deck.cs` loads the deck settings, runs the local HTTP server, and dispatches button actions.
4. `deck.html` is embedded into the executable and rendered by the remote device's browser. The page requests the current deck state from `/api/state` and sends button presses to `/api/press`.
5. Button actions run on the Windows computer. They can open apps and websites, run CMD or PowerShell commands, send keyboard/media input, type text, or perform power actions.

The configuration is stored in `deck-config.txt` beside the executable and is managed by the desktop app. The app creates default buttons when no configuration exists.

## Customize the deck

Select a button in the desktop app, edit its label, icon, color, action, and target, then select **Save button**. Use **Test action** to try an action on the computer. Drag buttons to swap their positions, or change the number of columns and rows.

The editor can also select image icons and a background image. Images are copied into the `icons` and `backgrounds` folders so the web page can serve them. The background position, clock display, and other appearance options are configurable in the app.

Available action types include:

| Action | Example target | Result |
|---|---|---|
| Open app / file | `notepad.exe`, `C:\Work`, `ms-settings:` | Opens an app, file, folder, or Windows URI |
| Open website | `https://example.com` | Opens a website |
| Run CMD command | `explorer C:\Work` | Runs a command on the Windows computer |
| Run PowerShell | `Get-Process` | Runs a PowerShell command |
| Media key | `play`, `next`, `mute`, `volup` | Sends a media control |
| Send keys | `win+shift+s`, `ctrl+shift+esc` | Sends a keyboard shortcut |
| Type text | `Hello` | Types text into the active window |
| System action | `lock`, `sleep`, `monitoroff`, `shutdown`, `restart` | Performs the selected system action |

### Launch OpenCode in the Greeting project

To add an OpenCode button:

1. Confirm OpenCode is installed and available from a terminal as `opencode`. Install Windows Terminal if you want the button to open a visible terminal window.
2. In the desktop app, select an unused button and set its label to `OpenCode`, choose an icon, and set **Action** to **Run CMD command**.
3. Set **Target** to the following command, replacing the example path if your `Greeting` project is elsewhere:

   ```cmd
   start "" wt.exe -d "C:\Users\pc\Documents\Greeting" opencode
   ```

4. Save the button and select **Test action**. Pressing the button opens Windows Terminal in the `Greeting` project directory and starts OpenCode there.

This button launches OpenCode in the existing project folder; it does not create or copy the `Greeting` project.

## Google Nest Hub

For a Nest Hub that supports WebView remote debugging:

1. On the Hub, open **Settings → About**, select **Fuchsia version** repeatedly to enable Developer Options, and enable **WebView remote debugging**.
2. Ensure the Hub and computer are on the same network.
3. Run a connectivity check:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\hub-check.ps1 -Ip <hub-ip>
   ```

4. If DevTools is available, navigate the Hub WebView to the Stream Deck page:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\hub-push.ps1 -Ip <hub-ip> -Port 9222 -Url http://<computer-ip>:8090/
   ```

Casting a Chrome tab can display the page, but does not forward touch input.

## Build from source

Run this from PowerShell in the project directory:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

The build script compiles `Deck.cs`, `Ui.cs`, `MainForm.cs`, and `Program.cs`, embeds `deck.html`, and produces `StreamDeckPC.exe`. Rebuild after changing source code or the page.

## Project files

| File or directory | Purpose |
|---|---|
| `StreamDeckPC.exe` | Windows desktop app and local web server |
| `Deck.cs` | Configuration, action execution, HTTP server, and casting logic |
| `MainForm.cs`, `Ui.cs`, `Program.cs` | Desktop editor and application startup |
| `deck.html` | Remote browser interface |
| `deck-config.txt` | Current app configuration |
| `build.ps1` | Source build script |
| `hub-check.ps1`, `hub-push.ps1` | Nest Hub connectivity and WebView helpers |
| `backgrounds/`, `icons/` | Image assets served by the app |
| `tools/` | Additional network scanning utilities |

## Troubleshooting

- **The page does not load:** Check that both devices are on the same network, the Windows Firewall allows the app on the private network, and you are using the computer's LAN IP rather than `127.0.0.1`.
- **The computer's IP changes:** Use the current address displayed by the desktop app.
- **A button does not work:** Use **Test action** on the computer and confirm the target is valid.
- **The port is busy:** The server tries ports 8090 through 8109; use the address displayed by the app.
- **OpenCode does not launch:** Run `opencode` from a terminal first, verify the `Greeting` folder path, and confirm Windows Terminal is installed and available as `wt.exe`.

## Security

The local web server does not require authentication, and button actions execute on the Windows computer. Only run the app on a trusted network and do not expose its port to the public internet.
