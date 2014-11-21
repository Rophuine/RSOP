using System.Collections.Generic;

namespace RSOP.Core
{
    /// <summary>
    /// A sorted dictionary of string,object which allows default values to be set for keys without actually entering that key into the collection.
    /// This allows returning a default value for some key values without the Keys collection containing that value. Primarily useful for avoiding
    /// divide-by-zero type errors without cluttering up your key collection.
    /// </summary>
    public class ItemDictionary : SortedDictionary<string, object>
    {
        private readonly Dictionary<string, object> _defaults = new Dictionary<string, object>();
        public new object this[string key]
        {
            get
            {
                if (ContainsKey(key)) return base[key];
                if (_defaults.ContainsKey(key)) return _defaults[key];
                return null;
            }
            set
            {
                base[key] = value;
            }
        }
        public void AddDefault(string key, object value)
        {
            _defaults.Add(key, value);
        }
    }
}