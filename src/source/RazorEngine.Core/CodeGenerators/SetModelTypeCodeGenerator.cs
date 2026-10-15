namespace RazorEngine.CodeGenerators
{
    using System;
    using System.Globalization;
    using System.Web.Razor.Generator;
    using Common;
    using System.Security;
    using RazorEngine.Compilation;

    [SecurityCritical]
    internal class SetModelTypeCodeGenerator : SetBaseTypeCodeGenerator
    {
        private readonly Func<Type, string, string> _builtBaseTypeName;

        [SecurityCritical]
        public SetModelTypeCodeGenerator(string modelType, Func<Type, string, string> builtBaseTypeName)
            : base(modelType)
        {
            _builtBaseTypeName = builtBaseTypeName;
        }

        private string GetBaseType(CodeGeneratorContext context, string baseType)
        {
            var host = (RazorEngineHost)context.Host;
            return _builtBaseTypeName(host.DefaultBaseTemplateType, baseType);
        }
        [SecurityCritical]
        protected override string ResolveType(CodeGeneratorContext context, string baseType)
        {
            return GetBaseType(context, baseType);
        }

        [SecuritySafeCritical]
        public override bool Equals(object obj)
        {
            SetModelTypeCodeGenerator other = obj as SetModelTypeCodeGenerator;
            return other != null &&
                   base.Equals(obj);
        }

        [SecuritySafeCritical]
        public override int GetHashCode()
        {
            return HashCodeCombiner.Start()
                .Add(base.GetHashCode())
                .CombinedHash;
        }

        [SecuritySafeCritical]
        public override string ToString()
        {
            return "Model:" + BaseType;
        }
    }
}
