using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
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
    /// Get element as XML element
    /// </summary>
    /// <returns>Xml Element for this class</returns>
    /// <exception cref="System.InvalidOperationException">If an attribute is missing.</exception>
    /// <remarks>
    /// This is implementation can be overridden in sub-classes, to provide a faster implementation for specific classes.
    ///
    /// This default implementation uses reflection to get all properties with OpenDocumentName attributes.
    /// </remarks>
    internal virtual XElement GetElement()
    {
        var element_namespace = NamespaceName is null ? OpenDocument.Style : OpenDocument.FindNamespace(NamespaceName);

        var elem = new XElement(element_namespace + OpenDocumentElementName);
        foreach (var item in NamespaceDefinitions)
        {
            elem.Add(item);
        }
        var cacheKey = NamespaceName + ":" + OpenDocumentElementName;

        if (!OpenDocument.TypeCache.TryGetValue(cacheKey, out var serializers))
        {
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
            // Another element of the same kind may have won the race; take whichever list ended
            // up in the cache so both callers use the same one.
            serializers = OpenDocument.TypeCache.GetOrAdd(cacheKey, serList);
        }
        foreach (var prop in serializers)
        {
            object? value = prop.Getter(this);

            switch (value)
            {
                case Enum valueEnum:
                    var enumStrValue = EnumToStringGenerator.EnumToString(valueEnum);
                    AddAttribute(elem, prop.OpenDocumentNameAttribute ?? throw new InvalidOperationException("OpenDocumentNameAttribute missing"), enumStrValue);
                    break;
                case string valueString:
                    AddAttribute(elem, prop.OpenDocumentNameAttribute ?? throw new InvalidOperationException("OpenDocumentNameAttribute missing"), valueString);
                    break;
                case int valueInt:
                    AddAttribute(elem, prop.OpenDocumentNameAttribute ?? throw new InvalidOperationException("OpenDocumentNameAttribute missing"), valueInt.ToString(CultureInfo.InvariantCulture));
                    break;
                case IEnumerable<OpenDocumentWritable> all:
                    foreach (var item in all)
                    {
                        elem.Add(item.GetElement());
                    }
                    break;
                case OpenDocumentWritable valueWritable:
                    elem.Add(valueWritable.GetElement());
                    break;
                case XElement element:
                    elem.Add(element);
                    break;
                default:
                    if (value is not null && prop.OpenDocumentNameAttribute is not null)
                    {
                        AddAttribute(elem, prop.OpenDocumentNameAttribute, value?.ToString() ?? string.Empty);
                    }
                    break;
            }
        }

        var content = TextContent();
        if (content is not null)
        {
            elem.Value = content;
        }

        return elem;
    }
}
