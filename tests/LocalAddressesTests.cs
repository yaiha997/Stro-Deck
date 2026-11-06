using System;
using System.Collections.Generic;

namespace StroDeck
{
    internal static class LocalAddressesTests
    {
        static int Main()
        {
            string displayUrl = LocalAddresses.FormatDisplayUrl();
            Assert(displayUrl == "http://<computer-ip>:<port>/",
                "display URL is the exact privacy-safe placeholder");
            Assert(LocalAddresses.RedactIpv4("Failed to reach 192.0.2.10") == "Failed to reach [IPv4]",
                "IPv4 values in status and error text are redacted");

            List<string> selected = LocalAddresses.OrderCandidates(new LocalAddressCandidate[]
            {
                new LocalAddressCandidate { Address = "192.0.2.20", Priority = 2 },
                new LocalAddressCandidate { Address = "192.0.2.10", Priority = 0 },
                new LocalAddressCandidate { Address = "192.0.2.10", Priority = 1 }
            });
            Assert(selected.Count == 2 && selected[0] == "192.0.2.10" && selected[1] == "192.0.2.20",
                "multiple interfaces are ordered by preference and duplicate addresses are removed");

            List<string> fallback = LocalAddresses.OrderCandidates(new LocalAddressCandidate[0]);
            Assert(fallback.Count == 1 && fallback[0] == "127.0.0.1",
                "no usable adapter falls back to loopback");

            List<string> filtered = LocalAddresses.OrderCandidates(new LocalAddressCandidate[]
            {
                new LocalAddressCandidate { Address = "127.0.0.1", Priority = 0 },
                new LocalAddressCandidate { Address = "169.254.1.2", Priority = 0 },
                new LocalAddressCandidate { Address = "::1", Priority = 0 },
                new LocalAddressCandidate { Address = "not-an-address", Priority = 0 }
            });
            Assert(filtered.Count == 1 && filtered[0] == "127.0.0.1",
                "loopback, link-local, IPv6, and invalid candidates are not advertised");

            Console.WriteLine("All local-address selection checks passed.");
            return 0;
        }

        static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Failed: " + message);
        }
    }
}
