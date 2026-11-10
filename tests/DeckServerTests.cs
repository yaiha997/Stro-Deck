using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace StroDeck
{
    internal static class DeckServerTests
    {
        static int actions;

        static int Main()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "stro-deck-auth-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            DeckServer server = null;
            try
            {
                int port = FindPort();
                DeckConfig defaults = DeckConfig.Defaults();
                Assert(defaults.Buttons.Exists(delegate(DeckButton button)
                {
                    return button.Label == "Spotify" && button.Action == "app" && button.Target == "spotify:";
                }), "the default deck includes a Spotify launch tile");

                DeckConfig config = new DeckConfig();
                config.Buttons.Add(new DeckButton { Label = "Test", Action = "test" });
                server = new DeckServer(port, config, RunTestAction,
                    Path.Combine(tempDirectory, "server.key"), "12345678");
                Assert(server.Start(), "test server starts");

                string origin = "http://127.0.0.1:" + server.Port;
                string host = "127.0.0.1:" + server.Port;
                string pressBody = "{\"id\":\"0\"}";

                Response missingToken = Request(server.Port, "POST", "/api/press", host, origin,
                    null, "application/json", pressBody);
                Assert(missingToken.Status == 403 && actions == 0,
                    "press without a token is rejected without running the action (status " +
                    missingToken.Status + ", action count " + actions + ", server " + server.LastError + ")");

                Response invalidToken = Request(server.Port, "POST", "/api/press", host, origin,
                    "strodeck_session=not-a-valid-token", "application/json", pressBody);
                Assert(invalidToken.Status == 403 && actions == 0,
                    "press with an invalid token is rejected");

                Response missingOrigin = Request(server.Port, "POST", "/api/press", host, null,
                    null, "application/json", pressBody);
                Assert(missingOrigin.Status == 403 && actions == 0,
                    "press without an Origin header is rejected");

                Response wrongOrigin = Request(server.Port, "POST", "/api/pair", host,
                    "http://example.invalid:" + server.Port, null, "application/json",
                    "{\"code\":\"12345678\"}");
                Assert(wrongOrigin.Status == 403 && wrongOrigin.Header("Set-Cookie") == null,
                    "pairing from a different origin is rejected");

                Response wrongHost = Request(server.Port, "POST", "/api/pair",
                    "example.invalid:" + server.Port, "http://example.invalid:" + server.Port,
                    null, "application/json", "{\"code\":\"12345678\"}");
                Assert(wrongHost.Status == 403, "unrecognized Host header is rejected");

                Response wrongLoopbackHost = Request(server.Port, "POST", "/api/pair",
                    "127.0.0.2:" + server.Port, "http://127.0.0.2:" + server.Port,
                    null, "application/json", "{\"code\":\"12345678\"}");
                Assert(wrongLoopbackHost.Status == 403,
                    "loopback Host must match the request's actual local destination");

                Response pair = Request(server.Port, "POST", "/api/pair", host, origin,
                    null, "application/json", "{\"code\":\"12345678\"}");
                string cookie = pair.Header("Set-Cookie");
                Assert(pair.Status == 200 && cookie != null && cookie.Contains("HttpOnly") &&
                    cookie.Contains("SameSite=Strict") && cookie.Contains("Max-Age=604800"),
                    "valid local pairing issues a seven-day HttpOnly strict-samesite cookie");

                string cookiePair = cookie.Split(';')[0];
                Response validAction = Request(server.Port, "POST", "/api/press", host, origin,
                    cookiePair, "application/json", pressBody);
                Assert(validAction.Status == 200 && actions == 1,
                    "valid paired local action invokes the injected test runner");

                Response wrongHostAction = Request(server.Port, "POST", "/api/press",
                    "example.invalid:" + server.Port, "http://example.invalid:" + server.Port,
                    cookiePair, "application/json", pressBody);
                Assert(wrongHostAction.Status == 403 && actions == 1,
                    "a valid cookie cannot authorize an unrecognized Host");

                Response mismatchedLoopbackOrigin = Request(server.Port, "POST", "/api/press", host,
                    "http://localhost:" + server.Port, cookiePair, "application/json", pressBody);
                Assert(mismatchedLoopbackOrigin.Status == 403 && actions == 1,
                    "loopback hostname and numeric loopback origin cannot be mixed");

                Response localhostPair = Request(server.Port, "POST", "/api/pair",
                    "localhost:" + server.Port, "http://localhost:" + server.Port,
                    null, "application/json", "{\"code\":\"12345678\"}");
                Assert(localhostPair.Status == 200,
                    "localhost is accepted when the request actually arrives over loopback");

                for (int i = 0; i < 5; i++)
                {
                    Response badCode = Request(server.Port, "POST", "/api/pair",
                        host, origin, null, "application/json", "{\"code\":\"00000000\"}");
                    Assert(badCode.Status == 403, "invalid pairing code is rejected");
                }
                Response exhaustedPairing = Request(server.Port, "POST", "/api/pair",
                    host, origin, null, "application/json", "{\"code\":\"12345678\"}");
                Assert(exhaustedPairing.Status == 403, "pairing attempts are capped per client for the server run");

                Response publicState = Request(server.Port, "GET", "/api/state", host, null,
                    null, null, "");
                Assert(publicState.Status == 200,
                    "the read-only deck state remains available to a display without pairing");

                Response publicNowPlaying = Request(server.Port, "GET", "/api/now", host, null,
                    null, null, "");
                Assert(publicNowPlaying.Status == 403,
                    "now-playing metadata is not exposed without pairing");

                Response publicCover = Request(server.Port, "GET", "/api/cover", host, null,
                    null, null, "");
                Assert(publicCover.Status == 403,
                    "media artwork is not exposed without pairing");

                Response unauthorizedMedia = Request(server.Port, "POST", "/api/media", host, origin,
                    null, "application/json", "{\"cmd\":\"next\"}");
                Assert(unauthorizedMedia.Status == 403,
                    "media controls require a paired same-origin session");

                Response mediaWithoutOrigin = Request(server.Port, "POST", "/api/media", host, null,
                    cookiePair, "application/json", "{\"cmd\":\"next\"}");
                Assert(mediaWithoutOrigin.Status == 403,
                    "a valid cookie cannot authorize media controls without a same-origin request");

                Response invalidMedia = Request(server.Port, "POST", "/api/media", host, origin,
                    cookiePair, "application/json", "{\"cmd\":\"launch\"}");
                Assert(invalidMedia.Status == 400,
                    "unknown media commands are rejected");

                Response pairedNowPlaying = Request(server.Port, "GET", "/api/now", host, null,
                    cookiePair, null, "");
                Assert(pairedNowPlaying.Status == 200,
                    "paired browsers can read now-playing metadata");

                Response pairedCover = Request(server.Port, "GET", "/api/cover", host, null,
                    cookiePair, null, "");
                Assert(pairedCover.Status == 200 || pairedCover.Status == 404,
                    "paired browsers can read artwork or receive the no-cover response");

                Console.WriteLine("All deck-server authorization checks passed.");
                return 0;
            }
            finally
            {
                if (server != null) server.Stop();
                Thread.Sleep(100);
                if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
            }
        }

        static string RunTestAction(DeckButton button)
        {
            Interlocked.Increment(ref actions);
            return "mock action";
        }

        static int FindPort()
        {
            TcpListener temporary = new TcpListener(IPAddress.Loopback, 0);
            temporary.Start();
            int port = ((IPEndPoint)temporary.LocalEndpoint).Port;
            temporary.Stop();
            return port;
        }

        static Response Request(int port, string method, string path, string host, string origin,
            string cookie, string contentType, string body)
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body ?? "");
            StringBuilder request = new StringBuilder();
            request.Append(method).Append(' ').Append(path).Append(" HTTP/1.1\r\n");
            if (host != null) request.Append("Host: ").Append(host).Append("\r\n");
            if (origin != null) request.Append("Origin: ").Append(origin).Append("\r\n");
            if (cookie != null) request.Append("Cookie: ").Append(cookie).Append("\r\n");
            if (contentType != null) request.Append("Content-Type: ").Append(contentType).Append("\r\n");
            if (method == "POST") request.Append("Content-Length: ").Append(bodyBytes.Length).Append("\r\n");
            request.Append("Connection: close\r\n\r\n");

            using (TcpClient client = new TcpClient())
            {
                client.ReceiveTimeout = 5000;
                client.Connect(IPAddress.Loopback, port);
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] header = Encoding.ASCII.GetBytes(request.ToString());
                    stream.Write(header, 0, header.Length);
                    if (bodyBytes.Length > 0) stream.Write(bodyBytes, 0, bodyBytes.Length);
                    stream.Flush();
                    MemoryStream response = new MemoryStream();
                    byte[] buffer = new byte[2048];
                    int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                        response.Write(buffer, 0, read);
                    return Response.Parse(Encoding.UTF8.GetString(response.ToArray()));
                }
            }
        }

        static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Failed: " + message);
        }

        sealed class Response
        {
            public int Status;
            readonly System.Collections.Generic.Dictionary<string, string> headers =
                new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public string Header(string name)
            {
                string value;
                return headers.TryGetValue(name, out value) ? value : null;
            }

            public static Response Parse(string text)
            {
                Response result = new Response();
                string[] lines = text.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                if (lines.Length == 0) return result;
                string[] status = lines[0].Split(' ');
                if (status.Length > 1) int.TryParse(status[1], out result.Status);
                for (int i = 1; i < lines.Length; i++)
                {
                    int colon = lines[i].IndexOf(':');
                    if (colon > 0)
                        result.headers[lines[i].Substring(0, colon).Trim()] = lines[i].Substring(colon + 1).Trim();
                }
                return result;
            }
        }
    }
}
