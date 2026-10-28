using System;
using System.Collections.Generic;
using System.Linq;

namespace Skytomo221.Sobakasu.Compiler.Target
{
    public static class UdonAbiSignatureFormatter
    {
        public static string TypeToken(RuntimeTypeIdentity type)
        {
            if (type.Kind == RuntimeTypeIdentityKind.Array) return TypeToken(type.ElementType) + "Array";
            var token = UdonExternSignatureIdentity.FormatTypeName(type.RuntimeName);
            if (type.Kind == RuntimeTypeIdentityKind.ConstructedGeneric)
                token += string.Concat(type.TypeArguments.Select(TypeToken));
            return token switch
            {
                "SystemCollectionsGenericListT" => "ListT",
                "SystemCollectionsGenericIEnumerableT" => "IEnumerableT",
                _ => token
            };
        }

        public static string Callable(string host, string name, IReadOnlyList<string> parameters,
            string returnToken, bool constructor = false)
        {
            var parameterToken = parameters.Count > 0 ? "__" + string.Join("_", parameters) :
                constructor ? "__" : string.Empty;
            return UdonExternSignatureIdentity.FormatHost(host) + ".__" + name.Trim('_').TrimStart('.') +
                parameterToken + "__" + returnToken;
        }

        public static string Field(string host, string name, string valueToken, bool setter) =>
            FieldFromTokens(UdonExternSignatureIdentity.FormatTypeName(host), name, valueToken, setter);

        public static string FieldFromTokens(string hostToken, string name, string valueToken, bool setter) =>
            hostToken + ".__" + (setter ? "set_" : "get_") + name + "__" + valueToken;
    }
}
