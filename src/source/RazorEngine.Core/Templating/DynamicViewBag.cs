using RazorEngine.Common;
using System;
using System.Collections.Generic;
using System.Dynamic;

namespace RazorEngine.Templating
{
    /// <summary>
    /// Defines a dynamic view bag.
    /// </summary>
    [Serializable]
    public class DynamicViewBag : DynamicObject
    {
        #region Fields
        private readonly IDictionary<string, object> _dict = 
            new CrossAppDomainDictionary<string, object>();
        #endregion
        /// <summary>
        /// Create a new DynamicViewBag.
        /// </summary>
        public DynamicViewBag()
        {
        }

        /// <summary>
        /// Create a new DynamicViewBag by copying the given dictionary.
        /// </summary>
        /// <param name="dictionary"></param>
        public DynamicViewBag(IDictionary<string, object> dictionary)
            : this()
        {
            AddDictionary(dictionary);
        }

        /// <summary>
        /// Create a copy of the given DynamicViewBag.
        /// </summary>
        /// <param name="viewbag"></param>
        public DynamicViewBag(DynamicViewBag viewbag)
            : this(viewbag._dict)
        {
        }


        #region Methods

        /// <summary>
        /// Adds the given dictionary to the current DynamicViewBag instance.
        /// </summary>
        /// <param name="dictionary"></param>
        public void AddDictionary(IDictionary<string, object> dictionary)
        {
            foreach (var item in dictionary)
            {
                _dict.Add(item.Key, item.Value);
            }
        }

        /// <summary>
        /// Adds a single value.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="value"></param>
        public void AddValue(string propertyName, object value) 
        {
            _dict.Add(propertyName, value);
        }

        /// <summary>
        /// Gets the set of dynamic member names.
        /// </summary>
        /// <returns>An instance of <see cref="IEnumerable{String}"/>.</returns>
        public override IEnumerable<string> GetDynamicMemberNames()
        {
            return _dict.Keys;
        }

        /// <summary>
        /// Attempts to read a dynamic member from the object.
        /// </summary>
        /// <param name="binder">The binder.</param>
        /// <param name="result">The result instance.</param>
        /// <returns>True, always.</returns>
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            if (_dict.ContainsKey(binder.Name))
                result = _dict[binder.Name];
            else
                result = null;

            return true;
        }

        /// <summary>
        /// Attempts to set a value on the object.
        /// </summary>
        /// <param name="binder">The binder.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>True, always.</returns>
        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            if (_dict.ContainsKey(binder.Name))
                _dict[binder.Name] = value;
            else
                _dict.Add(binder.Name, value);

            return true;
        }
        #endregion
    }
}
