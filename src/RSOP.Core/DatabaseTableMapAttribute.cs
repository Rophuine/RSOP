using System;

namespace RSOP.Core
{
    /// <summary>
    /// Database table mapping attibute. The class must inherit from a parent class which has the DatabaseConfiguration attribute, and in turn inherits from UnmanagedBusinessItem.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class DatabaseTableMapAttribute : Attribute
    {
        private readonly string _tableName;
        public string TableName { get { return _tableName; } }
        private readonly string _primaryKey;
        public string PrimaryKey { get { return _primaryKey; } }
        public DatabaseTableMapAttribute(string tableName, string primaryKey)
        {
            _tableName = tableName; _primaryKey = primaryKey;
        }
    }
}