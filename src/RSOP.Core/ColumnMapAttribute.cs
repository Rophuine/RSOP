using System;

namespace RSOP.Core
{
    /// <summary>
    /// Attribute to associate a property with a database field. You CANNOT use the DefaultValue attribute as well as the ColumnMap attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public class ColumnMapAttribute : Attribute
    {
        private readonly string _columnName;
        public string ColumnName { get { return _columnName; } }
        public object DefaultValue { get; set; }
        public ColumnMapAttribute(string columnName)
        {
            _columnName = columnName;
        }
    }
}