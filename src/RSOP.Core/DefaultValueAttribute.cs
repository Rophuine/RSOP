using System;

namespace RSOP.Core
{
    /// <summary>
    /// Markup to allow an unmapped property to be associated with a default value. Your property must handle storing and retrieving from this[propertyname] itself, and must
    /// store its value there. If the value is accessed before any value has been set, the default value will be used instead of calling your property's get method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public class DefaultValueAttribute : Attribute
    {
        private readonly object _value;
        public object Value { get { return _value; } }
        public DefaultValueAttribute(object value)
        {
            _value = value;
        }
    }
}