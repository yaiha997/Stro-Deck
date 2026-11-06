using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace StroDeck
{
    internal sealed class LocalAddressCandidate
    {
        public string Address;
        public int Priority;
    }

    internal static class LocalAddresses
    {
        static readonly Regex Ipv4Pattern = new Regex(@"(?<!\d)(?:\d{1,3}\.){3}\d{1,3}(?!\d)");

        public static List<string> Discover()
        {
            List<LocalAddressCandidate> candidates = new List<LocalAddressCandidate>();
            try
            {
                foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.OperationalStatus != OperationalStatus.Up ||
                        adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;

                    int typePriority = adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? 0 :
                        adapter.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? 1 : 2;
                    int priority = 10 + typePriority;
                    IPInterfaceProperties properties;
                    try
                    {
                        properties = adapter.GetIPProperties();
                        foreach (GatewayIPAddressInformation gateway in properties.GatewayAddresses)
                        {
                            if (gateway.Address.AddressFamily == AddressFamily.InterNetwork &&
                                !gateway.Address.Equals(IPAddress.Any))
                            {
                                priority = typePriority;
                                break;
                            }
                        }
                    }
                    catch (NetworkInformationException)
                    {
                        continue;
                    }

                    foreach (UnicastIPAddressInformation address in properties.UnicastAddresses)
                    {
                        if (address.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        candidates.Add(new LocalAddressCandidate
                        {
                            Address = address.Address.ToString(),
                            Priority = priority
                        });
                    }
                }
            }
            catch (NetworkInformationException)
            {
            }

            return OrderCandidates(candidates);
        }

        public static string FormatDisplayUrl()
        {
            return "http://<computer-ip>:<port>/";
        }

        public static string RedactIpv4(string text)
        {
            return text == null ? "" : Ipv4Pattern.Replace(text, "[IPv4]");
        }

        internal static List<string> OrderCandidates(IEnumerable<LocalAddressCandidate> candidates)
        {
            List<LocalAddressCandidate> usable = new List<LocalAddressCandidate>();
            foreach (LocalAddressCandidate candidate in candidates)
            {
                IPAddress address;
                if (candidate == null || !IPAddress.TryParse(candidate.Address, out address) ||
                    address.AddressFamily != AddressFamily.InterNetwork || !IsUsable(address))
                    continue;
                usable.Add(candidate);
            }

            usable.Sort(delegate(LocalAddressCandidate left, LocalAddressCandidate right)
            {
                return left.Priority.CompareTo(right.Priority);
            });

            List<string> result = new List<string>();
            foreach (LocalAddressCandidate candidate in usable)
            {
                if (!result.Contains(candidate.Address)) result.Add(candidate.Address);
            }
            if (result.Count == 0) result.Add(IPAddress.Loopback.ToString());
            return result;
        }

        static bool IsUsable(IPAddress address)
        {
            if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any)) return false;
            byte[] bytes = address.GetAddressBytes();
            if (bytes[0] == 169 && bytes[1] == 254) return false;
            if (bytes[0] == 0 || bytes[0] >= 224) return false;
            return true;
        }
    }
}
