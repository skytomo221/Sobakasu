using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Text;
using Skytomo221.Sobakasu.Tools.StandardLibraryGenerator;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public sealed class UdonBindingGeneratorFixture
    {
        public sealed class NestedValue
        {
        }

        public int Number;
        public readonly int ReadOnlyNumber;
        public int Count { get; set; }
        public static string Label => "fixture";
        public int this[int index] => index;

        public event Action Changed;

        public UdonBindingGeneratorFixture()
        {
        }

        public UdonBindingGeneratorFixture(int value)
        {
            Number = value;
        }

        public static UdonBindingGeneratorFixture Find(string name)
        {
            return name == null ? null : new UdonBindingGeneratorFixture();
        }

        public static UdonBindingGeneratorFixture Find(int id)
        {
            return id < 0 ? null : new UdonBindingGeneratorFixture();
        }

        public void SetActive(bool active)
        {
            if (active)
                Changed?.Invoke();
        }

        public int Mix(int value)
        {
            return value;
        }

        public float Mix(float value)
        {
            return value;
        }

        public void Hidden()
        {
        }

        public void RefValue(ref int value)
        {
            value++;
        }

        public bool RefOut(ref int value, out string text)
        {
            value++;
            text = value.ToString();
            return true;
        }

        public void OutReference(out UdonBindingGeneratorFixture value)
        {
            value = new UdonBindingGeneratorFixture();
        }

        public void OutNumber(out int value)
        {
            value = 1;
        }

        public void ArrayValue(string[] values)
        {
        }

        public void Nested(NestedValue value)
        {
        }

        public T Generic<T>(T value)
        {
            return value;
        }

        public T[] GenericArray<T>()
        {
            return Array.Empty<T>();
        }

        public void GenericList<T>(List<T> values)
        {
        }
    }

    public class UdonApiInheritedParentFixture
    {
        public event Action Changed;

        public void Foo()
        {
        }

        protected void RaiseChanged()
        {
            Changed?.Invoke();
        }
    }

    public sealed class UdonApiInheritedChildAFixture : UdonApiInheritedParentFixture
    {
    }

    public sealed class UdonApiInheritedChildBFixture : UdonApiInheritedParentFixture
    {
    }

    public sealed class UdonApiGenericCoverageFixture
    {
        public T ExposedGeneric<T>(T value)
        {
            return value;
        }

        public T UnexposedGeneric<T>(T value)
        {
            return value;
        }
    }

    public sealed class UdonApiNormalConstructorFixture
    {
        public UdonApiNormalConstructorFixture(int value)
        {
        }
    }

    public sealed class UdonApiRefConstructorFixture
    {
        public UdonApiRefConstructorFixture(ref int value)
        {
            value++;
        }
    }

    public sealed class UdonApiOutConstructorFixture
    {
        public UdonApiOutConstructorFixture(out string name)
        {
            name = "fixture";
        }
    }

    public sealed class UdonApiMixedConstructorFixture
    {
        public UdonApiMixedConstructorFixture(
            ref int value,
            out string name,
            ref float weight)
        {
            value++;
            name = value.ToString();
            weight += 1.0f;
        }
    }

    public struct UdonApiStructFixture
    {
        public int Value;
    }

    public struct UdonApiOperatorFixture
    {
        public float Value;

        public static UdonApiOperatorFixture operator *(
            float lhs,
            UdonApiOperatorFixture rhs) => rhs;

        public static UdonApiOperatorFixture operator -(
            UdonApiOperatorFixture value) => value;

        public static UdonApiOperatorFixture operator ~(
            UdonApiOperatorFixture value) => value;

        public static implicit operator float(UdonApiOperatorFixture value) =>
            value.Value;

        public static explicit operator UdonApiOperatorFixture(float value) =>
            new() { Value = value };

        public static UdonApiOperatorFixture operator ++(
            UdonApiOperatorFixture value) => value;

        public static UdonApiOperatorFixture operator --(
            UdonApiOperatorFixture value) => value;
    }

    public enum UdonApiEnumFixture
    {
        First = 10,
        Alias = 10,
        Second = 20
    }

    public class UdonApiNestedOuterFixture
    {
        public struct NestedValue
        {
            public int Value;
        }

        public enum NestedEnum
        {
            A,
            B
        }
    }

    public class UdonApiNestedCollisionA
    {
        public struct Value { public int Number; }
    }

    public class UdonApiNestedCollisionB
    {
        public struct Value { public int Number; }
    }

    public static class UdonApiStaticFixture
    {
        public static bool IsVisible { get; set; }

        public static int Abs(int value) => Math.Abs(value);
        public static float Abs(float value) => Math.Abs(value);
        public static bool IsReady() => true;
        public static bool isActiveAndEnabled() => true;
        public static int IsCount() => 1;
    }

    public static class UdonApiStaticFixture2
    {
        public static double Abs(double value) => Math.Abs(value);
    }

    public static class UdonApiStaticCollisionFixture
    {
        public static int Abs(int value) => Math.Abs(value);
    }

    namespace PolicyFixtures
    {
        public static class NamespaceFixture
        {
            public static int Value() => 1;
        }

        namespace Deep
        {
            public static class DeepNamespaceFixture
            {
                public static int DeepValue() => 2;
            }
        }
    }

}
