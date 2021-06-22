using System;
using System.Collections.Generic;
using Newtonsoft.Json.Schema;

namespace Cvent.SchemaToPoco.Core.Util
{
    //TODO
    public static class TypeUtils
    {
        /// <summary>
        ///     What primitive objects map to in C#.
        /// </summary>
        private static readonly Dictionary<JSchemaType, string> Primitives = new Dictionary<JSchemaType, string>
        {
            {JSchemaType.String, "System.String"},
            {JSchemaType.Number, "System.Single"},
            {JSchemaType.Integer, "System.Int32"},
            {JSchemaType.Boolean, "System.Boolean"},
            {JSchemaType.Object, "System.Object"}
        };

        /// <summary>
        ///     Check if a type is a primitive, or can be treated like one (ie. lowercased type).
        /// </summary>
        /// <param name="t">The type.</param>
        /// <returns>Whether or not it is a primitive type.</returns>
        public static bool IsPrimitive(Type t)
        {
            return t.IsPrimitive || t == typeof(Decimal) || t == typeof(String) || t == typeof(Object);
        }

        /// <summary>
        ///     Get the primitive name of a type if it exists.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The primitive type as a string, if it exists.</returns>
        public static string GetPrimitiveTypeAsString(JSchemaType? type)
        {
            if (type != null)
            {
                foreach (var prim in Primitives)
                {
                    // Return the first enum
                    if (type.Value.HasFlag(prim.Key))
                        return prim.Value;
                }
            }
            return "System.Object";
        }

    }
}
