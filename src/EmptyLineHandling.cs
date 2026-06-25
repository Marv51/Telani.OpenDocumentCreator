namespace OpenDocumentCreator;

/// <summary>
/// Controls how empty lines within an <see cref="OpenDocumentCell.Content"/> are rendered
/// when the content is split into paragraphs on its line breaks.
/// </summary>
public enum EmptyLineHandling
{
    /// <summary>
    /// Drop all empty lines: consecutive, leading and trailing line breaks collapse.
    /// This is the default and matches the historical behaviour.
    /// </summary>
    Collapse,

    /// <summary>
    /// Keep every empty line as an empty paragraph.
    /// </summary>
    Preserve,

    /// <summary>
    /// Keep empty lines between content, but trim empty lines at the start and end.
    /// </summary>
    TrimEnds,
}
