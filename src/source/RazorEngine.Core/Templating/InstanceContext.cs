namespace RazorEngine.Templating
{
    using System;

    /// <summary>
    /// Defines contextual information for a template instance.
    /// </summary>
    public class InstanceContext
    {
        #region Constructor
        /// <summary>
        /// Initialises a new instance of <see cref="InstanceContext"/>.
        /// </summary>
        /// <param name="loader">The type loader.</param>
        /// <param name="templateType">The template type.</param>
        internal InstanceContext(TypeLoader loader, Type templateType)
        {
            if (loader == null)
                throw new ArgumentNullException("loader");
            if (templateType == null)
                throw new ArgumentNullException("templateType");

            Loader = loader;
            TemplateType = templateType;
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets the type loader.
        /// </summary>
        public TypeLoader Loader { get; private set; }

        /// <summary>
        /// Gets the template type.
        /// </summary>
        public Type TemplateType { get; private set; }
        #endregion
    }
}