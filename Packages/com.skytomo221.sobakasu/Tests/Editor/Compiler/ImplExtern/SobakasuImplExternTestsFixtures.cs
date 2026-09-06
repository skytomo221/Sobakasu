using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Desugar;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Ir;
using Skytomo221.Sobakasu.Compiler.IrLowerer;
using Skytomo221.Sobakasu.Compiler.Lexer;
using Skytomo221.Sobakasu.Compiler.Optimizer;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using Skytomo221.Sobakasu.Compiler.UasmAssembler;
using UnityEditor;
using UnityEngine;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public static class SobakasuExternAbiFixture
    {
        public static void RefOnly(ref int value)
        {
            value++;
        }

        public static void OutOnly(out int value)
        {
            value = 42;
        }

        public static bool ReturnAndOut(out int value)
        {
            value = 42;
            return true;
        }

        public static int Mixed(
            int normal,
            ref int value,
            out string text,
            ref bool flag)
        {
            value += normal;
            text = value.ToString();
            flag = !flag;
            return normal;
        }
    }

    public sealed class SobakasuGenericExternFixture
    {
        public T Echo<T>(T value) where T : class
        {
            return value;
        }

        public T[] Values<T>()
        {
            return Array.Empty<T>();
        }

        public void Fill<T>(List<T> values)
        {
        }

        public void FillStrings(List<string> values)
        {
        }

        public T BaseConstraint<T>() where T : SobakasuGenericConstraintBase
        {
            return null;
        }

        public T InterfaceConstraint<T>() where T : ISobakasuGenericConstraint
        {
            return default;
        }

        public T StructConstraint<T>() where T : struct
        {
            return default;
        }

        public T ConstructorConstraint<T>() where T : new()
        {
            return new T();
        }
    }

    public class SobakasuGenericConstraintBase { }
    public interface ISobakasuGenericConstraint { }
    public sealed class SobakasuUnusedGenericExternFixture<T> { }

}
