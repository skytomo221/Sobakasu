using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Desugar;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.IrLowerer;
using Skytomo221.Sobakasu.Compiler.Optimizer;
using Skytomo221.Sobakasu.Compiler.Modules;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Text;
using Skytomo221.Sobakasu.Compiler.UasmAssembler;
using DiagnosticItem = Skytomo221.Sobakasu.Compiler.Diagnostic.Diagnostic;

namespace Skytomo221.Sobakasu.Compiler
{
  public enum HeapPatchKind
  {
    Constant,
    GlobalInitializer,
    FieldInitializer,
    ArrayInitializer,
    UserDefinedValue
  }

  public sealed class HeapPatchEntry
  {
    public string SymbolName { get; }
    public TypeKind SymbolType { get; }
    public object RuntimeValue { get; }
    public string RuntimeTypeName { get; }
    public HeapPatchKind Kind { get; }
    public TextSpan? SourceSpan { get; }

    public HeapPatchEntry(
        string symbolName,
        TypeKind symbolType,
        object runtimeValue,
        HeapPatchKind kind,
        TextSpan? sourceSpan = null,
        string runtimeTypeName = null)
    {
      SymbolName = symbolName ?? throw new ArgumentNullException(nameof(symbolName));
      SymbolType = symbolType;
      RuntimeValue = runtimeValue;
      RuntimeTypeName = runtimeTypeName;
      Kind = kind;
      SourceSpan = sourceSpan;
    }
  }

  public static class SobakasuTypeMapper
  {
    public static Type ToSystemType(TypeKind type)
    {
      return ToSystemType(type, null);
    }

    public static Type ToSystemType(TypeKind type, string runtimeTypeName)
    {
      if (type == TypeKind.Array)
      {
        if (string.IsNullOrEmpty(runtimeTypeName) ||
            !runtimeTypeName.EndsWith("[]", StringComparison.Ordinal))
        {
          throw new NotSupportedException(
              "Sobakasu array heap patches require their runtime array type name.");
        }

        var elementTypeName = runtimeTypeName.Substring(
            0,
            runtimeTypeName.Length - 2);
        return ResolveRuntimeType(elementTypeName).MakeArrayType();
      }

      return type switch
      {
        TypeKind.Bool => typeof(bool),
        TypeKind.Char => typeof(char),
        TypeKind.I8 => typeof(sbyte),
        TypeKind.U8 => typeof(byte),
        TypeKind.I16 => typeof(short),
        TypeKind.U16 => typeof(ushort),
        TypeKind.I32 => typeof(int),
        TypeKind.U32 => typeof(uint),
        TypeKind.I64 => typeof(long),
        TypeKind.U64 => typeof(ulong),
        TypeKind.F32 => typeof(float),
        TypeKind.F64 => typeof(double),
        TypeKind.String => typeof(string),
        TypeKind.Named when !string.IsNullOrEmpty(runtimeTypeName) =>
            ResolveRuntimeType(runtimeTypeName),
        _ => throw new NotSupportedException(
            $"Sobakasu heap patch type '{type}' is not supported.")
      };
    }

    internal static Type ResolveRuntimeType(string runtimeTypeName)
    {
      if (string.IsNullOrEmpty(runtimeTypeName))
        throw new ArgumentException("Runtime type name must not be empty.", nameof(runtimeTypeName));

      if (runtimeTypeName.EndsWith("[]", StringComparison.Ordinal))
      {
        return ResolveRuntimeType(
            runtimeTypeName.Substring(0, runtimeTypeName.Length - 2))
            .MakeArrayType();
      }

      var resolved = Type.GetType(runtimeTypeName, throwOnError: false);
      if (resolved != null)
        return resolved;

      foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
      {
        resolved = assembly.GetType(runtimeTypeName, throwOnError: false);
        if (resolved != null)
          return resolved;
      }

      throw new TypeLoadException(
          $"Runtime type '{runtimeTypeName}' could not be resolved.");
    }
  }

  public sealed class NetworkReceiveParameterMetadata
  {
    public string StorageName { get; }
    public TypeKind Type { get; }
    public string RuntimeTypeName { get; }

    public NetworkReceiveParameterMetadata(
        string storageName,
        TypeKind type,
        string runtimeTypeName)
    {
      StorageName = storageName ?? throw new ArgumentNullException(nameof(storageName));
      Type = type;
      RuntimeTypeName = runtimeTypeName;
    }
  }

  public sealed class NetworkReceiveMetadata
  {
    public string Name { get; }
    public IReadOnlyList<NetworkReceiveParameterMetadata> Parameters { get; }

    public NetworkReceiveMetadata(
        string name,
        IReadOnlyList<NetworkReceiveParameterMetadata> parameters)
    {
      Name = name ?? throw new ArgumentNullException(nameof(name));
      Parameters = parameters ??
          throw new ArgumentNullException(nameof(parameters));
    }
  }

  public enum ExternalBindingInvocationKind
  {
    Static,
    Instance
  }

  public enum ExternalBindingMemberKind
  {
    Method,
    Getter,
    Setter,
    Constructor,
    Operator
  }

  public enum ExternalBindingReturnMode
  {
    Raw,
    Maybe
  }

  public sealed class ExternalBindingMetadata
  {
    public string SobakasuSymbol { get; }
    public string SobakasuName { get; }
    public string DeclaringModule { get; }
    public IReadOnlyList<string> SobakasuParameterTypes { get; }
    public string SobakasuReturnType { get; }
    public string ExternalDeclaringType { get; }
    public string ExternalMemberName { get; }
    public IReadOnlyList<string> ExternalParameterTypes { get; }
    public string ExternalReturnType { get; }
    public string ResolvedExternalSignature { get; }
    public ExternalBindingInvocationKind InvocationKind { get; }
    public ExternalBindingMemberKind MemberKind { get; }
    public ExternalBindingReturnMode ReturnMode { get; }

    public ExternalBindingMetadata(
        string sobakasuSymbol,
        string sobakasuName,
        string declaringModule,
        IReadOnlyList<string> sobakasuParameterTypes,
        string sobakasuReturnType,
        string externalDeclaringType,
        string externalMemberName,
        IReadOnlyList<string> externalParameterTypes,
        string externalReturnType,
        string resolvedExternalSignature,
        ExternalBindingInvocationKind invocationKind,
        ExternalBindingMemberKind memberKind,
        ExternalBindingReturnMode returnMode)
    {
      SobakasuSymbol = sobakasuSymbol ??
          throw new ArgumentNullException(nameof(sobakasuSymbol));
      SobakasuName = sobakasuName ??
          throw new ArgumentNullException(nameof(sobakasuName));
      DeclaringModule = declaringModule ?? string.Empty;
      SobakasuParameterTypes = sobakasuParameterTypes ?? Array.Empty<string>();
      SobakasuReturnType = sobakasuReturnType ??
          throw new ArgumentNullException(nameof(sobakasuReturnType));
      ExternalDeclaringType = externalDeclaringType ??
          throw new ArgumentNullException(nameof(externalDeclaringType));
      ExternalMemberName = externalMemberName ??
          throw new ArgumentNullException(nameof(externalMemberName));
      ExternalParameterTypes = externalParameterTypes ?? Array.Empty<string>();
      ExternalReturnType = externalReturnType ??
          throw new ArgumentNullException(nameof(externalReturnType));
      ResolvedExternalSignature = resolvedExternalSignature ??
          throw new ArgumentNullException(nameof(resolvedExternalSignature));
      InvocationKind = invocationKind;
      MemberKind = memberKind;
      ReturnMode = returnMode;
    }
  }

  internal static class HeapPatchValueSerializer
  {
    public static string SerializeRuntimeValue(
        object value,
        TypeKind type,
        string runtimeTypeName = null)
    {
      if (value == null)
      {
        throw new InvalidOperationException(
            $"Heap patch runtime value for '{type}' must not be null.");
      }

      if (type == TypeKind.Array)
      {
        if (value is not Array array)
        {
          throw new InvalidOperationException(
              $"Heap patch runtime value '{value}' is not an array.");
        }

        if (!string.IsNullOrEmpty(runtimeTypeName))
        {
          var expectedType = SobakasuTypeMapper.ToSystemType(type, runtimeTypeName);
          if (!expectedType.IsInstanceOfType(array))
          {
            throw new InvalidOperationException(
                $"Heap patch array value '{array.GetType()}' does not match '{expectedType}'.");
          }
        }

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
          writer.Write((byte)1);
          WriteValue(writer, array);
        }

        return Convert.ToBase64String(stream.ToArray());
      }

      if (type == TypeKind.Named &&
          !string.IsNullOrEmpty(runtimeTypeName))
      {
        var runtimeType = SobakasuTypeMapper.ResolveRuntimeType(runtimeTypeName);
        if (runtimeType.IsEnum && runtimeType.IsInstanceOfType(value))
          return value.ToString();
      }

      return type switch
      {
        TypeKind.Bool when value is bool boolValue =>
            boolValue ? "true" : "false",
        TypeKind.Char when value is char charValue =>
            ((int)charValue).ToString(CultureInfo.InvariantCulture),
        TypeKind.I8 when value is sbyte int8Value =>
            int8Value.ToString(CultureInfo.InvariantCulture),
        TypeKind.U8 when value is byte uint8Value =>
            uint8Value.ToString(CultureInfo.InvariantCulture),
        TypeKind.I16 when value is short int16Value =>
            int16Value.ToString(CultureInfo.InvariantCulture),
        TypeKind.U16 when value is ushort uint16Value =>
            uint16Value.ToString(CultureInfo.InvariantCulture),
        TypeKind.I32 when value is int int32Value =>
            int32Value.ToString(CultureInfo.InvariantCulture),
        TypeKind.U32 when value is uint uint32Value =>
            uint32Value.ToString(CultureInfo.InvariantCulture),
        TypeKind.I64 when value is long int64Value =>
            int64Value.ToString(CultureInfo.InvariantCulture),
        TypeKind.U64 when value is ulong uint64Value =>
            uint64Value.ToString(CultureInfo.InvariantCulture),
        TypeKind.F32 when value is float floatValue =>
            floatValue.ToString("R", CultureInfo.InvariantCulture),
        TypeKind.F64 when value is double doubleValue =>
            doubleValue.ToString("R", CultureInfo.InvariantCulture),
        TypeKind.String when value is string stringValue =>
            stringValue,
        _ => throw new InvalidOperationException(
            $"Heap patch runtime value '{value}' does not match Sobakasu type '{type}'.")
      };
    }

    public static object DeserializeRuntimeValue(
        string value,
        TypeKind type,
        string runtimeTypeName = null)
    {
      if (type == TypeKind.Array)
      {
        var expectedType = SobakasuTypeMapper.ToSystemType(type, runtimeTypeName);
        using var stream = new MemoryStream(Convert.FromBase64String(value));
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
        var version = reader.ReadByte();
        if (version != 1)
        {
          throw new InvalidDataException(
              $"Unsupported Sobakasu array heap patch format version '{version}'.");
        }

        var result = ReadValue(reader);
        if (result == null || !expectedType.IsInstanceOfType(result))
        {
          throw new InvalidDataException(
              $"Stored heap patch value does not match '{expectedType}'.");
        }

        if (stream.Position != stream.Length)
          throw new InvalidDataException("Stored array heap patch has trailing data.");

        return result;
      }

      if (type == TypeKind.Named &&
          !string.IsNullOrEmpty(runtimeTypeName))
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
        _ => throw new NotSupportedException(
            $"Sobakasu heap patch type '{type}' is not supported.")
      };
    }

    private static void WriteValue(BinaryWriter writer, object value)
    {
      writer.Write(value != null);
      if (value == null)
        return;

      var runtimeType = value.GetType();
      writer.Write(runtimeType.AssemblyQualifiedName ?? runtimeType.FullName);
      if (value is Array array)
      {
        if (array.Rank != 1)
          throw new NotSupportedException("Only one-dimensional CLR arrays can be patched.");

        writer.Write(array.Length);
        foreach (var element in array)
          WriteValue(writer, element);
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
        default:
          throw new NotSupportedException(
              $"Runtime value type '{runtimeType}' cannot be stored in an array heap patch.");
      }
    }

    private static object ReadValue(BinaryReader reader)
    {
      if (!reader.ReadBoolean())
        return null;

      var runtimeType = SobakasuTypeMapper.ResolveRuntimeType(reader.ReadString());
      if (runtimeType.IsArray)
      {
        if (runtimeType.GetArrayRank() != 1)
          throw new NotSupportedException("Only one-dimensional CLR arrays can be patched.");

        var length = reader.ReadInt32();
        if (length < 0)
          throw new InvalidDataException("Stored array length must not be negative.");

        var array = Array.CreateInstance(runtimeType.GetElementType(), length);
        for (var index = 0; index < length; index++)
          array.SetValue(ReadValue(reader), index);
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
        _ => throw new NotSupportedException(
            $"Runtime value type '{runtimeType}' cannot be restored from an array heap patch.")
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
        _ => throw new NotSupportedException(
            $"Sobakasu heap patch type '{type}' is not supported.")
      };
    }
  }

  public static class SobakasuCompiler
  {
    public readonly struct CompileResult
    {
      public readonly bool Success;
      public readonly string Uasm;
      public readonly string ErrorText;
      public readonly IReadOnlyList<HeapPatchEntry> HeapPatches;
      public readonly IReadOnlyList<NetworkReceiveMetadata> NetworkReceivers;
      public readonly IReadOnlyList<ExternalBindingMetadata> ExternalBindings;
      public readonly IReadOnlyList<DiagnosticItem> Diagnostics;

      public CompileResult(
          bool success,
          string uasm,
          string errorText,
          IReadOnlyList<HeapPatchEntry> heapPatches,
          IReadOnlyList<NetworkReceiveMetadata> networkReceivers,
          IReadOnlyList<ExternalBindingMetadata> externalBindings,
          IReadOnlyList<DiagnosticItem> diagnostics)
      {
        Success = success;
        Uasm = uasm;
        ErrorText = errorText;
        HeapPatches = heapPatches ?? Array.Empty<HeapPatchEntry>();
        NetworkReceivers = networkReceivers ?? Array.Empty<NetworkReceiveMetadata>();
        ExternalBindings = externalBindings ?? Array.Empty<ExternalBindingMetadata>();
        Diagnostics = diagnostics ?? Array.Empty<DiagnosticItem>();
      }

      public CompileResult(
          bool success,
          string uasm,
          string errorText,
          IReadOnlyList<HeapPatchEntry> heapPatches,
          IReadOnlyList<NetworkReceiveMetadata> networkReceivers,
          IReadOnlyList<DiagnosticItem> diagnostics)
          : this(
              success,
              uasm,
              errorText,
              heapPatches,
              networkReceivers,
              Array.Empty<ExternalBindingMetadata>(),
              diagnostics)
      {
      }

      public CompileResult(
          bool success,
          string uasm,
          string errorText,
          IReadOnlyList<HeapPatchEntry> heapPatches,
          IReadOnlyList<DiagnosticItem> diagnostics)
          : this(
              success,
              uasm,
              errorText,
              heapPatches,
              Array.Empty<NetworkReceiveMetadata>(),
              Array.Empty<ExternalBindingMetadata>(),
              diagnostics)
      {
      }

      public static CompileResult Ok(
          string uasm,
          IReadOnlyList<HeapPatchEntry> heapPatches,
          IReadOnlyList<NetworkReceiveMetadata> networkReceivers,
          IReadOnlyList<ExternalBindingMetadata> externalBindings,
          IReadOnlyList<DiagnosticItem> diagnostics)
      {
        return new CompileResult(
            true,
            uasm,
            "",
            heapPatches ?? Array.Empty<HeapPatchEntry>(),
            networkReceivers ?? Array.Empty<NetworkReceiveMetadata>(),
            externalBindings ?? Array.Empty<ExternalBindingMetadata>(),
            diagnostics ?? Array.Empty<DiagnosticItem>());
      }

      public static CompileResult Ok(
          string uasm,
          IReadOnlyList<HeapPatchEntry> heapPatches,
          IReadOnlyList<NetworkReceiveMetadata> networkReceivers,
          IReadOnlyList<DiagnosticItem> diagnostics)
      {
        return Ok(
            uasm,
            heapPatches,
            networkReceivers,
            Array.Empty<ExternalBindingMetadata>(),
            diagnostics);
      }

      public static CompileResult Ok(
          string uasm,
          IReadOnlyList<HeapPatchEntry> heapPatches,
          IReadOnlyList<DiagnosticItem> diagnostics)
      {
        return Ok(
            uasm,
            heapPatches,
            Array.Empty<NetworkReceiveMetadata>(),
            diagnostics);
      }

      public static CompileResult Fail(
          string errorText,
          IReadOnlyList<DiagnosticItem> diagnostics)
      {
        return new CompileResult(
            false,
            "",
            errorText,
            Array.Empty<HeapPatchEntry>(),
            Array.Empty<NetworkReceiveMetadata>(),
            Array.Empty<ExternalBindingMetadata>(),
            diagnostics ?? Array.Empty<DiagnosticItem>());
      }
    }

    public static CompileResult CompileToUasm(string sourceText)
    {
      return CompileToUasm(sourceText, null);
    }

    public static CompileResult CompileToUasm(
        string sourceText,
        string standardLibraryRoot)
    {
      var resolver = new StandardLibraryResolver();
      var resolution = resolver.Resolve(
          sourceText ?? string.Empty,
          standardLibraryRoot);
      var graph = resolution.Graph;
      var text = graph.EntryModule.SourceText;

      var diagnostics = new DiagnosticBag();
      diagnostics.AddRange(resolution.Diagnostics);

      var binder = new SobakasuBinder();
      var boundProgram = binder.BindProgram(graph);
      diagnostics.AddRange(binder.Diagnostics);

      if (diagnostics.HasErrors)
      {
        var errorText = FormatDiagnostics(text, graph, diagnostics);
        return CompileResult.Fail(errorText, CopyDiagnostics(diagnostics));
      }

      var desugarer = new SobakasuDesugarer();
      var desugaredProgram = desugarer.Desugar(boundProgram);
      diagnostics.AddRange(desugarer.Diagnostics);

      if (diagnostics.HasErrors)
      {
        var errorText = FormatDiagnostics(text, graph, diagnostics);
        return CompileResult.Fail(errorText, CopyDiagnostics(diagnostics));
      }

      var irLowerer = new SobakasuIrLowerer();
      var irProgram = irLowerer.Lower(desugaredProgram);
      diagnostics.AddRange(irLowerer.Diagnostics);

      if (diagnostics.HasErrors)
      {
        var errorText = FormatDiagnostics(text, graph, diagnostics);
        return CompileResult.Fail(errorText, CopyDiagnostics(diagnostics));
      }

      var optimizer = new SobakasuOptimizer();
      var optimizedProgram = optimizer.Optimize(irProgram);

      var uasmAssembler = new SobakasuUasmAssembler();
      var uasm = uasmAssembler.Assemble(optimizedProgram);
      diagnostics.AddRange(uasmAssembler.Diagnostics);

      if (diagnostics.HasErrors)
      {
        var errorText = FormatDiagnostics(text, graph, diagnostics);
        return CompileResult.Fail(errorText, CopyDiagnostics(diagnostics));
      }

      return CompileResult.Ok(
          uasm,
          CopyHeapPatches(uasmAssembler.HeapPatches),
          CopyNetworkReceivers(boundProgram.NetworkReceivers),
          CopyExternalBindings(boundProgram.Functions),
          CopyDiagnostics(diagnostics));
    }

    private static string FormatDiagnostics(
        SourceText entrySourceText,
        StandardLibraryModuleGraph graph,
        DiagnosticBag diagnostics)
    {
      var builder = new StringBuilder();

      foreach (var diagnostic in diagnostics.Diagnostics)
      {
        var sourceText = entrySourceText;
        var sourcePath = diagnostic.SourcePath;
        if (!string.IsNullOrEmpty(sourcePath))
        {
          foreach (var module in graph.Modules)
          {
            if (string.Equals(
                    module.SourcePath,
                    sourcePath,
                    StringComparison.OrdinalIgnoreCase))
            {
              sourceText = module.SourceText;
              break;
            }
          }
        }

        var line = sourceText.GetLineFromPosition(diagnostic.Span.Start);
        var lineIndex = GetLineIndex(sourceText, line);
        var column = diagnostic.Span.Start - line.Start + 1;

        builder.AppendFormat(
            "{0}{1} {2} (line {3}, col {4}): {5}\n",
            string.IsNullOrEmpty(sourcePath) ? string.Empty : sourcePath + ": ",
            diagnostic.Severity,
            diagnostic.Code,
            lineIndex + 1,
            column,
            diagnostic.Message);

        if (!string.IsNullOrWhiteSpace(diagnostic.Hint))
          builder.AppendFormat("  hint: {0}\n", diagnostic.Hint);
      }

      return TrimTrailingLineBreaks(builder.ToString());
    }

    private static int GetLineIndex(SourceText sourceText, TextLine targetLine)
    {
      for (var index = 0; index < sourceText.Lines.Count; index++)
      {
        if (ReferenceEquals(sourceText.Lines[index], targetLine))
          return index;
      }

      return 0;
    }

    private static string TrimTrailingLineBreaks(string text)
    {
      if (string.IsNullOrEmpty(text))
        return string.Empty;

      var end = text.Length;
      while (end > 0 &&
             (text[end - 1] == '\r' || text[end - 1] == '\n'))
      {
        end--;
      }

      if (end == text.Length)
        return text;

      return text.Substring(0, end);
    }

    private static IReadOnlyList<DiagnosticItem> CopyDiagnostics(DiagnosticBag diagnostics)
    {
      if (diagnostics.Diagnostics.Count == 0)
        return Array.Empty<DiagnosticItem>();

      return new List<DiagnosticItem>(diagnostics.Diagnostics).ToArray();
    }

    private static IReadOnlyList<HeapPatchEntry> CopyHeapPatches(
        IReadOnlyList<HeapPatchEntry> heapPatches)
    {
      if (heapPatches == null || heapPatches.Count == 0)
        return Array.Empty<HeapPatchEntry>();

      return new List<HeapPatchEntry>(heapPatches).ToArray();
    }

    private static IReadOnlyList<NetworkReceiveMetadata> CopyNetworkReceivers(
        IReadOnlyList<BoundNetworkReceiveDeclaration> receivers)
    {
      if (receivers == null || receivers.Count == 0)
        return Array.Empty<NetworkReceiveMetadata>();

      var result = new List<NetworkReceiveMetadata>(receivers.Count);
      foreach (var receiver in receivers)
      {
        var parameters = new List<NetworkReceiveParameterMetadata>(
            receiver.ReceiveSymbol.PhysicalParameters.Count);
        foreach (var physical in receiver.ReceiveSymbol.PhysicalParameters)
        {
          parameters.Add(new NetworkReceiveParameterMetadata(
              physical.PhysicalParameter.UdonStorageName,
              physical.PhysicalParameter.Type.TypeKind,
              physical.PhysicalParameter.Type.RuntimeQualifiedName));
        }

        result.Add(new NetworkReceiveMetadata(
            receiver.ReceiveSymbol.ExportName,
            parameters));
      }

      return result.ToArray();
    }

    private static IReadOnlyList<ExternalBindingMetadata> CopyExternalBindings(
        IReadOnlyList<BoundFunctionDeclaration> functions)
    {
      if (functions == null || functions.Count == 0)
        return Array.Empty<ExternalBindingMetadata>();

      var result = new List<ExternalBindingMetadata>();
      foreach (var declaration in functions)
      {
        var function = declaration.FunctionSymbol;
        var binding = function.ExternalBinding;
        if (binding == null)
          continue;

        var sobakasuParameters = new string[function.Parameters.Count];
        for (var index = 0; index < function.Parameters.Count; index++)
          sobakasuParameters[index] = function.Parameters[index].Type.QualifiedName;

        var externalParameterOffset = binding.ExternalMethod.IsStatic ? 0 : 1;
        var externalParameterCount = Math.Max(
            0,
            binding.ExternalMethod.Parameters.Count - externalParameterOffset);
        var externalParameters = new string[externalParameterCount];
        for (var index = 0; index < externalParameterCount; index++)
        {
          externalParameters[index] = binding.ExternalMethod
              .Parameters[index + externalParameterOffset]
              .Type
              .RuntimeQualifiedName;
        }

        result.Add(new ExternalBindingMetadata(
            function.InternalIdentity,
            function.DisplayName,
            function.DeclaringModule,
            sobakasuParameters,
            function.ReturnType.QualifiedName,
            binding.ExternalDeclaringType.RuntimeQualifiedName,
            binding.ExternalMemberName,
            externalParameters,
            binding.ExternalMethod.ReturnType.RuntimeQualifiedName,
            binding.ResolvedExternalSignature,
            binding.InvocationKind == ExternalInvocationKind.Static
                ? ExternalBindingInvocationKind.Static
                : ExternalBindingInvocationKind.Instance,
            binding.MemberKind switch
            {
              ExternMemberKind.Getter => ExternalBindingMemberKind.Getter,
              ExternMemberKind.Setter => ExternalBindingMemberKind.Setter,
              ExternMemberKind.Constructor => ExternalBindingMemberKind.Constructor,
              ExternMemberKind.Operator => ExternalBindingMemberKind.Operator,
              _ => ExternalBindingMemberKind.Method
            },
            binding.ReturnBindingMode == ExternalReturnBindingMode.Maybe
                ? ExternalBindingReturnMode.Maybe
                : ExternalBindingReturnMode.Raw));
      }

      return result.ToArray();
    }
  }
}
