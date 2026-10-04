using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;

using Skytomo221.Sobakasu.Compiler.Binder;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public sealed class RuntimeValueSerializerIntegrationTests
    {
        private sealed class SystemTypeFixture
        {
        }

        [Test]
        public void HeapPatchSerializer_RoundTripsNestedArraysNullsAndBoxingTypes()
        {
            object[] value = { 1, "text", true, null, new[] { 2, 3 } };
            var serialized = HeapPatchValueSerializer.SerializeRuntimeValue(
                value, TypeKind.Array, "System.Object[]");
            var restored = (object[])HeapPatchValueSerializer.DeserializeRuntimeValue(
                serialized, TypeKind.Array, "System.Object[]");

            Assert.That(restored[0], Is.TypeOf<int>());
            Assert.That(restored[0], Is.EqualTo(1));
            Assert.That(restored[1], Is.TypeOf<string>());
            Assert.That(restored[2], Is.TypeOf<bool>());
            Assert.That(restored[3], Is.Null);
            Assert.That(restored[4], Is.EqualTo(new[] { 2, 3 }));
        }

        [Test]
        public void HeapPatchValueSerializer_RoundTripsSystemTypeIdentity()
        {
            var serialized = HeapPatchValueSerializer.SerializeRuntimeValue(
                typeof(SystemTypeFixture), TypeKind.Named, typeof(System.Type).FullName);
            var restored = HeapPatchValueSerializer.DeserializeRuntimeValue(
                serialized, TypeKind.Named, typeof(System.Type).FullName);

            Assert.That(restored, Is.EqualTo(typeof(SystemTypeFixture)));
            Assert.That(serialized, Does.Contain(typeof(SystemTypeFixture).FullName));
        }
    }
}
