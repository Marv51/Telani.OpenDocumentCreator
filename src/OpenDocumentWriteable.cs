using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Linq;

namespace OpenDocumentCreator;

/// <summary>
/// This is a parent element for all classes that will be serialized to create an open document file.
/// </summary>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public abstract class OpenDocumentWritable
{
    /// <summary>
    /// Name of the XML tag in the open document format
    /// </summary>
    internal abstract string OpenDocumentElementName { get; }

    /// <summary>
    /// the namespace of the tag
    /// </summary>
    internal virtual string? NamespaceName { get; }

    /// <summary>
    /// The namespace definitions needed for this element.
    /// </summary>
    internal virtual IEnumerable<XAttribute> NamespaceDefinitions { get; } = [];

    /// <summary>
    /// Get the text content
    /// </summary>
    /// <returns>text content</returns>
    public virtual string? TextContent() => null;

    private static void AddAttribute(XElement elem, OpenDocumentNameAttribute odnAttribute, string valueString)
    {
        var xmlname = odnAttribute.Name ?? string.Empty;
        var xmlnamespace = odnAttribute.ElemNamespace is null ? OpenDocument.Style : OpenDocument.FindNamespace(odnAttribute.ElemNamespace);
        elem.Add(new XAttribute(xmlnamespace + xmlname, valueString));
    }

    /*
     * This creates an expression to speed up the access of properties.
     *
     * The expression takes an OpenDocumentWritable parameter.
     * Casts the parameter as the type-parameter.
     * Gets the property prop on that.
     * Casts the property value to object
     * returns the object
     */
    private static Func<OpenDocumentWritable, object?> CompileFastAccessor(PropertyInfo prop, Type type)
    {
        // This is/was on the hot path during serialization. Previously we did the nice compiled expression tree below,
        // that was very fast. But with AOT compilation enabled, the pure reflection based approach is about the same speed:
        if (!RuntimeFeature.IsDynamicCodeSupported)
        {
            // Still worthwhile to cache, since the captured PropertyInfo is cached.
            return prop.GetValue;
        }
        else
        {
            var objParameterExpr = Expression.Parameter(typeof(OpenDocumentWritable), "instance");
            var instanceExpr = Expression.TypeAs(objParameterExpr, type);
            var propertyExpr = Expression.Property(instanceExpr, prop);
            var propertyObjExpr = Expression.Convert(propertyExpr, typeof(object));
            return Expression.Lambda<Func<OpenDocumentWritable, object?>>(propertyObjExpr, objParameterExpr).Compile();
        }
    }

    /// <summary>
    /// The cached list of properties to serialize for this element, built once per element kind.
    /// </summary>
    /// <returns>the property accessors and their names</returns>
    private IList<SerializationHelper> GetSerializers()
    {
        var cacheKey = NamespaceName + ":" + OpenDocumentElementName;

        if (OpenDocument.TypeCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var typeInfo = GetType().GetTypeInfo();
        var type = GetType();

        var serList = new List<SerializationHelper>();

        foreach (var prop in typeInfo.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (prop is not null
                && (prop.GetMethod?.IsPublic ?? false)
                && prop.Name != nameof(NamespaceName)
                && prop.Name != nameof(OpenDocumentElementName)
                && prop.GetCustomAttribute<OpenDocumentNameAttribute>() is not null)
            {
                Debug.Assert(
                    prop.DeclaringType?.BaseType == typeof(OpenDocumentWritable),
                    "Defining properties with OpenDocumentName attributes in sub-classes is currently not supported.");
                serList.Add(new SerializationHelper(CompileFastAccessor(prop, prop.DeclaringType ?? type))
                {
                    OpenDocumentNameAttribute = prop.GetCustomAttribute<OpenDocumentNameAttribute>(),
                });
            }
        }

        // Another element of the same kind may have won the race; use whichever list landed.
        return OpenDocument.TypeCache.GetOrAdd(cacheKey, serList);
    }

    private static void WriteAttribute(XmlWriter writer, OpenDocumentNameAttribute odnAttribute, string valueString)
    {
        var (prefix, uri) = OpenDocument.FindPrefixedNamespace(odnAttribute.ElemNamespace);
        writer.WriteAttributeString(prefix, odnAttribute.Name ?? string.Empty, uri, valueString);
    }

    /// <summary>
    /// Writes this element straight to <paramref name="writer"/>, without building an XElement for
    /// it first.
    /// </summary>
    /// <param name="writer">the writer to write to</param>
    /// <remarks>
    /// This is the streaming counterpart of <see cref="GetElement"/> and produces the same XML. It
    /// deliberately reuses the same cached property list, so it introduces no reflection of its own
    /// and the trimming and AOT behaviour of the library is unchanged.
    ///
    /// Attributes are written in a first pass and child elements in a second, because a writer has
    /// to be given every attribute before the first child, while the element model is free to take
    /// them in any order and sort them on the way out.
    /// </remarks>
    internal void WriteTo(XmlWriter writer) => WriteTo(writer, null, null);

    /// <summary>
    /// Writes this element, letting the caller contribute extra attributes and children.
    /// </summary>
    /// <param name="writer">the writer to write to</param>
    /// <param name="extraAttributes">called after the element's own attributes; may be null</param>
    /// <param name="extraChildren">called after the element's own children; may be null</param>
    /// <remarks>
    /// This exists for content that the element model assembles from outside the element itself:
    /// the rows a spreadsheet appends to a table after serializing the table, and the attributes a
    /// cell adds to its own element. A writer has to be given every attribute before the first
    /// child, which is why the two are separate callbacks rather than one.
    /// </remarks>
    internal virtual void WriteTo(XmlWriter writer, Action<XmlWriter>? extraAttributes, Action<XmlWriter>? extraChildren)
    {
        ArgumentNullException.ThrowIfNull(writer, nameof(writer));

        var (prefix, uri) = OpenDocument.FindPrefixedNamespace(NamespaceName);
        writer.WriteStartElement(prefix, OpenDocumentElementName, uri);

        foreach (var item in NamespaceDefinitions)
        {
            if (item.Name.Namespace == XNamespace.Xmlns)
            {
                writer.WriteAttributeString("xmlns", item.Name.LocalName, null, item.Value);
            }
            else
            {
                var (attrPrefix, attrUri) = OpenDocument.FindPrefixedNamespace(PrefixOf(item.Name.Namespace));
                writer.WriteAttributeString(attrPrefix, item.Name.LocalName, attrUri, item.Value);
            }
        }

        var serializers = GetSerializers();

        foreach (var prop in serializers)
        {
            object? value = prop.Getter(this);
            switch (value)
            {
                case Enum valueEnum:
                    WriteAttribute(writer, RequireName(prop), EnumToStringGenerator.EnumToString(valueEnum));
                    break;
                case string valueString:
                    WriteAttribute(writer, RequireName(prop), valueString);
                    break;
                case int valueInt:
                    WriteAttribute(writer, RequireName(prop), valueInt.ToString(CultureInfo.InvariantCulture));
                    break;
                case IEnumerable<OpenDocumentWritable>:
                case OpenDocumentWritable:
                case XElement:
                    break;
                default:
                    if (value is not null && prop.OpenDocumentNameAttribute is not null)
                    {
                        WriteAttribute(writer, prop.OpenDocumentNameAttribute, value.ToString() ?? string.Empty);
                    }
                    break;
            }
        }

        extraAttributes?.Invoke(writer);

        // Setting Value on an XElement replaces whatever children were added, so an element with
        // text content has no child elements. Mirror that here.
        var content = TextContent();
        if (content is not null)
        {
            writer.WriteString(content);
            extraChildren?.Invoke(writer);
            writer.WriteEndElement();
            return;
        }

        foreach (var prop in serializers)
        {
            switch (prop.Getter(this))
            {
                case IEnumerable<OpenDocumentWritable> all:
                    foreach (var item in all)
                    {
                        item.WriteTo(writer);
                    }
                    break;
                case OpenDocumentWritable valueWritable:
                    valueWritable.WriteTo(writer);
                    break;
                case XElement element:
                    element.WriteTo(writer);
                    break;
                default:
                    break;
            }
        }

        extraChildren?.Invoke(writer);

        writer.WriteEndElement();
    }

    private static string? PrefixOf(XNamespace theNamespace)
    {
        foreach (var (prefix, known) in OpenDocument.Namespaces)
        {
            if (known == theNamespace)
            {
                return prefix;
            }
        }
        return null;
    }

    private static OpenDocumentNameAttribute RequireName(SerializationHelper prop)
        => prop.OpenDocumentNameAttribute ?? throw new InvalidOperationException("OpenDocumentNameAttribute missing");

    /// <summary>
    /// Get element as XML element
    /// </summary>
    /// <returns>Xml Element for this class</returns>
    /// <exception cref="System.InvalidOperationException">If an attribute is missing.</exception>
    /// <remarks>
    /// Built by running <see cref="WriteTo(XmlWriter)"/> into a writer that appends to an XLinq
    /// tree, so there is one description of how an element serializes rather than two that have to
    /// be kept in step. They did drift: the streaming path once spelled the non finite numbers
    /// differently from this one.
    ///
    /// Saving no longer goes through here - it writes straight to the file - so this is for
    /// callers that want the tree: the unzipped debug output, and tests.
    /// </remarks>
    internal XElement GetElement()
    {
        var document = new XDocument();
        using (var writer = document.CreateWriter())
        {
            WriteTo(writer);
        }

        var root = document.Root ?? throw new InvalidOperationException("Serializing " + OpenDocumentElementName + " produced no element");
        RemoveRedundantNamespaceDeclarations(root);
        return root;
    }

    /// <summary>
    /// Drops namespace declarations that an ancestor already makes.
    /// </summary>
    /// <param name="root">the element to clean up, together with everything under it</param>
    /// <remarks>
    /// A writer has to declare a prefix before using it and cannot know that an element will later
    /// be nested inside one that already declares it, so writing into a tree leaves a declaration
    /// on every element. Serializing that produces the same XML with a great deal of noise in it.
    /// The ones an ancestor already makes are therefore removed; the outermost declaration of each
    /// prefix stays, which is what the element model produced before.
    /// </remarks>
    private static void RemoveRedundantNamespaceDeclarations(XElement root)
    {
        foreach (var element in root.Descendants().ToList())
        {
            foreach (var declaration in element.Attributes().Where(a => a.IsNamespaceDeclaration).ToList())
            {
                var boundAbove = element.Ancestors()
                    .SelectMany(a => a.Attributes())
                    .Any(a => a.IsNamespaceDeclaration && a.Name == declaration.Name && a.Value == declaration.Value);

                if (boundAbove)
                {
                    declaration.Remove();
                }
            }
        }
    }
}
