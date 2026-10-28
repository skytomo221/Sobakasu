using System;
using System.Text;

namespace Skytomo221.Sobakasu.Compiler.Target
{
    // A Udon callable consists of a host token and an ABI callable token. Rehosting
    // changes only the former; parameter and return tokens retain physical identity.
    public readonly struct UdonExternSignatureIdentity
    {
        private readonly string _callable;

        private UdonExternSignatureIdentity(string callable) => _callable = callable;

        public static bool TryParse(string signature, out UdonExternSignatureIdentity identity)
        {
            var separator = signature?.IndexOf(".__", StringComparison.Ordinal) ?? -1;
            identity = separator > 0
                ? new UdonExternSignatureIdentity(signature.Substring(separator + 1))
                : default;
            return separator > 0;
        }

        public string OnHost(string runtimeName) => FormatHost(runtimeName) + "." + _callable;

        public static string FormatHost(string runtimeName)
        {
            return (runtimeName ?? string.Empty).Replace(",", "").Replace(".", "")
                .Replace("[]", "Array").Replace("&", "Ref").Replace("+", "")
                .Replace("VRCUdonUdonBehaviour", "VRCUdonCommonInterfacesIUdonEventReceiver")
                .Replace("UdonSharpUdonSharpBehaviour", "VRCUdonCommonInterfacesIUdonEventReceiver");
        }

        public static string FormatTypeName(string runtimeName)
        {
            var result = new StringBuilder();
            var arity = false;
            foreach (var character in runtimeName ?? string.Empty)
            {
                if (character == '`') { arity = true; continue; }
                if (arity && char.IsDigit(character)) continue;
                arity = false;
                if (character != '.' && character != '+' && character != ',')
                    result.Append(character);
            }
            return result.ToString();
        }
    }
}
