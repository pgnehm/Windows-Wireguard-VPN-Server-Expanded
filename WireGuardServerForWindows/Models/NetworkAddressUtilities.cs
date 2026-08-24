using System;
using System.Collections.Generic;
using System.Numerics;
using System.Net;

namespace WireGuardServerForWindows.Models
{
    internal static class NetworkAddressUtilities
    {
        public static IEnumerable<IPAddress> EnumerateUsableClientAddresses(IPNetwork network)
        {
            int addressBits = GetAddressBits(network);
            BigInteger addressCount = BigInteger.One << (addressBits - network.PrefixLength);
            BigInteger endExclusive = IsIPv4(network) ? addressCount - 1 : addressCount;

            // Reserve the network address and the server's first address.
            for (BigInteger offset = 2; offset < endExclusive; offset++)
            {
                yield return GetAddressAt(network, offset);
            }
        }

        public static bool IsUsableClientAddress(IPNetwork network, IPAddress address)
        {
            if (network.Contains(address) == false || address.Equals(network.BaseAddress))
            {
                return false;
            }

            // The first address after the network address is reserved for the server.
            if (address.Equals(GetAddressAt(network, BigInteger.One)))
            {
                return false;
            }

            // IPv4 broadcast is not a client address. IPv6 has no broadcast address.
            if (IsIPv4(network))
            {
                int addressBits = GetAddressBits(network);
                BigInteger addressCount = BigInteger.One << (addressBits - network.PrefixLength);
                if (address.Equals(GetAddressAt(network, addressCount - 1)))
                {
                    return false;
                }
            }

            return true;
        }

        public static IPAddress GetFirstServerAddress(IPNetwork network)
        {
            return GetAddressAt(network, BigInteger.One);
        }

        private static IPAddress GetAddressAt(IPNetwork network, BigInteger offset)
        {
            byte[] addressBytes = network.BaseAddress.GetAddressBytes();
            byte[] offsetBytes = offset.ToByteArray(isUnsigned: true, isBigEndian: true);

            for (int i = 0; i < offsetBytes.Length; i++)
            {
                int addressIndex = addressBytes.Length - i - 1;
                if (addressIndex < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(offset));
                }

                int value = addressBytes[addressIndex] + offsetBytes[offsetBytes.Length - i - 1];
                addressBytes[addressIndex] = (byte)value;
                int carry = value >> 8;

                int carryIndex = addressIndex - 1;
                while (carry > 0 && carryIndex >= 0)
                {
                    value = addressBytes[carryIndex] + carry;
                    addressBytes[carryIndex] = (byte)value;
                    carry = value >> 8;
                    carryIndex--;
                }
            }

            return new IPAddress(addressBytes);
        }

        private static int GetAddressBits(IPNetwork network)
        {
            return IsIPv4(network) ? 32 : 128;
        }

        private static bool IsIPv4(IPNetwork network)
        {
            return network.BaseAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
        }
    }
}
