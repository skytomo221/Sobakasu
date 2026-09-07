using System;
using System.Globalization;
using System.IO;
using System.Text;
using Skytomo221.Sobakasu.Compiler.Binder;

namespace Skytomo221.Sobakasu.Compiler
{
    internal static class HeapPatchValueSerializer
    {
        public static string SerializeRuntimeValue(object value, TypeKind type, string runtimeTypeName = null)
        {
            if (value == null)
                throw new InvalidOperationException($"Heap patch runtime value for '{type}' must not be null.");

            if (type == TypeKind.Array)
            {
                if (value is not Array array)
                    throw new InvalidOperationException($"Heap patch runtime value '{value}' is not an array.");
                if (!string.IsNullOrEmpty(runtimeTypeName))
                {
                    var expectedType = SobakasuTypeMapper.ToSystemType(type, runtimeTypeName);
                    if (!expectedType.IsInstanceOfType(array))
                        throw new InvalidOperationException($"Heap patch array value '{array.GetType()}' does not match '{expectedType}'.");
                }

                using var stream = new MemoryStream();
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
                {
                    writer.Write((byte)1);
                    WriteValue(writer, array);
                }
                return Convert.ToBase64String(stream.ToArray());
            }

            if (type == TypeKind.Named && !string.IsNullOrEmpty(runtimeTypeName))
            {
                if (string.Equals(runtimeTypeName, typeof(Type).FullName, StringComparison.Ordinal) && value is Type runtimeTypeValue)
                    return runtimeTypeValue.AssemblyQualifiedName;
                var runtimeType = SobakasuTypeMapper.ResolveRuntimeType(runtimeTypeName);
                if (runtimeType.IsEnum && runtimeType.IsInstanceOfType(value))
                    return value.ToString();
            }

            return type switch
            {
                TypeKind.Bool when value is bool boolValue => boolValue ? "true" : "false",
                TypeKind.Char when value is char charValue => ((int)charValue).ToString(CultureInfo.InvariantCulture),
                TypeKind.I8 when value is sbyte int8Value => int8Value.ToString(CultureInfo.InvariantCulture),
                TypeKind.U8 when value is byte uint8Value => uint8Value.ToString(CultureInfo.InvariantCulture),
                TypeKind.I16 when value is short int16Value => int16Value.ToString(CultureInfo.InvariantCulture),
                TypeKind.U16 when value is ushort uint16Value => uint16Value.ToString(CultureInfo.InvariantCulture),
                TypeKind.I32 when value is int int32Value => int32Value.ToString(CultureInfo.InvariantCulture),
                TypeKind.U32 when value is uint uint32Value => uint32Value.ToString(CultureInfo.InvariantCulture),
                TypeKind.I64 when value is long int64Value => int64Value.ToString(CultureInfo.InvariantCulture),
                TypeKind.U64 when value is ulong uint64Value => uint64Value.ToString(CultureInfo.InvariantCulture),
                TypeKind.F32 when value is float floatValue => floatValue.ToString("R", CultureInfo.InvariantCulture),
                TypeKind.F64 when value is double doubleValue => doubleValue.ToString("R", CultureInfo.InvariantCulture),
                TypeKind.String when value is string stringValue => stringValue,
                _ => throw new InvalidOperationException($"Heap patch runtime value '{value}' does not match Sobakasu type '{type}'.")
            };
        }

        public static object DeserializeRuntimeValue(string value, TypeKind type, string runtimeTypeName = null)
        {
            if (type == TypeKind.Named && string.Equals(runtimeTypeName, typeof(Type).FullName, StringComparison.Ordinal))
                return SobakasuTypeMapper.ResolveRuntimeType(value);
            if (type == TypeKind.Array)
            {
                var expectedType = SobakasuTypeMapper.ToSystemType(type, runtimeTypeName);
                using var stream = new MemoryStream(Convert.FromBase64String(value));
                using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
                var version = reader.ReadByte();
                if (version != 1)
                    throw new InvalidDataException($"Unsupported Sobakasu array heap patch format version '{version}'.");

                var result = ReadValue(reader);
                if (result == null || !expectedType.IsInstanceOfType(result))
                    throw new InvalidDataException($"Stored heap patch value does not match '{expectedType}'.");
                if (stream.Position != stream.Length)
                    throw new InvalidDataException("Stored array heap patch has trailing data.");
                return result;
            }

            if (type == TypeKind.Named && !string.IsNullOrEmpty(runtimeTypeName))
            {
                var runtimeType = SobakasuTypeMapper.ResolveRuntimeType(runtimeTypeName);
                if (runtimeType.IsEnum)
                    return Enum.Parse(runtimeType, value, ignoreCase: false);
            }

            return type switch
            {
                TypeKind.Bool => value == "true",
                TypeKind.Char => (char)int.Parse(value, CultureInfo.InvariantCulture),
                TypeKind.I8 => sbyte.Parse(value, CultureInfo.InvariantCulture),
                TypeKind.U8 => byte.Parse(value, CultureInfo.InvariantCulture),
                TypeKind.I16 => short.Parse(value, CultureInfo.InvariantCulture),
                TypeKind.U16 => ushort.Parse(value, CultureInfo.InvariantCulture),
                TypeKind.I32 => int.Parse(value, CultureInfo.InvariantCulture),
                TypeKind.U32 => uint.Parse(value, CultureInfo.InvariantCulture),
                TypeKind.I64 => long.Parse(value, CultureInfo.InvariantCulture),
                TypeKind.U64 => ulong.Parse(value, CultureInfo.InvariantCulture),
                TypeKind.F32 => float.Parse(value, CultureInfo.InvariantCulture),
                TypeKind.F64 => double.Parse(value, CultureInfo.InvariantCulture),
                TypeKind.String => value ?? string.Empty,
                _ => throw new NotSupportedException($"Sobakasu heap patch type '{type}' is not supported.")
            };
        }

        private static void WriteValue(BinaryWriter writer, object value)
        {
            writer.Write(value != null);
            if (value == null) return;

            var runtimeType = value.GetType();
            writer.Write(runtimeType.AssemblyQualifiedName ?? runtimeType.FullName);
            if (value is Array array)
            {
                if (array.Rank != 1)
                    throw new NotSupportedException("Only one-dimensional CLR arrays can be patched.");
                writer.Write(array.Length);
                foreach (var element in array) WriteValue(writer, element);
                return;
            }

            switch (Type.GetTypeCode(runtimeType))
            {
                case TypeCode.Boolean: writer.Write((bool)value); return;
                case TypeCode.Char: writer.Write((char)value); return;
                case TypeCode.SByte: writer.Write((sbyte)value); return;
                case TypeCode.Byte: writer.Write((byte)value); return;
                case TypeCode.Int16: writer.Write((short)value); return;
                case TypeCode.UInt16: writer.Write((ushort)value); return;
                case TypeCode.Int32: writer.Write((int)value); return;
                case TypeCode.UInt32: writer.Write((uint)value); return;
                case TypeCode.Int64: writer.Write((long)value); return;
                case TypeCode.UInt64: writer.Write((ulong)value); return;
                case TypeCode.Single: writer.Write((float)value); return;
                case TypeCode.Double: writer.Write((double)value); return;
                case TypeCode.String: writer.Write((string)value); return;
                default: throw new NotSupportedException($"Runtime value type '{runtimeType}' cannot be stored in an array heap patch.");
            }
        }

        private static object ReadValue(BinaryReader reader)
        {
            if (!reader.ReadBoolean()) return null;

            var runtimeType = SobakasuTypeMapper.ResolveRuntimeType(reader.ReadString());
            if (runtimeType.IsArray)
            {
                if (runtimeType.GetArrayRank() != 1)
                    throw new NotSupportedException("Only one-dimensional CLR arrays can be patched.");
                var length = reader.ReadInt32();
                if (length < 0)
                    throw new InvalidDataException("Stored array length must not be negative.");
                var array = Array.CreateInstance(runtimeType.GetElementType(), length);
                for (var index = 0; index < length; index++) array.SetValue(ReadValue(reader), index);
                return array;
            }

            return Type.GetTypeCode(runtimeType) switch
            {
                TypeCode.Boolean => reader.ReadBoolean(),
                TypeCode.Char => reader.ReadChar(),
                TypeCode.SByte => reader.ReadSByte(),
                TypeCode.Byte => reader.ReadByte(),
                TypeCode.Int16 => reader.ReadInt16(),
                TypeCode.UInt16 => reader.ReadUInt16(),
                TypeCode.Int32 => reader.ReadInt32(),
                TypeCode.UInt32 => reader.ReadUInt32(),
                TypeCode.Int64 => reader.ReadInt64(),
                TypeCode.UInt64 => reader.ReadUInt64(),
                TypeCode.Single => reader.ReadSingle(),
                TypeCode.Double => reader.ReadDouble(),
                TypeCode.String => reader.ReadString(),
                _ => throw new NotSupportedException($"Runtime value type '{runtimeType}' cannot be restored from an array heap patch.")
            };
        }

        public static string GetPlaceholderValue(TypeKind type)
        {
            return type switch
            {
                TypeKind.String => string.Empty,
                // UAssembly requires these slots to start as a reference placeholder
                // and Sobakasu writes the real typed value during post-assemble patching.
                TypeKind.Bool => "null",
                TypeKind.Char => "null",
                TypeKind.I8 => "0",
                TypeKind.U8 => "0",
                TypeKind.I16 => "0",
                TypeKind.U16 => "0",
                TypeKind.I32 => "0",
                TypeKind.U32 => "0",
                TypeKind.I64 => "null",
                TypeKind.U64 => "null",
                TypeKind.F32 => "0",
                TypeKind.F64 => "0",
                _ => throw new NotSupportedException($"Sobakasu heap patch type '{type}' is not supported.")
            };
        }
    }
}
