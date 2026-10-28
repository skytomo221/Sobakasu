using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Skytomo221.Sobakasu.Compiler.Target.UdonApiCatalog
{
    // Early v2 catalogs classified generator-policy failures as unexposed. Recover
    // physical exposure from declarations and the persisted actual signature set.
    // No reflection or SDK discovery participates in this compatibility projection.
    public static class UdonApiCatalogExposureReconciler
    {
        public static IReadOnlyList<string> Reconcile(UdonApiCatalogData catalog)
        {
            var unmatched = new HashSet<string>(catalog.unmodeledUdonSignatures ?? new List<string>(), StringComparer.Ordinal);
            var recovered = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var ns in catalog.namespaces ?? new List<UdonApiNamespaceRecord>())
            {
                if (ns?.exposed == null) continue;
                var types = (ns.exposed.classes ?? new List<UdonApiClassRecord>()).Cast<UdonApiTypeRecord>()
                    .Concat(ns.exposed.structs ?? new List<UdonApiStructRecord>())
                    .Concat(ns.exposed.interfaces ?? new List<UdonApiInterfaceRecord>())
                    .Concat(ns.exposed.enums ?? new List<UdonApiEnumTypeRecord>());
                foreach (var type in types)
                {
                    if (type?.unexposed == null || type.path == null) continue;
                    var host = (string.IsNullOrEmpty(ns.name) ? "" : ns.name + ".") + string.Join("+", type.path);
                    var source = type.unexposed;
                    var destination = type.exposed ??= new UdonApiMemberExposure();
                    Move(source.constructors, ref destination.constructors,
                        item => Callable("ctor", item.parameters, UdonApiTypeRef.Named(host), null, true),
                        (item, signature) => item.udonSignature = signature);
                    Move(source.operators, ref destination.operators,
                        item => Callable(item.name, item.parameters, item.returnType, null),
                        (item, signature) => item.udonSignature = signature);
                    destination.instance = Project(source.instance, destination.instance);
                    destination.@static = Project(source.@static, destination.@static);

                    UdonApiMemberSet Project(UdonApiMemberSet input, UdonApiMemberSet output)
                    {
                        if (input == null) return output;
                        output ??= new UdonApiMemberSet();
                        Move(input.methods, ref output.methods,
                            item => Callable(item.name, item.parameters, item.returnType, item.genericParameters),
                            (item, signature) => item.udonSignature = signature);
                        Move(input.propertyGetters, ref output.propertyGetters,
                            item => Callable("get_" + item.name, item.parameters, item.type, null),
                            (item, signature) => item.udonSignature = signature);
                        Move(input.propertySetters, ref output.propertySetters,
                            item => Callable("set_" + item.name,
                                (item.parameters ?? new List<UdonApiParameterRecord>()).Concat(new[]
                                { new UdonApiParameterRecord { name = "value", type = item.type } }).ToList(),
                                UdonApiTypeRef.Named("System.Void"), null),
                            (item, signature) => item.udonSignature = signature);
                        Move(input.fieldGetters, ref output.fieldGetters,
                            item => UdonAbiSignatureFormatter.Field(host, item.name, Token(item.type, null), false),
                            (item, signature) => item.udonSignature = signature);
                        Move(input.fieldSetters, ref output.fieldSetters,
                            item => UdonAbiSignatureFormatter.Field(host, item.name, Token(item.type, null), true),
                            (item, signature) => item.udonSignature = signature);
                        return output;
                    }

                    string Callable(string name, List<UdonApiParameterRecord> parameters, UdonApiTypeRef result,
                        List<UdonApiGenericParameterRecord> generic, bool constructor = false) =>
                        UdonAbiSignatureFormatter.Callable(host, name,
                            (parameters ?? new List<UdonApiParameterRecord>()).Select(parameter =>
                                Token(parameter.type, generic) +
                                (parameter.passing is "out" or "ref" or "in" ? "Ref" : "")).ToArray(),
                            Token(result, generic), constructor);

                    string Token(UdonApiTypeRef reference, List<UdonApiGenericParameterRecord> methodParameters) =>
                        UdonAbiSignatureFormatter.TypeToken(Identity(reference, methodParameters));

                    RuntimeTypeIdentity Identity(UdonApiTypeRef reference, List<UdonApiGenericParameterRecord> methodParameters)
                    {
                        if (reference == null) throw new InvalidDataException("Missing catalog type reference.");
                        if (reference.named != null) return RuntimeTypeIdentity.Named(reference.named);
                        if (reference.array != null) return RuntimeTypeIdentity.Array(Identity(reference.array, methodParameters));
                        if (reference.generic != null) return RuntimeTypeIdentity.ConstructedGeneric(
                            RuntimeTypeIdentity.Named(reference.generic),
                            (reference.arguments ?? new List<UdonApiTypeRef>()).Select(argument => Identity(argument, methodParameters)).ToArray());
                        var ordinal = reference.methodParameter ?? reference.typeParameter ?? -1;
                        var parameters = reference.methodParameter.HasValue ? methodParameters : type.genericParameters;
                        if (parameters == null || ordinal < 0 || ordinal >= parameters.Count)
                            throw new InvalidDataException("Invalid catalog generic parameter reference.");
                        return RuntimeTypeIdentity.Named(parameters[ordinal].name);
                    }
                }
            }
            catalog.unmodeledUdonSignatures = (catalog.unmodeledUdonSignatures ?? new List<string>())
                .Where(signature => !recovered.Contains(signature)).ToList();
            return recovered.ToArray();

            void Move<T>(List<T> source, ref List<T> destination, Func<T, string> format, Action<T, string> expose)
            {
                if (source == null) return;
                foreach (var item in source.ToArray())
                {
                    var signature = format(item);
                    if (!unmatched.Contains(signature)) continue;
                    expose(item, signature);
                    (destination ??= new List<T>()).Add(item);
                    source.Remove(item);
                    recovered.Add(signature);
                }
            }
        }
    }
}
