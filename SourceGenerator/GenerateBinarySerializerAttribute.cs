using System;

namespace BinarySerializerGenerator
{
    /// <summary>
    /// Marks a class for binary serializer generation.
    /// When applied to a class, the source generator will create a partial class
    /// with a SerializeToBinary method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class GenerateBinarySerializerAttribute : Attribute
    {
        public GenerateBinarySerializerAttribute()
        {
        }
    }
}