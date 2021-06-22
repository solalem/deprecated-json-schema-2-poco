using System;
using System.CodeDom;
using System.Text.RegularExpressions;
using Cvent.SchemaToPoco.Core.Types;
using Cvent.SchemaToPoco.Core.Util;
using Cvent.SchemaToPoco.Core.Wrappers;
using Microsoft.CSharp;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;

namespace Cvent.SchemaToPoco.Core
{
    /// <summary>
    ///     Model for converting a JSchema to a CodeCompileUnit
    /// </summary>
    public class JsonSchemaToCodeUnit
    {
        /// <summary>
        ///     The namespace for the document.
        /// </summary>
        private readonly string _codeNamespace;

        /// <summary>
        ///     The JSchema, for easy access.
        /// </summary>
        private readonly JSchema _schemaDocument;

        /// <summary>
        ///     The extended JSchema wrapper.
        /// </summary>
        private readonly JsonSchemaWrapper _schemaWrapper;

        /// <summary>
        ///     The annotation type.
        /// </summary>
        private readonly AttributeType _attributeType;

        public JsonSchemaToCodeUnit(JsonSchemaWrapper schema, string requestedNamespace, AttributeType attributeType)
        {
            if (schema == null || schema.Schema == null)
            {
                throw new ArgumentNullException("schema");
            }

            _schemaWrapper = schema;
            _schemaDocument = schema.Schema;
            _codeNamespace = requestedNamespace;
            _attributeType = attributeType;
        }

        public JsonSchemaToCodeUnit(JsonSchemaWrapper schema)
            : this(schema, "", AttributeType.SystemDefault)
        {
        }

        /// <summary>
        ///     Main executor function.
        /// </summary>
        /// <returns>A CodeCompileUnit.</returns>
        public CodeCompileUnit Execute()
        {
            var codeCompileUnit = new CodeCompileUnit();
            // Set namespace
            var nsWrap = new NamespaceWrapper(new CodeNamespace(_codeNamespace));
            // Add imports for interfaces and dependencies
            nsWrap.AddImportsFromWrapper(_schemaWrapper);

            // Main class
            SchemaToClass(nsWrap, _schemaDocument);

            //// Definitions
            //foreach (var schema in _schemaDocument.Items)
            //{
            //    SchemaToClass(nsWrap, schema);
            //}

            codeCompileUnit.Namespaces.Add(nsWrap.Namespace);

            return codeCompileUnit;
        }

        private void SchemaToClass(NamespaceWrapper nsWrap, JSchema classSchema)
        {
            // Set class
            var className = classSchema.Title.SanitizeIdentifier();

            var codeClass = new CodeTypeDeclaration(className) { Attributes = MemberAttributes.Public };
            var clWrap = new ClassWrapper(codeClass);

            //// todo: add extended class
            //if (schema.ExtensionData != null && schema.ExtensionData.Count > 0)
            //{
            //    clWrap.AddInterface(JsonSchemaUtils.GetType(schema.ExtensionData[0], _codeNamespace).name);
            //}

            // Add comments and attributes for class
            if (!String.IsNullOrEmpty(classSchema.Description))
            {
                clWrap.AddComment(classSchema.Description);
            }
            // Add interfaces
            foreach (Type t in _schemaWrapper.Interfaces)
            {
                clWrap.AddInterface(t.Name);
            }

            // Sanitize inputs
            if (!String.IsNullOrEmpty(classSchema.Description))
            {
                classSchema.Description = Regex.Unescape(classSchema.Description);
            }

            // Add properties with getters/setters
            if (classSchema.Properties != null)
            {
                foreach (var i in classSchema.Properties)
                {
                    var propertySchema = i.Value;
                    // If it is an enum
                    var propertyName = i.Key.Capitalize();
                    if (propertySchema.Enum != null && propertySchema.Enum.Count > 0)
                    {
                        var enumField = new CodeTypeDeclaration(propertyName);
                        var enumWrap = new EnumWrapper(enumField);

                        // Add comment if not null
                        if (!String.IsNullOrEmpty(propertySchema.Description))
                        {
                            enumField.Comments.Add(new CodeCommentStatement(propertySchema.Description));
                        }

                        foreach (JToken j in propertySchema.Enum)
                        {
                            enumWrap.AddMember(j.ToString().SanitizeIdentifier());
                        }

                        // Add to namespace
                        nsWrap.AddClass(enumWrap.Property);
                    }
                    else
                    {
                        // WARNING: This assumes the namespace of the property is the same as the parent.
                        // This should not be a problem since imports are handled for all dependencies at the beginning.
                        Type type = JsonSchemaUtils.GetType(propertySchema, _codeNamespace);
                        bool isCustomType = type.Namespace != null && type.Namespace.Equals(_codeNamespace);
                        string strType = String.Empty;

                        // Add imports
                        nsWrap.AddImport(type.Namespace);
                        nsWrap.AddImportsFromSchema(propertySchema);

                        // Get the property type
                        if (isCustomType)
                        {
                            strType = JsonSchemaUtils.IsArray(propertySchema) ? string.Format("{0}<{1}>", JsonSchemaUtils.GetArrayType(propertySchema), type.Name) : type.Name;
                            if (propertySchema.Type == JSchemaType.Object)
                                SchemaToClass(nsWrap, propertySchema);
                            else if (propertySchema.Type == JSchemaType.Array && propertySchema.Items != null && propertySchema.Items.Count > 0)
                                SchemaToClass(nsWrap, propertySchema.Items[0]);
                        }
                        else if (JsonSchemaUtils.IsArray(propertySchema))
                        {
                            strType = string.Format("{0}<{1}>", JsonSchemaUtils.GetArrayType(propertySchema),
                                new CSharpCodeProvider().GetTypeOutput(new CodeTypeReference(type)));
                        }

                        //var field = new CodeMemberField
                        //{
                        //    Attributes = MemberAttributes.Private,
                        //    Name = "_" + i.Key,
                        //    Type =
                        //        TypeUtils.IsPrimitive(type) && !JsonSchemaUtils.IsArray(schema)
                        //            ? new CodeTypeReference(type)
                        //            : new CodeTypeReference(strType)
                        //};

                        //clWrap.Property.Members.Add(field);

                        var property = CreateProperty(propertyName, TypeUtils.IsPrimitive(type) && !JsonSchemaUtils.IsArray(propertySchema)
                                    ? new CodeTypeReference(type)
                                    : new CodeTypeReference(strType));

                        var prWrap = new PropertyWrapper(property);

                        // Add comments and attributes
                        prWrap.Populate(propertySchema, _attributeType);

                        // Add default, if any
                        if (propertySchema.Default != null)
                        {
                            clWrap.AddDefault(propertyName, property.Type, propertySchema.Default.ToString());
                        }

                        clWrap.Property.Members.Add(property);
                    }
                }
            }

            // Add class to namespace
            nsWrap.AddClass(clWrap.Property);   
        }

        /// <summary>
        ///     Creates a public auto property with getters and setters that wrap the
        ///     specified field.
        /// </summary>
        /// <param name="field">The field to get and set.</param>
        /// <param name="name">The name of the property.</param>
        /// <param name="type">The type of the property.</param>
        /// <returns>The property.</returns>
        public static CodeMemberField CreateProperty(string name, CodeTypeReference type)
        {
            var field = new CodeMemberField
            {
                Name = name,
                Type = type,
                Attributes = MemberAttributes.Public | MemberAttributes.Final,
                
            };

            field.Name += " { get; set; }";

            return field;
        }
    }
}
