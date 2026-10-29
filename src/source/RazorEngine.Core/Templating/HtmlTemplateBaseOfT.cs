namespace RazorEngine.Templating
{
    using System;

    /// <summary>
    /// Provides a base implementation of an html template with a model.
    /// </summary>
    /// <remarks>
    /// This type adds nothing to <see cref="TemplateBase{T}"/>: the WriteAttribute* API has been migrated to the TemplateBase type.
    /// </remarks>
    /// <typeparam name="T">The model type.</typeparam>
    [Obsolete("HtmlTemplateBase<T> adds nothing to TemplateBase<T>; use TemplateBase<T> instead. HtmlTemplateBase<T> will be removed in 5.0.")]
    public class HtmlTemplateBase<T> : TemplateBase<T> { }
}