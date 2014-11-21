using System;

namespace RSOP.Core
{
    /// <summary>
    /// Enables configuration of an RSOPItem to link it to a database. 
    /// Create a class marked up with this inheriting from UnmanagedBusinessItem as a parent for all other business items using the same DB connection string.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class DatabaseConfigurationAttribute : Attribute
    {
        private readonly string _connectionString;
        public string ConnectionString { get { return _connectionString; } }
        public DatabaseConfigurationAttribute(string connectionString)
        {
            _connectionString = connectionString;
        }
    }
}